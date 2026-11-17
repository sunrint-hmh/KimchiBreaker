using KimchiBreaker.Core;
using UnityEngine;

namespace KimchiBreaker.Gameplay
{
    /// <summary>서버가 검증을 마친 타격 한 번의 정보.</summary>
    public readonly struct HitContext
    {
        /// <summary>때린 플레이어.</summary>
        public readonly ulong AttackerClientId;

        /// <summary>맞은 지점(월드 좌표, 서버에서 보정됨).</summary>
        public readonly Vector3 Point;

        /// <summary>때린 사람 눈에서 맞은 지점으로 향하는 단위 벡터.</summary>
        public readonly Vector3 Direction;

        /// <summary>때린 사람이 이번 라운드에 가진 스킬(패시브 효과 계산용).</summary>
        public readonly SkillId AttackerSkill;

        public HitContext(ulong attackerClientId, Vector3 point, Vector3 direction, SkillId attackerSkill)
        {
            AttackerClientId = attackerClientId;
            Point = point;
            Direction = direction;
            AttackerSkill = attackerSkill;
        }
    }

    /// <summary>
    /// 플레이어가 클릭으로 때릴 수 있는 네트워크 오브젝트(김장 재료 등).
    /// NetworkObject 가 붙은 오브젝트의 NetworkBehaviour 에 구현한다.
    ///
    /// 흐름: 클라이언트 클릭 → 서버가 거리/시야각/빈도 검증 → <see cref="ServerApplyHit"/> 호출(서버에서만).
    /// </summary>
    public interface IKimchiHittable
    {
        /// <summary>지금 맞을 수 있는 상태인지(이미 완성되어 사라지는 중이면 false).</summary>
        bool CanBeHit { get; }

        /// <summary>판정 중심(월드 좌표).</summary>
        Vector3 HitCenter { get; }

        /// <summary>판정 반지름(미터).</summary>
        float HitRadius { get; }

        /// <summary>[서버 전용] 검증된 타격을 적용한다.</summary>
        void ServerApplyHit(in HitContext hit);
    }
}
