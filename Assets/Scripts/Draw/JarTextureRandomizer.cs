using UnityEngine;

/// <summary>Varies dirty-glass patterns per jar without duplicating or editing shared materials.</summary>
[DisallowMultipleComponent]
public sealed class JarTextureRandomizer : MonoBehaviour
{
    [SerializeField, Range(.5f, 2f)] private float minimumPatternScale = .85f;
    [SerializeField, Range(.5f, 2f)] private float maximumPatternScale = 1.2f;
    [SerializeField] private bool allowHorizontalMirror = true;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    private MaterialPropertyBlock propertyBlock;

    private void OnEnable() => RandomizeTexture();

    [ContextMenu("Randomize Texture Preview")]
    public void RandomizeTexture()
    {
        // Keep cosmetic randomness separate from gameplay and raffle randomness.
        var random = new System.Random(System.Guid.NewGuid().GetHashCode());
        float minimum = Mathf.Clamp(Mathf.Min(minimumPatternScale, maximumPatternScale), .5f, 2f);
        float maximum = Mathf.Clamp(Mathf.Max(minimumPatternScale, maximumPatternScale), minimum, 2f);
        float scaleX = Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        float scaleY = Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        if (allowHorizontalMirror && random.Next(2) == 0) scaleX = -scaleX;
        float offsetX = (float)random.NextDouble();
        float offsetY = (float)random.NextDouble();
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

        foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (int slot = 0; slot < materials.Length; slot++)
            {
                var material = materials[slot];
                if (material == null || !material.HasProperty(BaseMapId)) continue;
                var originalScale = material.GetTextureScale("_BaseMap");
                var originalOffset = material.GetTextureOffset("_BaseMap");
                propertyBlock.Clear();
                renderer.GetPropertyBlock(propertyBlock, slot);
                // URP Lit samples color, alpha, and metallic/smoothness using this same UV.
                propertyBlock.SetVector(BaseMapStId, new Vector4(
                    originalScale.x * scaleX, originalScale.y * scaleY,
                    originalOffset.x + offsetX, originalOffset.y + offsetY));
                renderer.SetPropertyBlock(propertyBlock, slot);
            }
        }
    }
}
