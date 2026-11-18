using UnityEngine;

public enum SkillKind
{
    SpicyHands,
    BigHands,
    ChargedGloves,
    AncestralTaste
}

/// <summary>Authored skill data. Drawing, granting and applying skills use this definition separately.</summary>
[CreateAssetMenu(menuName = "KimchiBreaker/Skill Definition", fileName = "NewSkill")]
public sealed class SkillDefinition : ScriptableObject
{
    [SerializeField] private SkillKind kind;
    [SerializeField] private string displayName;
    [SerializeField, Min(1)] private int winnerCount = 1;
    [SerializeField] private bool availableInDraw = true;
    [TextArea(2, 4)] [SerializeField] private string descriptionOverride;

    [Header("Spicy Hands")]
    [SerializeField, Min(1)] private int extraClickDamage = 1;
    [Header("Big Hands")]
    [SerializeField, Range(0f, 2f)] private float aimDiameterBonus = .2f;
    [Header("Charged Gloves")]
    [SerializeField, Min(.1f)] private float chargeDelaySeconds = 1f;
    [SerializeField, Min(1f)] private float chargedDamageMultiplier = 2f;
    [Header("Ancestral Taste")]
    [Tooltip("One successful click counts once, even when it hits several ingredients.")]
    [SerializeField, Min(1)] private int successfulClicksPerExtraStrike = 5;
    [SerializeField, Min(.1f)] private float extraStrikeDamageMultiplier = 1f;

    public SkillKind Kind => kind;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public int WinnerCount => Mathf.Max(1, winnerCount);
    public bool AvailableInDraw => availableInDraw;
    public int ExtraClickDamage => Mathf.Max(1, extraClickDamage);
    public float AimDiameterBonus => Mathf.Max(0f, aimDiameterBonus);
    public float ChargeDelaySeconds => Mathf.Max(.1f, chargeDelaySeconds);
    public float ChargedDamageMultiplier => Mathf.Max(1f, chargedDamageMultiplier);
    public int SuccessfulClicksPerExtraStrike => Mathf.Max(1, successfulClicksPerExtraStrike);
    public float ExtraStrikeDamageMultiplier => Mathf.Max(.1f, extraStrikeDamageMultiplier);

    public string Description
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(descriptionOverride)) return descriptionOverride;
            switch (kind)
            {
                case SkillKind.SpicyHands:
                    return $"클릭 데미지가 {ExtraClickDamage} 증가합니다.";
                case SkillKind.BigHands:
                    return $"조준 원의 크기가 {AimDiameterBonus * 100f:0.#}% 증가합니다.";
                case SkillKind.ChargedGloves:
                    return $"{ChargeDelaySeconds:0.#}초 동안 클릭을 쉬면\n다음 클릭의 피해가 {ChargedDamageMultiplier:0.#}배가 됩니다.";
                case SkillKind.AncestralTaste:
                    return $"명중한 클릭 {SuccessfulClicksPerExtraStrike}회마다\n다른 보이는 재료 1개를 추가 타격합니다.";
                default:
                    return "";
            }
        }
    }
}
