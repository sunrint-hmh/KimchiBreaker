using System;
using TMPro;
using UnityEngine;

/// <summary>Read-only local skill HUD. It never grants skills or changes combat state.</summary>
[DisallowMultipleComponent]
public sealed class PlayerSkillHud : MonoBehaviour
{
    [Serializable] public sealed class SkillRow
    {
        public SkillKind kind;
        public GameObject root;
        public TMP_Text nameLabel;
        public TMP_Text detailLabel;
    }
    [SerializeField] private PlayerSkillEffects player;
    [SerializeField] private TMP_Text emptyLabel;
    [SerializeField] private SkillRow[] rows = new SkillRow[4];
    private MatchSkillInventory.OwnedSkill[] owned = Array.Empty<MatchSkillInventory.OwnedSkill>();
    private float nextUpdate;
    private float nextSearch;

    private void OnEnable()
    {
        MatchSkillInventory.Changed += OnInventoryChanged;
        RefreshOwnership();
    }
    private void OnDisable() => MatchSkillInventory.Changed -= OnInventoryChanged;
    private void Start() => FindLocalPlayer();

    public void Bind(PlayerSkillEffects source)
    {
        player = source;
        RefreshOwnership();
    }

    private void FindLocalPlayer()
    {
        if (player != null) return;
        var camera = Camera.main;
        if (camera != null && camera.gameObject.scene == gameObject.scene)
            player = camera.GetComponent<PlayerSkillEffects>();
        if (player == null)
            foreach (var candidate in FindObjectsByType<PlayerSkillEffects>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == gameObject.scene && candidate.isActiveAndEnabled)
                { player = candidate; break; }
        RefreshOwnership();
    }

    private void OnInventoryChanged(int id)
    {
        if (player != null && player.PlayerId == id) RefreshOwnership();
    }

    private void RefreshOwnership()
    {
        owned = player == null ? Array.Empty<MatchSkillInventory.OwnedSkill>() : MatchSkillInventory.GetSkills(player.PlayerId);
        RefreshNow();
    }

    private void LateUpdate()
    {
        if (player == null && Time.unscaledTime >= nextSearch)
        { nextSearch = Time.unscaledTime + 1f; FindLocalPlayer(); }
        if (Time.unscaledTime < nextUpdate) return;
        nextUpdate = Time.unscaledTime + .1f;
        RefreshNow();
    }

    public void RefreshNow()
    {
        if (player == null) owned = Array.Empty<MatchSkillInventory.OwnedSkill>();
        if (emptyLabel != null)
        {
            emptyLabel.gameObject.SetActive(owned.Length == 0);
            emptyLabel.text = player == null ? "플레이어 연결 대기" : "아직 획득한 스킬이 없어요";
        }
        foreach (var row in rows)
        {
            if (row == null || row.root == null) continue;
            MatchSkillInventory.OwnedSkill skill = null;
            foreach (var item in owned)
                if (item.Definition != null && item.Definition.Kind == row.kind) { skill = item; break; }
            row.root.SetActive(skill != null);
            if (skill == null) continue;
            string title = skill.Definition.DisplayName + "  Lv. " + skill.Level;
            if (row.nameLabel != null && row.nameLabel.text != title) row.nameLabel.text = title;
            string detail = SkillHudText.Detail(row.kind,player.State,Time.time);
            if (row.detailLabel != null && row.detailLabel.text != detail) row.detailLabel.text = detail;
            if (row.detailLabel != null) row.detailLabel.color =
                row.kind == SkillKind.ChargedGloves && player.State.ChargeProgress(Time.time) >= 1f
                || row.kind == SkillKind.AncestralTaste && player.State.AncestorReadyOnNextHit
                ? new Color(.72f,1f,.25f) : new Color(.9f,.9f,.8f);
        }
    }
}
