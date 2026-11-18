using UnityEngine;

/// <summary>Displays the four authored offers; it does not grant or execute skills.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DrawTicketAllocator))]
public sealed class DrawSkillPresenter : MonoBehaviour
{
    [SerializeField] private SkillDefinition[] skills = new SkillDefinition[4];
    [SerializeField] private TMPro.TMP_Text[] nameLabels = new TMPro.TMP_Text[4];
    [SerializeField] private TMPro.TMP_Text[] descriptionLabels = new TMPro.TMP_Text[4];

    public SkillDefinition GetSkill(int index) => skills != null && index >= 0 && index < skills.Length ? skills[index] : null;

    public void ShowResult(int index, string result)
    {
        var skill = GetSkill(index);
        var label = descriptionLabels != null && index >= 0 && index < descriptionLabels.Length ? descriptionLabels[index] : null;
        if (label != null) label.text = (skill == null ? "" : skill.Description + "\n") + result;
    }

    private void Start()
    {
        if (!RefreshSkillLabels())
            Debug.LogWarning("[Draw] Connect all four skill definitions, name labels and description labels on DrawSkillPresenter.", this);
    }

    private void Reset()
    {
        skills = new SkillDefinition[4];
        nameLabels = new TMPro.TMP_Text[4];
        descriptionLabels = new TMPro.TMP_Text[4];
#if UNITY_EDITOR
        string[] files = { "SpicyHands", "BigHands", "ChargedGloves", "AncestralTaste" };
        for (int i = 0; i < 4; i++)
            skills[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/Skills/" + files[i] + ".asset");
#endif
        AutoConnectLabels();
        RefreshSkillLabels();
    }

    [ContextMenu("Auto Connect Labels")]
    public void AutoConnectLabels()
    {
        var allocator = GetComponent<DrawTicketAllocator>();
        if (allocator == null) return;
        if (nameLabels == null || nameLabels.Length != 4) nameLabels = new TMPro.TMP_Text[4];
        if (descriptionLabels == null || descriptionLabels.Length != 4) descriptionLabels = new TMPro.TMP_Text[4];
        for (int i = 0; i < 4; i++)
        {
            var count = allocator.GetAllocationLabel(i);
            if (count == null || count.transform.parent == null) continue;
            var parent = count.transform.parent;
            if (nameLabels[i] == null)
                nameLabels[i] = parent.Find("Jar" + (i + 1) + "SkillName")?.GetComponent<TMPro.TMP_Text>();
            if (descriptionLabels[i] == null)
                descriptionLabels[i] = parent.Find("Jar" + (i + 1) + "SkillDescription")?.GetComponent<TMPro.TMP_Text>();
        }
    }

    [ContextMenu("Refresh Skill Labels")]
    private void RefreshFromMenu() => RefreshSkillLabels();

    public bool RefreshSkillLabels()
    {
        bool complete = true;
        for (int i = 0; i < 4; i++)
        {
            var skill = GetSkill(i);
            var title = nameLabels != null && i < nameLabels.Length ? nameLabels[i] : null;
            var description = descriptionLabels != null && i < descriptionLabels.Length ? descriptionLabels[i] : null;
            if (skill == null || title == null || description == null) { complete = false; continue; }
            title.text = skill.DisplayName;
            description.text = skill.Description + $"\n당첨 {skill.WinnerCount}명";
            title.raycastTarget = description.raycastTarget = false;
        }
        return complete;
    }
}
