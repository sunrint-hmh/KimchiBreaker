using UnityEngine;

namespace KimchiBreaker.Presentation
{
    /// <summary>
    /// 임시 그래픽(기본 도형)을 코드로 만드는 도우미. 실제 모델/머티리얼이 준비되면 교체한다.
    /// 색은 MaterialPropertyBlock 으로 바꿔서 머티리얼을 새로 만들지 않는다(메모리 누수 방지).
    /// 기본 도형의 기본 머티리얼은 렌더 파이프라인이 항상 빌드에 포함하므로 셰이더가 빠질 걱정이 없다.
    /// </summary>
    public static class RuntimeVisuals
    {
        static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int s_Color = Shader.PropertyToID("_Color");

        /// <summary>
        /// 기본 도형을 만들어 <paramref name="parent"/> 아래에 붙인다.
        /// </summary>
        /// <param name="keepCollider">false 면 도형에 딸린 충돌체를 제거한다(보이기만 하는 장식).</param>
        public static Renderer CreatePart(
            PrimitiveType type,
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            bool keepCollider = false)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            if (!keepCollider && part.TryGetComponent(out Collider collider))
            {
                Object.Destroy(collider);
            }

            Transform t = part.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = Quaternion.identity;
            t.localScale = localScale;
            var renderer = part.GetComponent<Renderer>();
            SetColor(renderer, color);
            return renderer;
        }

        public static void SetColor(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(s_BaseColor, color);
            block.SetColor(s_Color, color);
            renderer.SetPropertyBlock(block);
        }

        public static void SetVisible(Renderer renderer, bool visible)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }
}
