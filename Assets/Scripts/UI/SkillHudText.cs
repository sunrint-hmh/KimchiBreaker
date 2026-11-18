using UnityEngine;

public static class SkillHudText
{
    public static string Detail(SkillKind kind, SkillCombatState state, float now)
    {
        switch (kind)
        {
            case SkillKind.SpicyHands: return "클릭 피해 +" + state.DamageBonus;
            case SkillKind.BigHands: return $"조준 원 +{(state.AimMultiplier - 1f) * 100f:0.#}%";
            case SkillKind.ChargedGloves:
                float charge = state.ChargeProgress(now);
                return charge >= 1f ? $"충전 완료 · 다음 클릭 {state.ChargeMultiplier:0.#}배"
                    : "충전 중 " + Mathf.Clamp(Mathf.FloorToInt(charge * 100f),0,99) + "%";
            case SkillKind.AncestralTaste:
                if (state.SuccessfulClicks >= state.AncestorInterval) return "추가 타격 대기 · 다른 보이는 재료 필요";
                if (state.AncestorReadyOnNextHit) return "다음 명중 클릭에 추가 타격";
                return "추가 타격까지 명중 클릭 " + (state.AncestorInterval - state.SuccessfulClicks) + "회";
            default: return "";
        }
    }
}
