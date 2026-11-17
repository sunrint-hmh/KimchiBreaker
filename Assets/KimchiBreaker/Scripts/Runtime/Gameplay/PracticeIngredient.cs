using KimchiBreaker.Core;
using KimchiBreaker.Net;
using KimchiBreaker.Presentation;
using Unity.Netcode;
using UnityEngine;

namespace KimchiBreaker.Gameplay
{
    /// <summary>
    /// [임시] 네트워크 동작 검증용 연습 재료.
    /// 남현 님의 실제 김장 재료(배추/무, 단계별 변색, 타격감)가 준비되면 이 클래스를 대체한다.
    /// 서버만 체력을 알고, 클라이언트에는 양념 진행도(0~255)만 보낸다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PracticeIngredient : NetworkBehaviour, IKimchiHittable
    {
        const float k_Radius = 0.35f;
        const float k_MaxHealth = 10f;
        const float k_HitImpulse = 1.2f;

        static readonly Color s_Raw = new Color(0.92f, 0.96f, 0.82f);
        static readonly Color s_Seasoned = new Color(0.97f, 0.62f, 0.66f);
        static readonly Color s_Kimchi = new Color(0.78f, 0.08f, 0.06f);
        static readonly Color s_Gold = new Color(1f, 0.8f, 0.2f);

        readonly NetworkVariable<byte> m_Seasoning = new NetworkVariable<byte>();
        readonly NetworkVariable<byte> m_KimchiValue = new NetworkVariable<byte>(1);

        Rigidbody m_Body;
        Renderer m_Renderer;
        float m_Health = k_MaxHealth;
        bool m_Consumed;

        public bool CanBeHit => IsSpawned && !m_Consumed;

        public Vector3 HitCenter => transform.position;

        public float HitRadius => k_Radius;

        /// <summary>양념 진행도(0~1). 모든 피어에서 읽을 수 있다.</summary>
        public float Seasoning => m_Seasoning.Value / 255f;

        void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
            m_Renderer = RuntimeVisuals.CreatePart(PrimitiveType.Sphere, transform, "Visual", Vector3.zero, Vector3.one * (k_Radius * 2f), s_Raw);
        }

        public override void OnNetworkSpawn()
        {
            m_Seasoning.OnValueChanged += HandleVisualChanged;
            m_KimchiValue.OnValueChanged += HandleVisualChanged;
            RefreshColor();
        }

        public override void OnNetworkDespawn()
        {
            m_Seasoning.OnValueChanged -= HandleVisualChanged;
            m_KimchiValue.OnValueChanged -= HandleVisualChanged;
        }

        /// <summary>[서버 전용] 생성 직후 값을 정한다(스폰 전에 호출).</summary>
        public void ServerConfigure(int kimchiValue)
        {
            m_KimchiValue.Value = (byte)Mathf.Clamp(kimchiValue, 1, GameRules.MaxKimchiPerAward);
        }

        public void ServerApplyHit(in HitContext hit)
        {
            if (!IsServer || m_Consumed)
            {
                return;
            }

            float damage = hit.AttackerSkill == SkillId.FlameGloves ? 1.75f : 1f;
            m_Health = Mathf.Max(0f, m_Health - damage);
            m_Seasoning.Value = (byte)Mathf.RoundToInt(255f * (1f - m_Health / k_MaxHealth));

            if (m_Body != null && !m_Body.isKinematic)
            {
                Vector3 push = hit.Direction.sqrMagnitude > 0.0001f ? hit.Direction.normalized : Vector3.up;
                m_Body.AddForceAtPosition((push + Vector3.up * 0.6f) * k_HitImpulse, hit.Point, ForceMode.Impulse);
            }

            if (m_Health <= 0f)
            {
                m_Consumed = true; // 같은 프레임에 들어온 다른 막타는 무시한다
                MatchNetwork match = MatchNetwork.Instance;
                if (match != null)
                {
                    match.ServerAwardKimchi(hit.AttackerClientId, m_KimchiValue.Value);
                }

                NetworkObject.Despawn(true);
            }
        }

        void FixedUpdate()
        {
            // 포대 밖으로 떨어진 재료는 서버가 정리한다.
            if (IsServer && IsSpawned && !m_Consumed && transform.position.y < -5f)
            {
                m_Consumed = true;
                NetworkObject.Despawn(true);
            }
        }

        void HandleVisualChanged(byte previous, byte current) => RefreshColor();

        void RefreshColor()
        {
            float t = m_Seasoning.Value / 255f;
            Color color = t < 0.5f ? Color.Lerp(s_Raw, s_Seasoned, t * 2f) : Color.Lerp(s_Seasoned, s_Kimchi, (t - 0.5f) * 2f);
            if (m_KimchiValue.Value > 1)
            {
                color = Color.Lerp(color, s_Gold, 0.5f);
            }

            RuntimeVisuals.SetColor(m_Renderer, color);
        }
    }
}
