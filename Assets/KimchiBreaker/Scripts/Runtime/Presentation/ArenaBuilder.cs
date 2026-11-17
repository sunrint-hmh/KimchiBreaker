using KimchiBreaker.Net;
using UnityEngine;

namespace KimchiBreaker.Presentation
{
    /// <summary>
    /// [임시] 경기장(바닥, 핑크 김장포대, 주름 테두리 벽, 조명)을 기본 도형으로 만든다.
    /// 모든 피어가 각자 똑같이 만든다(네트워크 오브젝트가 아님). 실제 모델이 준비되면 교체한다.
    /// </summary>
    public static class ArenaBuilder
    {
        static readonly Color s_Floor = new Color(0.55f, 0.55f, 0.5f);
        static readonly Color s_Tarp = new Color(1f, 0.55f, 0.72f);
        static readonly Color s_TarpWall = new Color(0.93f, 0.42f, 0.62f);

        /// <summary>포대 테두리 벽 높이. 재료가 밖으로 튀어 나가지 않고, 사람은 넘어가지 못하는 높이.</summary>
        public const float WallHeight = 0.6f;

        public static GameObject Build()
        {
            var root = new GameObject("Arena");
            float tarpHalf = GameArena.Layout.TarpHalfSize;
            float walkHalf = GameArena.Layout.WalkHalfSize;

            // 바닥: 사람이 서는 곳. 충돌체를 남겨 재료가 떨어져도 받친다.
            RuntimeVisuals.CreatePart(PrimitiveType.Cube, root.transform, "Floor",
                new Vector3(0f, -0.25f, 0f), new Vector3(walkHalf * 2f + 6f, 0.5f, walkHalf * 2f + 6f), s_Floor, keepCollider: true);

            // 핑크 김장포대: 바닥은 평평한 판(물리 판정 안정)
            RuntimeVisuals.CreatePart(PrimitiveType.Cube, root.transform, "Tarp",
                new Vector3(0f, 0.025f, 0f), new Vector3(tarpHalf * 2f, 0.05f, tarpHalf * 2f), s_Tarp, keepCollider: true);

            // 주름 테두리 벽(사방)
            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;
                float sign = side % 2 == 0 ? 1f : -1f;
                Vector3 position = alongX
                    ? new Vector3(0f, WallHeight * 0.5f, sign * tarpHalf)
                    : new Vector3(sign * tarpHalf, WallHeight * 0.5f, 0f);
                Vector3 scale = alongX
                    ? new Vector3(tarpHalf * 2f + 0.3f, WallHeight, 0.3f)
                    : new Vector3(0.3f, WallHeight, tarpHalf * 2f + 0.3f);
                RuntimeVisuals.CreatePart(PrimitiveType.Cube, root.transform, "TarpWall" + side, position, scale, s_TarpWall, keepCollider: true);
            }

            // 보이지 않는 바깥 울타리: 사람이 경기장 밖으로 나가지 않게 한다(서버도 범위를 검증한다).
            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;
                float sign = side % 2 == 0 ? 1f : -1f;
                var fence = new GameObject("Fence" + side);
                fence.transform.SetParent(root.transform, false);
                fence.transform.localPosition = alongX
                    ? new Vector3(0f, 1.5f, sign * (walkHalf + 0.5f))
                    : new Vector3(sign * (walkHalf + 0.5f), 1.5f, 0f);
                var box = fence.AddComponent<BoxCollider>();
                box.size = alongX ? new Vector3(walkHalf * 2f + 2f, 3f, 1f) : new Vector3(1f, 3f, walkHalf * 2f + 2f);
            }

            // 조명
            var lightObject = new GameObject("Sun");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;

            return root;
        }
    }
}
