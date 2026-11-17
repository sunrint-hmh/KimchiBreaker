using System;
using UnityEngine;

namespace KimchiBreaker.Presentation
{
    /// <summary>
    /// [임시] 플레이어 캐릭터 겉모습. 캡슐 몸통 + 큰 머리 + 빨간 고무장갑 두 손.
    /// 남현 님의 실제 모델이 준비되면 이 클래스만 교체하면 된다(고개 각도와 타격 동작만 받으면 된다).
    /// </summary>
    public sealed class AvatarVisuals : IDisposable
    {
        static readonly Color[] s_SeatColors =
        {
            new Color(0.95f, 0.45f, 0.55f),
            new Color(0.35f, 0.65f, 0.95f),
            new Color(0.45f, 0.85f, 0.45f),
            new Color(0.98f, 0.78f, 0.3f),
        };

        static readonly Color s_Glove = new Color(0.86f, 0.1f, 0.12f);
        static readonly Color s_Skin = new Color(1f, 0.86f, 0.72f);

        readonly GameObject m_Root;
        readonly Transform m_Head;
        readonly Transform m_LeftHand;
        readonly Transform m_RightHand;
        readonly Renderer m_Body;
        readonly Renderer m_HeadRenderer;
        readonly Vector3 m_LeftRest = new Vector3(-0.32f, -0.32f, 0.45f);
        readonly Vector3 m_RightRest = new Vector3(0.32f, -0.32f, 0.45f);

        float m_LeftPunch;
        float m_RightPunch;
        bool m_NextIsRight = true;

        public AvatarVisuals(Transform owner)
        {
            m_Root = new GameObject("Visuals");
            m_Root.transform.SetParent(owner, false);

            m_Body = RuntimeVisuals.CreatePart(PrimitiveType.Capsule, m_Root.transform, "Body", new Vector3(0f, 0.75f, 0f), new Vector3(0.7f, 0.75f, 0.7f), s_SeatColors[0]);

            var head = new GameObject("Head");
            m_Head = head.transform;
            m_Head.SetParent(m_Root.transform, false);
            m_Head.localPosition = new Vector3(0f, 1.6f, 0f);
            m_HeadRenderer = RuntimeVisuals.CreatePart(PrimitiveType.Sphere, m_Head, "HeadMesh", Vector3.zero, Vector3.one * 0.5f, s_Skin);
            RuntimeVisuals.CreatePart(PrimitiveType.Cube, m_Head, "Nose", new Vector3(0f, 0f, 0.26f), new Vector3(0.08f, 0.08f, 0.1f), s_Skin);

            m_LeftHand = RuntimeVisuals.CreatePart(PrimitiveType.Sphere, m_Head, "LeftGlove", m_LeftRest, new Vector3(0.18f, 0.14f, 0.24f), s_Glove).transform;
            m_RightHand = RuntimeVisuals.CreatePart(PrimitiveType.Sphere, m_Head, "RightGlove", m_RightRest, new Vector3(0.18f, 0.14f, 0.24f), s_Glove).transform;

            var eye = new GameObject("Eye");
            Eye = eye.transform;
            Eye.SetParent(m_Head, false);
            Eye.localPosition = new Vector3(0f, 0f, 0.05f);
        }

        /// <summary>1인칭 카메라가 붙을 위치.</summary>
        public Transform Eye { get; }

        /// <summary>현재 고개 각도(도, 양수면 아래).</summary>
        public float Pitch { get; private set; }

        /// <summary>내 캐릭터면 몸통과 머리를 숨겨 시야를 가리지 않게 한다(손은 보인다).</summary>
        public void SetFirstPerson(bool firstPerson)
        {
            RuntimeVisuals.SetVisible(m_Body, !firstPerson);
            foreach (Renderer renderer in m_Head.GetComponentsInChildren<Renderer>())
            {
                if (renderer.transform != m_LeftHand && renderer.transform != m_RightHand)
                {
                    RuntimeVisuals.SetVisible(renderer, !firstPerson);
                }
            }
        }

        /// <summary>3인칭 연출(피날레)에서 다시 전신을 보이게 한다.</summary>
        public void ShowFullBody() => SetFirstPerson(false);

        public void SetSeatColor(int seat)
        {
            RuntimeVisuals.SetColor(m_Body, s_SeatColors[Mathf.Clamp(seat, 0, s_SeatColors.Length - 1)]);
        }

        public static Color SeatColor(int seat) => s_SeatColors[Mathf.Clamp(seat, 0, s_SeatColors.Length - 1)];

        public void SetLook(float pitch)
        {
            Pitch = float.IsNaN(pitch) ? 0f : pitch;
            m_Head.localRotation = Quaternion.Euler(Pitch, 0f, 0f);
        }

        /// <summary>손으로 찰싹 때리는 동작(양손 번갈아).</summary>
        public void PlayHit()
        {
            if (m_NextIsRight)
            {
                m_RightPunch = 1f;
            }
            else
            {
                m_LeftPunch = 1f;
            }

            m_NextIsRight = !m_NextIsRight;
        }

        public void Tick(float deltaTime)
        {
            m_LeftPunch = Mathf.MoveTowards(m_LeftPunch, 0f, deltaTime * 8f);
            m_RightPunch = Mathf.MoveTowards(m_RightPunch, 0f, deltaTime * 8f);
            m_LeftHand.localPosition = m_LeftRest + new Vector3(0.12f, -0.05f, 0.35f) * Mathf.Sin(m_LeftPunch * Mathf.PI);
            m_RightHand.localPosition = m_RightRest + new Vector3(-0.12f, -0.05f, 0.35f) * Mathf.Sin(m_RightPunch * Mathf.PI);
        }

        public void Dispose()
        {
            if (m_Root != null)
            {
                UnityEngine.Object.Destroy(m_Root);
            }
        }
    }
}
