using System;

namespace KimchiBreaker.Core
{
    /// <summary>
    /// 김장포대와 좌석의 배치 수치. 엔진과 무관한 순수 수치라서 서버 검증과 테스트에 그대로 쓴다.
    /// 좌표계는 Unity 와 같다(X: 오른쪽, Y: 위, Z: 앞). 중심은 원점.
    /// </summary>
    public sealed class ArenaLayout
    {
        /// <summary>김장포대 한 변의 절반 길이(미터).</summary>
        public float TarpHalfSize = 4f;

        /// <summary>포대 중심에서 좌석까지의 거리(미터).</summary>
        public float SeatDistance = 5.5f;

        /// <summary>플레이어가 걸어 다닐 수 있는 정사각형 영역의 절반 길이(미터).</summary>
        public float WalkHalfSize = 7f;

        /// <summary>플레이어 발 위치가 가질 수 있는 최소/최대 높이.</summary>
        public float MinY = -0.5f;

        public float MaxY = 3f;

        /// <summary>
        /// 좌석 번호(0~3)의 위치와 바라보는 방향.
        /// 입장 순서대로 남 → 북 → 서 → 동 에 앉아, 2명일 때 서로 마주 보게 한다.
        /// </summary>
        /// <param name="yawDegrees">Y축 회전 각도. 0 이면 +Z(북쪽)를 바라본다.</param>
        public void GetSeatPose(int seat, out float x, out float z, out float yawDegrees)
        {
            switch (seat)
            {
                case 0:
                    x = 0f;
                    z = -SeatDistance;
                    yawDegrees = 0f;
                    break;
                case 1:
                    x = 0f;
                    z = SeatDistance;
                    yawDegrees = 180f;
                    break;
                case 2:
                    x = -SeatDistance;
                    z = 0f;
                    yawDegrees = 90f;
                    break;
                case 3:
                    x = SeatDistance;
                    z = 0f;
                    yawDegrees = 270f;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(seat), "좌석 번호는 0~3 이어야 합니다.");
            }
        }

        /// <summary>수치가 서로 모순되지 않는지 확인한다.</summary>
        public bool IsValid =>
            TarpHalfSize > 0f && SeatDistance > 0f && WalkHalfSize >= SeatDistance && MaxY > MinY &&
            !float.IsNaN(TarpHalfSize) && !float.IsNaN(SeatDistance) && !float.IsNaN(WalkHalfSize) &&
            !float.IsInfinity(WalkHalfSize) && !float.IsNaN(MinY) && !float.IsNaN(MaxY);
    }
}
