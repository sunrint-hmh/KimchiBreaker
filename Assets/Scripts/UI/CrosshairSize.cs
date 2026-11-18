using UnityEngine;

/// <summary>Visual diameter in Canvas reference pixels; skills can set a total multiplier.</summary>
[ExecuteAlways, DisallowMultipleComponent]
[RequireComponent(typeof(UnityEngine.UI.Image))]
public sealed class CrosshairSize : MonoBehaviour
{
    [SerializeField, Min(1f)] private float baseDiameter = 8f;
    [SerializeField, Range(0.1f, 10f)] private float skillMultiplier = 1f;

    public float Diameter => baseDiameter * skillMultiplier;
    public float SkillMultiplier => skillMultiplier;

    public void SetSkillMultiplier(float multiplier)
    {
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier)) return;
        skillMultiplier = Mathf.Clamp(multiplier, 0.1f, 10f);
        ApplySize();
    }

    private void OnEnable()
    {
        var image = GetComponent<UnityEngine.UI.Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        ApplySize();
    }

    private void OnValidate()
    {
        baseDiameter = Mathf.Max(1f, baseDiameter);
        skillMultiplier = Mathf.Clamp(skillMultiplier, 0.1f, 10f);
        ApplySize();
    }

    private void ApplySize()
    {
        var rect = (RectTransform)transform;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Diameter);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Diameter);
    }
}
