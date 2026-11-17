using System;

namespace KimchiBreaker.Core
{
    /// <summary>플레이어 자세 표본(발 위치 + 몸 방향 + 고개 숙임).</summary>
    public struct PoseSample
    {
        public float X;
        public float Y;
        public float Z;

        /// <summary>몸이 바라보는 방향(Y축 회전, 도).</summary>
        public float Yaw;

        /// <summary>고개 상하 각도(도). 위를 보면 음수, 아래를 보면 양수.</summary>
        public float Pitch;
    }

    public enum PoseVerdict
    {
        /// <summary>그대로 수용.</summary>
        Accepted,

        /// <summary>범위를 벗어난 값을 보정해서 수용.</summary>
        Clamped,

        /// <summary>거절. 마지막으로 인정된 자세를 유지하고, 클라이언트를 그 자리로 되돌려야 한다.</summary>
        Rejected,
    }

    /// <summary>
    /// 클라이언트가 보고한 자세를 서버에서 검증한다(플레이어 1명당 1개).
    /// 순간이동/속도 조작을 막기 위해 "이동 거리 예산"을 시간에 따라 채우고, 이동한 만큼 차감한다.
    /// </summary>
    public sealed class MovementValidator
    {
        /// <summary>정상 클라이언트의 오차(프레임 시간 차, 네트워크 지연)를 감안한 여유 배율.</summary>
        const double k_SpeedTolerance = 1.25;

        /// <summary>패킷이 몰려서 도착하는 경우를 위해 쌓아 둘 수 있는 예산(초 단위).</summary>
        const double k_BurstSeconds = 0.75;

        public const float MaxPitch = 89f;

        readonly ArenaLayout m_Arena;
        readonly double m_MaxSpeed;
        PoseSample m_Last;
        double m_LastTime;
        double m_Budget;
        bool m_HasLast;

        public MovementValidator(ArenaLayout arena, float maxSpeed)
        {
            if (arena == null)
            {
                throw new ArgumentNullException(nameof(arena));
            }

            if (!arena.IsValid)
            {
                throw new ArgumentException("경기장 수치가 올바르지 않습니다.", nameof(arena));
            }

            if (!(maxSpeed > 0f) || float.IsInfinity(maxSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(maxSpeed));
            }

            m_Arena = arena;
            m_MaxSpeed = maxSpeed;
        }

        /// <summary>마지막으로 인정된 자세.</summary>
        public PoseSample Last => m_Last;

        public bool HasPose => m_HasLast;

        /// <summary>서버가 직접 위치를 지정할 때(좌석 배치 등) 호출한다.</summary>
        public void Reset(PoseSample pose, double now)
        {
            m_Last = Sanitize(pose, out _);
            m_LastTime = now;
            m_Budget = m_MaxSpeed * k_BurstSeconds * 0.5;
            m_HasLast = true;
        }

        /// <summary>
        /// 클라이언트가 보고한 자세를 검증한다. <paramref name="accepted"/> 에는 서버가 인정한 자세가 담긴다.
        /// </summary>
        /// <param name="allowMove">false 면 위치 변경은 무시하고 시선(Yaw/Pitch)만 반영한다.</param>
        public PoseVerdict Validate(PoseSample requested, double now, bool allowMove, out PoseSample accepted)
        {
            accepted = m_Last;
            if (!m_HasLast || double.IsNaN(now) || double.IsInfinity(now) || !IsFinite(requested))
            {
                return PoseVerdict.Rejected;
            }

            double elapsed = now - m_LastTime;
            if (elapsed > 0.0)
            {
                m_Budget = Math.Min(m_MaxSpeed * k_BurstSeconds, m_Budget + elapsed * m_MaxSpeed * k_SpeedTolerance);
                m_LastTime = now;
            }

            PoseSample candidate = Sanitize(requested, out bool clamped);
            if (!allowMove)
            {
                bool moved = candidate.X != m_Last.X || candidate.Y != m_Last.Y || candidate.Z != m_Last.Z;
                candidate.X = m_Last.X;
                candidate.Y = m_Last.Y;
                candidate.Z = m_Last.Z;
                m_Last = candidate;
                accepted = candidate;
                return moved ? PoseVerdict.Rejected : (clamped ? PoseVerdict.Clamped : PoseVerdict.Accepted);
            }

            double dx = candidate.X - m_Last.X;
            double dz = candidate.Z - m_Last.Z;
            double distance = Math.Sqrt(dx * dx + dz * dz);
            if (distance > m_Budget)
            {
                // 위치는 거절하되 시선은 반영한다(시선은 속도 제한 대상이 아니다).
                m_Last.Yaw = candidate.Yaw;
                m_Last.Pitch = candidate.Pitch;
                accepted = m_Last;
                return PoseVerdict.Rejected;
            }

            m_Budget -= distance;
            m_Last = candidate;
            accepted = candidate;
            return clamped ? PoseVerdict.Clamped : PoseVerdict.Accepted;
        }

        PoseSample Sanitize(PoseSample pose, out bool clamped)
        {
            clamped = false;
            float limit = m_Arena.WalkHalfSize;
            pose.X = Clamp(pose.X, -limit, limit, ref clamped);
            pose.Z = Clamp(pose.Z, -limit, limit, ref clamped);
            pose.Y = Clamp(pose.Y, m_Arena.MinY, m_Arena.MaxY, ref clamped);
            pose.Pitch = Clamp(pose.Pitch, -MaxPitch, MaxPitch, ref clamped);
            pose.Yaw = NormalizeYaw(pose.Yaw);
            return pose;
        }

        static float Clamp(float value, float min, float max, ref bool clamped)
        {
            if (value < min)
            {
                clamped = true;
                return min;
            }

            if (value > max)
            {
                clamped = true;
                return max;
            }

            return value;
        }

        /// <summary>각도를 0 이상 360 미만으로 맞춘다.</summary>
        public static float NormalizeYaw(float yaw)
        {
            if (float.IsNaN(yaw) || float.IsInfinity(yaw))
            {
                return 0f;
            }

            yaw %= 360f;
            if (yaw < 0f)
            {
                yaw += 360f;
            }

            return yaw >= 360f ? 0f : yaw;
        }

        static bool IsFinite(PoseSample pose)
        {
            return IsFinite(pose.X) && IsFinite(pose.Y) && IsFinite(pose.Z) && IsFinite(pose.Yaw) && IsFinite(pose.Pitch);
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
