using UnityEngine;

/// <summary>Skill calculations and click counters; does not deal damage or award kills.</summary>
public sealed class SkillCombatState
{
    public int DamageBonus { get; private set; }
    public float AimMultiplier { get; private set; } = 1f;
    public bool HasCharge { get; private set; }
    public float ChargeDelay { get; private set; } = 1f;
    public float ChargeMultiplier { get; private set; } = 1f;
    public bool HasAncestor { get; private set; }
    public int AncestorInterval { get; private set; } = 5;
    public float AncestorDamageMultiplier { get; private set; } = 1f;
    public int SuccessfulClicks { get; private set; }
    public bool AncestorReadyOnNextHit => HasAncestor && SuccessfulClicks + 1 >= AncestorInterval;
    private float lastClickAt;

    public void Configure(MatchSkillInventory.OwnedSkill[] skills, float now)
    {
        DamageBonus = 0; AimMultiplier = 1f;
        HasCharge = HasAncestor = false;
        ChargeDelay = 1f; ChargeMultiplier = AncestorDamageMultiplier = 1f;
        AncestorInterval = 5; SuccessfulClicks = 0; lastClickAt = now;
        foreach (var owned in skills)
        {
            var skill = owned.Definition;
            if (skill == null) continue;
            int level = owned.Level;
            switch (skill.Kind)
            {
                case SkillKind.SpicyHands: DamageBonus = skill.ExtraClickDamage * level; break;
                case SkillKind.BigHands: AimMultiplier = 1f + skill.AimDiameterBonus * level; break;
                case SkillKind.ChargedGloves:
                    HasCharge = true;
                    ChargeDelay = skill.ChargeDelaySeconds;
                    ChargeMultiplier = 1f + (skill.ChargedDamageMultiplier - 1f) * level;
                    break;
                case SkillKind.AncestralTaste:
                    HasAncestor = true;
                    AncestorInterval = skill.SuccessfulClicksPerExtraStrike;
                    AncestorDamageMultiplier = skill.ExtraStrikeDamageMultiplier * level;
                    break;
            }
        }
    }

    public float ChargeProgress(float now) => HasCharge ? Mathf.Clamp01((now - lastClickAt) / ChargeDelay) : 0f;

    public int BeginClick(int baseDamage, float now, out bool charged)
    {
        // Mono can retain intermediate precision; tolerate sub-frame float rounding at the deadline.
        charged = HasCharge && now + .00001f >= lastClickAt + ChargeDelay;
        lastClickAt = now; // A missed click also consumes the prepared attack.
        return Mathf.Max(1, Mathf.RoundToInt((Mathf.Max(1, baseDamage) + DamageBonus) * (charged ? ChargeMultiplier : 1f)));
    }

    public bool RecordSuccessfulClick()
    {
        if (!HasAncestor) return false;
        SuccessfulClicks = Mathf.Min(AncestorInterval, SuccessfulClicks + 1);
        return SuccessfulClicks >= AncestorInterval;
    }

    public void ConsumeAncestorStrike() => SuccessfulClicks = 0;
    public int GetAncestorDamage(int baseDamage) => Mathf.Max(1,
        Mathf.RoundToInt((Mathf.Max(1, baseDamage) + DamageBonus) * AncestorDamageMultiplier));
}
