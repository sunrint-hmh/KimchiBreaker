using System;
using System.Text;
using KimchiBreaker.Core;
using NUnit.Framework;

namespace KimchiBreaker.Tests
{
    public class ConnectionPayloadTests
    {
        [Test]
        public void RoundTrip_PreservesVersionAndName()
        {
            byte[] payload = ConnectionPayload.Encode("김치왕");
            Assert.IsTrue(ConnectionPayload.TryDecode(payload, out int version, out string name));
            Assert.AreEqual(GameRules.ProtocolVersion, version);
            Assert.AreEqual("김치왕", name);
            Assert.LessOrEqual(payload.Length, ConnectionPayload.MaxBytes);
        }

        [Test]
        public void Encode_SanitizesAndLimitsTheName()
        {
            byte[] payload = ConnectionPayload.Encode("<b>" + new string('가', 100) + "</b>");
            Assert.IsTrue(ConnectionPayload.TryDecode(payload, out _, out string name));
            Assert.IsFalse(name.Contains("<"));
            Assert.LessOrEqual(name.Length, GameRules.MaxNicknameLength);
        }

        [Test]
        public void EmptyName_IsAllowed()
        {
            byte[] payload = ConnectionPayload.Encode(null);
            Assert.IsTrue(ConnectionPayload.TryDecode(payload, out _, out string name));
            Assert.AreEqual(string.Empty, name);
        }

        [Test]
        public void MalformedPayloads_AreRejected()
        {
            byte[] good = ConnectionPayload.Encode("abc");
            Assert.IsFalse(ConnectionPayload.TryDecode(null, out _, out _));
            Assert.IsFalse(ConnectionPayload.TryDecode(new byte[0], out _, out _));
            Assert.IsFalse(ConnectionPayload.TryDecode(new byte[8], out _, out _));
            Assert.IsFalse(ConnectionPayload.TryDecode(new byte[ConnectionPayload.MaxBytes + 1], out _, out _));

            byte[] badMagic = (byte[])good.Clone();
            badMagic[0] ^= 0xFF;
            Assert.IsFalse(ConnectionPayload.TryDecode(badMagic, out _, out _));

            byte[] lengthTooLong = (byte[])good.Clone();
            lengthTooLong[8] = 200;
            Assert.IsFalse(ConnectionPayload.TryDecode(lengthTooLong, out _, out _));

            byte[] lengthMismatch = (byte[])good.Clone();
            lengthMismatch[8] = 2;
            Assert.IsFalse(ConnectionPayload.TryDecode(lengthMismatch, out _, out _));

            byte[] trailing = new byte[good.Length + 1];
            Buffer.BlockCopy(good, 0, trailing, 0, good.Length);
            Assert.IsFalse(ConnectionPayload.TryDecode(trailing, out _, out _));
        }

        [Test]
        public void InvalidUtf8_IsRejected()
        {
            byte[] good = ConnectionPayload.Encode("ab");
            good[9] = 0xC3; // 잘린 2바이트 시퀀스
            good[10] = 0x28;
            Assert.IsFalse(ConnectionPayload.TryDecode(good, out _, out string name));
            Assert.AreEqual(string.Empty, name);
        }

        [Test]
        public void DifferentProtocolVersion_IsReported()
        {
            byte[] payload = ConnectionPayload.Encode("a");
            payload[4] = 99;
            Assert.IsTrue(ConnectionPayload.TryDecode(payload, out int version, out _));
            Assert.AreNotEqual(GameRules.ProtocolVersion, version);
        }

        [Test]
        public void RandomBytes_NeverThrow()
        {
            var random = new Random(5);
            for (int i = 0; i < 20000; i++)
            {
                var bytes = new byte[random.Next(0, ConnectionPayload.MaxBytes + 10)];
                random.NextBytes(bytes);
                if (bytes.Length >= 4 && random.Next(2) == 0)
                {
                    byte[] good = ConnectionPayload.Encode("x");
                    Buffer.BlockCopy(good, 0, bytes, 0, 4);
                }

                if (ConnectionPayload.TryDecode(bytes, out _, out string name))
                {
                    Assert.LessOrEqual(Encoding.UTF8.GetByteCount(name), GameRules.MaxNicknameUtf8Bytes);
                }
            }
        }
    }

    public class RequestGuardTests
    {
        [Test]
        public void NormalPlay_NeverAccumulatesEnoughStrikesToKick()
        {
            var guard = new RequestGuard();
            double now = 0.0;
            // 10분 동안 초당 30회 자세 보고 + 초당 12회 타격
            for (int i = 0; i < 600 * 30; i++)
            {
                now += 1.0 / 30.0;
                Assert.IsTrue(guard.Allow(RequestKind.Pose, now));
                if (i % 5 < 2)
                {
                    Assert.IsTrue(guard.Allow(RequestKind.Hit, now));
                }
            }

            Assert.IsFalse(guard.ShouldKick);
            Assert.AreEqual(0.0, guard.Strikes, 1e-9);
        }

