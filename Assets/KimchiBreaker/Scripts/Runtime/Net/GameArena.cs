using KimchiBreaker.Core;
using UnityEngine;

namespace KimchiBreaker.Net
{
    /// <summary>경기장 배치 수치를 Unity 좌표로 다루는 도우미. 모든 피어가 같은 값을 쓴다.</summary>
    public static class GameArena
    {
        /// <summary>경기장 수치(포대 크기, 좌석 거리 등). 바꾸면 모든 피어가 같은 빌드를 써야 한다.</summary>
        public static readonly ArenaLayout Layout = new ArenaLayout();

        /// <summary>좌석의 발 위치.</summary>
        public static Vector3 SeatPosition(int seat)
        {
            Layout.GetSeatPose(Mathf.Clamp(seat, 0, GameRules.MaxPlayers - 1), out float x, out float z, out _);
            return new Vector3(x, 0f, z);
        }

        /// <summary>좌석에서 포대 중앙을 바라보는 방향(Y축 회전, 도).</summary>
        public static float SeatYaw(int seat)
        {
            Layout.GetSeatPose(Mathf.Clamp(seat, 0, GameRules.MaxPlayers - 1), out _, out _, out float yaw);
            return yaw;
        }

        public static Quaternion SeatRotation(int seat) => Quaternion.Euler(0f, SeatYaw(seat), 0f);

        /// <summary>임의의 좌표를 경기장 안으로 보정한다. NaN/무한대는 원점으로 바꾼다.</summary>
        public static Vector3 ClampToArena(Vector3 point)
        {
            float limit = Layout.WalkHalfSize;
            return new Vector3(
                Finite(point.x) ? Mathf.Clamp(point.x, -limit, limit) : 0f,
                Finite(point.y) ? Mathf.Clamp(point.y, -1f, 6f) : 0f,
                Finite(point.z) ? Mathf.Clamp(point.z, -limit, limit) : 0f);
        }

        /// <summary>포대 위의 무작위 지점(가장자리에서 조금 안쪽).</summary>
        public static Vector3 RandomPointOnTarp(System.Random random, float height)
        {
            float half = Layout.TarpHalfSize * 0.8f;
            float x = (float)(random.NextDouble() * 2.0 - 1.0) * half;
            float z = (float)(random.NextDouble() * 2.0 - 1.0) * half;
            return new Vector3(x, height, z);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
