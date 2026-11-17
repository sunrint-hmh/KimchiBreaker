using System;
using KimchiBreaker.Core;
using NUnit.Framework;

namespace KimchiBreaker.Tests
{
    public class ArenaLayoutTests
    {
        [Test]
        public void Seats_AreDistinct_InsideWalkArea_AndFaceTheCentre()
        {
            var arena = new ArenaLayout();
            Assert.IsTrue(arena.IsValid);
            for (int seat = 0; seat < GameRules.MaxPlayers; seat++)
            {
                arena.GetSeatPose(seat, out float x, out float z, out float yaw);
                Assert.LessOrEqual(Math.Abs(x), arena.WalkHalfSize);
                Assert.LessOrEqual(Math.Abs(z), arena.WalkHalfSize);
                Assert.AreEqual(arena.SeatDistance, Math.Sqrt(x * x + z * z), 1e-4);

                // 바라보는 방향 벡터가 중심을 향해야 한다.
                double radians = yaw * Math.PI / 180.0;
                double forwardX = Math.Sin(radians);
                double forwardZ = Math.Cos(radians);
                double dot = forwardX * -x + forwardZ * -z;
                Assert.AreEqual(arena.SeatDistance, dot, 1e-3, "좌석 " + seat);

                for (int other = seat + 1; other < GameRules.MaxPlayers; other++)
                {
                    arena.GetSeatPose(other, out float ox, out float oz, out _);
                    Assert.Greater(Math.Abs(ox - x) + Math.Abs(oz - z), 1.0);
                }
            }
        }