        [Test]
        public void Flooding_IsLimitedAndEventuallyKicked()
        {
            var guard = new RequestGuard();
            int allowed = 0;
            for (int i = 0; i < 10000; i++)
            {
                if (guard.Allow(RequestKind.Hit, i * 0.0001)) // 1초에 10000회
                {
                    allowed++;
                }
            }

            Assert.LessOrEqual(allowed, 30 + 22 + 1);
            Assert.IsTrue(guard.ShouldKick);
        }

        [Test]
        public void Strikes_DecayOverTime()
        {
            var guard = new RequestGuard();
            guard.ReportViolation(50, 0.0);
            Assert.AreEqual(50.0, guard.Strikes, 1e-9);
            guard.ReportViolation(0, 4.0);
            Assert.AreEqual(30.0, guard.Strikes, 1e-9);
            guard.ReportViolation(0, 1000.0);
            Assert.AreEqual(0.0, guard.Strikes, 1e-9);
        }

        [Test]
        public void BadInputs_DoNotCorruptState()
        {
            var guard = new RequestGuard();
            guard.ReportViolation(double.NaN, 0.0);
            guard.ReportViolation(-100, 0.0);
            guard.ReportViolation(5, double.NaN);
            Assert.AreEqual(5.0, guard.Strikes, 1e-9);
            Assert.IsFalse(guard.Allow((RequestKind)99, 1.0));
            Assert.IsFalse(guard.Allow((RequestKind)(-1), 1.0));
            guard.ReportViolation(double.PositiveInfinity, 2.0);
            Assert.IsFalse(double.IsInfinity(guard.Strikes));
            Assert.IsTrue(guard.ShouldKick);
        }

        [Test]
        public void Kinds_HaveIndependentBudgets()
        {
            var guard = new RequestGuard();
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(guard.Allow(RequestKind.Skill, 0.0));
            }

            Assert.IsFalse(guard.Allow(RequestKind.Skill, 0.0));
            Assert.IsTrue(guard.Allow(RequestKind.Lobby, 0.0));
            Assert.IsTrue(guard.Allow(RequestKind.Hit, 0.0));
        }
    }

    public class HitGeometryTests
    {
        static PoseSample Pose(float x, float z, float yaw, float pitch = 0f)
        {
            return new PoseSample { X = x, Y = 0f, Z = z, Yaw = yaw, Pitch = pitch };
        }

        [TestCase(0f, 0f, 0f, 0f, 1f)]
        [TestCase(90f, 0f, 1f, 0f, 0f)]
        [TestCase(180f, 0f, 0f, 0f, -1f)]
        [TestCase(270f, 0f, -1f, 0f, 0f)]
        [TestCase(0f, 90f, 0f, -1f, 0f)]
        [TestCase(0f, -90f, 0f, 1f, 0f)]
        public void LookDirection_MatchesUnityConventions(float yaw, float pitch, float ex, float ey, float ez)
        {
            HitGeometry.LookDirection(yaw, pitch, out float x, out float y, out float z);
            Assert.AreEqual(ex, x, 1e-5);
            Assert.AreEqual(ey, y, 1e-5);
            Assert.AreEqual(ez, z, 1e-5);
        }

        [Test]
        public void TargetInFront_IsReachable()
        {
            // 남쪽 좌석에서 북쪽(포대 중앙)을 내려다보는 플레이어
            var pose = Pose(0f, -5.5f, 0f, 15f);
            Assert.IsTrue(HitGeometry.CanReach(pose, 0f, 0.2f, 0f, 0.3f));
            Assert.IsTrue(HitGeometry.CanReach(pose, 3.5f, 0.2f, 3.5f, 0.3f), "포대 반대편 모서리");
        }

        [Test]
        public void TargetBehind_IsNotReachable()
        {
            var pose = Pose(0f, -5.5f, 0f, 15f);
            Assert.IsFalse(HitGeometry.CanReach(pose, 0f, 0.2f, -9f, 0.3f));
        }

        [Test]
        public void TargetTooFar_IsNotReachable()
        {
            var pose = Pose(0f, 0f, 0f, 0f);
            Assert.IsFalse(HitGeometry.CanReach(pose, 0f, HitGeometry.EyeHeight, HitGeometry.MaxReach + 1f, 0.3f));
        }

        [Test]
        public void TargetRightUnderneath_IsReachableRegardlessOfLook()
        {
            var pose = Pose(0f, 0f, 180f, -80f);
            Assert.IsTrue(HitGeometry.CanReach(pose, 0.1f, HitGeometry.EyeHeight - 0.3f, 0.1f, 0.3f));
        }

        [Test]
        public void NonFiniteTargets_AreRejected()
        {
            var pose = Pose(0f, 0f, 0f);
            Assert.IsFalse(HitGeometry.CanReach(pose, float.NaN, 0f, 1f, 0.3f));
            Assert.IsFalse(HitGeometry.CanReach(pose, 0f, float.PositiveInfinity, 1f, 0.3f));
            Assert.IsFalse(HitGeometry.CanReach(pose, 0f, 0f, 1f, float.NaN));
        }
    }
}
