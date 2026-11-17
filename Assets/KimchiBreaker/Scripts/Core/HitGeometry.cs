using System;

namespace KimchiBreaker.Core
{
    /// <summary>타격 판정에 쓰는 기하 계산. Unity 좌표계와 같은 규칙을 따른다.</summary>
    public static class HitGeometry
    {
        /// <summary>바닥에서 눈까지의 높이(미터).</summary>
        public const float EyeHeight = 1.6f;

        /// <summary>서버가 허용하는 최대 타격 거리(미터). 포대 반대편 모서리까지 닿는 거리.</summary>
        public const float MaxReach = 14f;

        /// <summary>서버가 허용하는 시선과 목표 방향 사이의 최대 각도. 지연을 감안해 넉넉히 잡는다.</summary>
        public const float MaxAngleDegrees = 50f;

        /// <summary>
        /// Yaw/Pitch(도)로부터 바라보는 방향 단위 벡터를 구한다.
        /// Unity 의 Quaternion.Euler(pitch, yaw, 0) * Vector3.forward 와 같다(pitch 가 양수면 아래를 본다).
        /// </summary>
        public static void LookDirection(float yawDegrees, float pitchDegrees, out float x, out float y, out float z)
        {
            double yaw = yawDegrees * Math.PI / 180.0;
            double pitch = pitchDegrees * Math.PI / 180.0;
            double cosPitch = Math.Cos(pitch);
            x = (float)(Math.Sin(yaw) * cosPitch);
            y = (float)(-Math.Sin(pitch));
            z = (float)(Math.Cos(yaw) * cosPitch);
        }

        /// <summary>
        /// 플레이어(발 위치 + 시선)가 목표 지점을 타격할 수 있는 위치/방향인지 판정한다.
        /// </summary>
        public static bool CanReach(PoseSample pose, float targetX, float targetY, float targetZ, float targetRadius)
        {
            if (!IsFinite(targetX) || !IsFinite(targetY) || !IsFinite(targetZ) || !IsFinite(targetRadius))
            {
                return false;
            }

            float eyeX = pose.X;
            float eyeY = pose.Y + EyeHeight;
            float eyeZ = pose.Z;
            double dx = targetX - eyeX;
            double dy = targetY - eyeY;
            double dz = targetZ - eyeZ;
            double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            double radius = Math.Max(0.0, Math.Min(5.0, targetRadius));
            if (distance > MaxReach + radius)
            {
                return false;
            }

            if (distance <= radius + 0.5)
            {
                return true; // 바로 코앞이면 방향은 따지지 않는다
            }

            LookDirection(pose.Yaw, pose.Pitch, out float lx, out float ly, out float lz);
            double cos = (dx * lx + dy * ly + dz * lz) / distance;
            double allowed = MaxAngleDegrees + Math.Atan2(radius, distance) * 180.0 / Math.PI;
            return cos >= Math.Cos(Math.Min(180.0, allowed) * Math.PI / 180.0);
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