        [Test]
        public void FirstTwoSeats_FaceEachOther()
        {
            var arena = new ArenaLayout();
            arena.GetSeatPose(0, out float x0, out float z0, out _);
            arena.GetSeatPose(1, out float x1, out float z1, out _);
            Assert.AreEqual(-x0, x1, 1e-5);
            Assert.AreEqual(-z0, z1, 1e-5);
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void InvalidSeat_Throws(int seat)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArenaLayout().GetSeatPose(seat, out _, out _, out _));
        }
    }

    public class MovementValidatorTests
    {
        const float MaxSpeed = 5f;
        ArenaLayout m_Arena;
        MovementValidator m_Validator;

        [SetUp]
        public void SetUp()
        {
            m_Arena = new ArenaLayout();
            m_Validator = new MovementValidator(m_Arena, MaxSpeed);
            m_Validator.Reset(new PoseSample { X = 0, Y = 0, Z = 0 }, 0.0);
        }

        static PoseSample At(float x, float z, float yaw = 0f, float pitch = 0f)
        {
            return new PoseSample { X = x, Y = 0f, Z = z, Yaw = yaw, Pitch = pitch };
        }

        [Test]
        public void BeforeReset_EverythingIsRejected()
        {
            var validator = new MovementValidator(m_Arena, MaxSpeed);
            Assert.IsFalse(validator.HasPose);
            Assert.AreEqual(PoseVerdict.Rejected, validator.Validate(At(0, 0), 1.0, true, out _));
        }

        [Test]
        public void WalkingAtFullSpeed_IsAcceptedIndefinitely()
        {
            // 초당 30회 보고, 최대 속도로 좌우 왕복
            double time = 0.0;
            float x = 0f;
            float direction = 1f;
            for (int i = 0; i < 3000; i++)
            {
                time += 1.0 / 30.0;
                x += direction * MaxSpeed / 30f;
                if (Math.Abs(x) > 6f)
                {
                    direction = -direction;
                }

                PoseVerdict verdict = m_Validator.Validate(At(x, 0), time, true, out PoseSample accepted);
                Assert.AreEqual(PoseVerdict.Accepted, verdict, "표본 " + i);
                Assert.AreEqual(x, accepted.X, 1e-5);
            }
        }

        [Test]
        public void JitteryPackets_AtFullSpeed_AreAccepted()
        {
            // 패킷이 0.4초 동안 안 오다가 한꺼번에 도착하는 상황
            var random = new Random(7);
            double time = 0.0;
            float x = -6f;
            m_Validator.Reset(At(x, 0), 0.0);
            for (int i = 0; i < 400; i++)
            {
                double gap = random.NextDouble() < 0.1 ? 0.4 : 1.0 / 30.0;
                time += gap;
                x += (float)(MaxSpeed * gap);
                if (x > 6f)
                {
                    x = -6f;
                    m_Validator.Reset(At(x, 0), time);
                    continue;
                }

                Assert.AreNotEqual(PoseVerdict.Rejected, m_Validator.Validate(At(x, 0), time, true, out _), "표본 " + i);
            }
        }

        [Test]
        public void Teleport_IsRejected_AndPositionIsKept()
        {
            PoseVerdict verdict = m_Validator.Validate(At(6, 6), 0.033, true, out PoseSample accepted);
            Assert.AreEqual(PoseVerdict.Rejected, verdict);
            Assert.AreEqual(0f, accepted.X);
            Assert.AreEqual(0f, accepted.Z);
            Assert.AreEqual(0f, m_Validator.Last.X);
        }

        [Test]
        public void SpeedHack_CannotOutrunTheBudget()
        {
            // 10초 동안 최대 속도의 3배로 달리려는 클라이언트
            double time = 0.0;
            float x = -6.5f;
            m_Validator.Reset(At(x, 0), 0.0);
            for (int i = 0; i < 300; i++)
            {
                time += 1.0 / 30.0;
                float wanted = m_Validator.Last.X + 3f * MaxSpeed / 30f;
                m_Validator.Validate(At(Math.Min(wanted, 6.5f), 0), time, true, out _);
            }

            float travelled = m_Validator.Last.X - x;
            // 허용 오차(1.25배) + 버스트 예산을 넘어서는 이동은 불가능하다.
            Assert.LessOrEqual(travelled, MaxSpeed * 10.0 * 1.25 + MaxSpeed * 0.75 + 0.01);
        }

        [Test]
        public void LongIdle_DoesNotBankUnlimitedDistance()
        {
            // 오래 가만히 있다가 한 번에 멀리 이동하는 꼼수 방지
            PoseVerdict verdict = m_Validator.Validate(At(6.9f, 0), 600.0, true, out _);
            Assert.AreEqual(PoseVerdict.Rejected, verdict);
            Assert.AreEqual(PoseVerdict.Accepted, m_Validator.Validate(At(3f, 0), 600.0, true, out _));
        }

        [Test]
        public void OutOfBounds_IsClampedToTheWalkArea()
        {
            m_Validator.Reset(At(6.9f, 0), 0.0);
            PoseVerdict verdict = m_Validator.Validate(At(50f, 0), 0.5, true, out PoseSample accepted);
            Assert.AreEqual(PoseVerdict.Clamped, verdict);
            Assert.AreEqual(m_Arena.WalkHalfSize, accepted.X);
        }

        [Test]
        public void Height_IsClamped()
        {
            var pose = At(0, 0);
            pose.Y = 100f;
            Assert.AreEqual(PoseVerdict.Clamped, m_Validator.Validate(pose, 0.1, true, out PoseSample accepted));
            Assert.AreEqual(m_Arena.MaxY, accepted.Y);
            pose.Y = -100f;
            m_Validator.Validate(pose, 0.2, true, out accepted);
            Assert.AreEqual(m_Arena.MinY, accepted.Y);
        }

        [Test]
        public void NonFiniteValues_AreRejected()
        {
            foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.AreEqual(PoseVerdict.Rejected, m_Validator.Validate(new PoseSample { X = bad }, 0.1, true, out _));
                Assert.AreEqual(PoseVerdict.Rejected, m_Validator.Validate(new PoseSample { Y = bad }, 0.1, true, out _));
                Assert.AreEqual(PoseVerdict.Rejected, m_Validator.Validate(new PoseSample { Z = bad }, 0.1, true, out _));
                Assert.AreEqual(PoseVerdict.Rejected, m_Validator.Validate(new PoseSample { Yaw = bad }, 0.1, true, out _));
                Assert.AreEqual(PoseVerdict.Rejected, m_Validator.Validate(new PoseSample { Pitch = bad }, 0.1, true, out _));
            }

            Assert.AreEqual(PoseVerdict.Rejected, m_Validator.Validate(At(0, 0), double.NaN, true, out _));
            Assert.AreEqual(0f, m_Validator.Last.X);
        }

        [Test]
        public void LookAngles_AreNormalizedAndClamped()
        {
            m_Validator.Validate(At(0, 0, 725f, 200f), 0.1, true, out PoseSample accepted);
            Assert.AreEqual(5f, accepted.Yaw, 1e-3);
            Assert.AreEqual(MovementValidator.MaxPitch, accepted.Pitch);

            m_Validator.Validate(At(0, 0, -90f, -200f), 0.2, true, out accepted);
            Assert.AreEqual(270f, accepted.Yaw, 1e-3);
            Assert.AreEqual(-MovementValidator.MaxPitch, accepted.Pitch);
        }

        [TestCase(0f, 0f)]
        [TestCase(360f, 0f)]
        [TestCase(-360f, 0f)]
        [TestCase(-0.00001f, 0f)]
        [TestCase(1e30f, 0f)]
        [TestCase(float.NaN, 0f)]
        [TestCase(450f, 90f)]
        public void NormalizeYaw_AlwaysReturnsZeroToBelow360(float input, float expected)
        {
            float result = MovementValidator.NormalizeYaw(input);
            Assert.GreaterOrEqual(result, 0f);
            Assert.Less(result, 360f);
            if (!float.IsNaN(input) && Math.Abs(input) < 1e6f)
            {
                Assert.AreEqual(expected, result, 1e-3);
            }
        }

        [Test]
        public void WhenMovementIsLocked_PositionIsFrozenButLookIsApplied()
        {
            m_Validator.Reset(At(1, 2), 0.0);
            PoseVerdict verdict = m_Validator.Validate(At(1.5f, 2f, 45f, 10f), 0.1, false, out PoseSample accepted);
            Assert.AreEqual(PoseVerdict.Rejected, verdict);
            Assert.AreEqual(1f, accepted.X);
            Assert.AreEqual(2f, accepted.Z);
            Assert.AreEqual(45f, accepted.Yaw);
            Assert.AreEqual(10f, accepted.Pitch);

            Assert.AreEqual(PoseVerdict.Accepted, m_Validator.Validate(At(1f, 2f, 90f, 0f), 0.2, false, out accepted));
            Assert.AreEqual(90f, accepted.Yaw);
        }

        [Test]
        public void RejectedMove_StillAppliesLookDirection()
        {
            m_Validator.Validate(At(6, 6, 123f, 45f), 0.033, true, out PoseSample accepted);
            Assert.AreEqual(123f, accepted.Yaw);
            Assert.AreEqual(45f, accepted.Pitch);
        }

        [Test]
        public void InvalidConstruction_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MovementValidator(null, 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MovementValidator(m_Arena, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MovementValidator(m_Arena, float.NaN));
            Assert.Throws<ArgumentException>(() => new MovementValidator(new ArenaLayout { WalkHalfSize = 1f }, 5f));
        }

        [Test]
        public void RandomGarbage_NeverEscapesBoundsOrThrows()
        {
            var random = new Random(99);
            double time = 0.0;
            for (int i = 0; i < 20000; i++)
            {
                time += random.NextDouble() * 0.1 - 0.01; // 가끔 시간이 거꾸로 가기도 한다
                var pose = new PoseSample
                {
                    X = Garbage(random),
                    Y = Garbage(random),
                    Z = Garbage(random),
                    Yaw = Garbage(random),
                    Pitch = Garbage(random),
                };
                m_Validator.Validate(pose, time, random.Next(2) == 0, out PoseSample accepted);
                Assert.LessOrEqual(Math.Abs(accepted.X), m_Arena.WalkHalfSize);
                Assert.LessOrEqual(Math.Abs(accepted.Z), m_Arena.WalkHalfSize);
                Assert.That(accepted.Y, Is.InRange(m_Arena.MinY, m_Arena.MaxY));
                Assert.That(accepted.Pitch, Is.InRange(-MovementValidator.MaxPitch, MovementValidator.MaxPitch));
                Assert.That(accepted.Yaw, Is.InRange(0f, 360f));
                Assert.IsFalse(float.IsNaN(accepted.X + accepted.Y + accepted.Z + accepted.Yaw + accepted.Pitch));
            }
        }

        static float Garbage(Random random)
        {
            switch (random.Next(8))
            {
                case 0: return float.NaN;
                case 1: return float.PositiveInfinity;
                case 2: return float.NegativeInfinity;
                case 3: return float.MaxValue;
                case 4: return float.MinValue;
                case 5: return (float)(random.NextDouble() * 2000.0 - 1000.0);
                default: return (float)(random.NextDouble() * 16.0 - 8.0);
            }
        }
    }
}
