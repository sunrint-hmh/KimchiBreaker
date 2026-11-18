using UnityEngine;

/// <summary>Reads this player's current-match awards and exposes their in-game effects.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(IngredientClicker))]
public sealed class PlayerSkillEffects : MonoBehaviour
{
    [SerializeField] private int playerId;
    [SerializeField] private Color chargedRingColor = new Color(1f, .64f, .15f, 1f);
    private readonly SkillCombatState state = new SkillCombatState();
    private CrosshairSize crosshair;
    private UnityEngine.UI.Image ringImage;
    private Color originalRingColor;
    private float originalMultiplier = 1f;
    private bool captured;

    public int PlayerId => playerId;
    public SkillCombatState State => state;

    private void OnEnable()
    {
        var aimCircle = GetComponent<IngredientClicker>().AimCircle;
        crosshair = aimCircle == null ? null : aimCircle.GetComponent<CrosshairSize>();
        ringImage = aimCircle == null ? null : aimCircle.GetComponent<UnityEngine.UI.Image>();
        if (crosshair != null) originalMultiplier = crosshair.SkillMultiplier;
        if (ringImage != null) originalRingColor = ringImage.color;
        captured = true;
        MatchSkillInventory.Changed += OnSkillChanged;
        ApplyOwnedSkills();
    }

    private void OnSkillChanged(int changedPlayer) { if (changedPlayer == playerId) ApplyOwnedSkills(); }

    public void ApplyOwnedSkills()
    {
        state.Configure(MatchSkillInventory.GetSkills(playerId), Time.time);
        if (crosshair != null) crosshair.SetSkillMultiplier(originalMultiplier * state.AimMultiplier);
    }

    private void Update()
    {
        if (ringImage != null)
            ringImage.color = state.HasCharge ? Color.Lerp(originalRingColor, chargedRingColor,
                Mathf.Pow(state.ChargeProgress(Time.time), 2f)) : originalRingColor;
    }

    private void OnDisable()
    {
        MatchSkillInventory.Changed -= OnSkillChanged;
        if (!captured) return;
        if (crosshair != null) crosshair.SetSkillMultiplier(originalMultiplier);
        if (ringImage != null) ringImage.color = originalRingColor;
    }

#if UNITY_EDITOR
    [ContextMenu("Editor: Grant All Four Test Skills")]
    private void GrantAllEditorTestSkills()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Skills] Enter Play mode before granting temporary test skills.", this);
            return;
        }
        foreach (string file in new[] { "SpicyHands", "BigHands", "ChargedGloves", "AncestralTaste" })
        {
            var skill = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/Skills/" + file + ".asset");
            if (skill != null && MatchSkillInventory.GetLevel(playerId, skill.Kind) == 0) MatchSkillInventory.Grant(playerId, skill);
        }
        Debug.Log("[Skills] EDITOR TEST: four temporary skills granted for this Play session.", this);
    }
#endif
}
