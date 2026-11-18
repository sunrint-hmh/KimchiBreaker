using UnityEngine;

/// <summary>Local prototype: clicks season the ingredient until it becomes one kimchi.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class KimchiIngredient : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHp = 10;
    [SerializeField] private Color seasonedColor = new Color(0.68f, 0.06f, 0.025f);
    [SerializeField] private int currentHp;
    [Header("Hit physics")]
    [SerializeField, Min(0f)] private float pushImpulsePerDamage = 1.1f;
    [SerializeField, Min(0f)] private float liftImpulsePerDamage = 0.18f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private Renderer[] renderers;
    private Color[][] originalColors;
    private MaterialPropertyBlock block;
    private bool completed;
    private Rigidbody body;

    public int CurrentHp => currentHp;
    public int MaxHp => maxHp;

    private void Awake()
    {
        currentHp = Mathf.Max(1, maxHp);
        body = GetComponent<Rigidbody>();
        if (body != null && !body.isKinematic)
        {
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
        block = new MaterialPropertyBlock();
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length][];
        for (int r = 0; r < renderers.Length; r++)
        {
            Material[] materials = renderers[r].sharedMaterials;
            originalColors[r] = new Color[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                originalColors[r][i] = material != null && material.HasProperty(BaseColorId)
                    ? material.GetColor(BaseColorId)
                    : material != null && material.HasProperty(ColorId)
                        ? material.GetColor(ColorId) : Color.white;
            }
        }
    }

    // Returns true only for the hit that finishes this ingredient.
    public bool ApplyHit(int damage, Vector3 attackerPosition, Vector3 hitPoint)
    {
        if (completed || damage <= 0) return false;
        ApplyKnockback(damage, attackerPosition, hitPoint);
        currentHp = Mathf.Max(0, currentHp - damage);
        float progress = 1f - (float)currentHp / Mathf.Max(1, maxHp);
        for (int r = 0; r < renderers.Length; r++)
        {
            if (renderers[r] == null) continue;
            for (int i = 0; i < originalColors[r].Length; i++)
            {
                block.Clear();
                renderers[r].GetPropertyBlock(block, i);
                Color color = Color.Lerp(originalColors[r][i], seasonedColor, progress);
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                renderers[r].SetPropertyBlock(block, i);
            }
        }
        Debug.Log($"[Kimjang] {name}: HP {currentHp}/{maxHp}", this);
        if (currentHp != 0) return false;
        completed = true;
        // Hide immediately so another hit in the same frame cannot score twice.
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }

    private void ApplyKnockback(int damage, Vector3 attackerPosition, Vector3 hitPoint)
    {
        if (body == null || body.isKinematic) return;
        // Camera tilt should not drive the ingredient down into the mat.
        Vector3 away = Vector3.ProjectOnPlane(body.worldCenterOfMass - attackerPosition, Vector3.up);
        if (away.sqrMagnitude < 0.0001f) return;
        Vector3 impulse = (away.normalized * pushImpulsePerDamage
            + Vector3.up * liftImpulsePerDamage) * damage;
        body.WakeUp();
        // An off-center hit produces real torque: no canned movement or random spin.
        body.AddForceAtPosition(impulse, hitPoint, ForceMode.Impulse);
    }
}
