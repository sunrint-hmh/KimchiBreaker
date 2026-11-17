using KimchiBreaker.Core;
using KimchiBreaker.Net;
using UnityEngine;

namespace KimchiBreaker.Presentation
{
    /// <summary>
    /// 카메라 연출.
    /// - 메뉴: 경기장 주변을 천천히 돈다.
    /// - 대기실: 김장포대 전체를 비추는 탑뷰.
    /// - 라운드 시작: 탑뷰에서 내 자리의 1인칭 시점으로 줌인.
    /// - 라운드 진행~결과: 1인칭.
    /// - 피날레: 최종 승자를 3인칭으로 클로즈업.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraDirector : MonoBehaviour
    {
        const float k_IntroZoomSeconds = 2.5f;

        static readonly Vector3 s_TopViewPosition = new Vector3(0f, 15f, -6f);
        static readonly Quaternion s_TopViewRotation = Quaternion.Euler(68f, 0f, 0f);

        Camera m_Camera;
        PlayerAvatar m_ThirdPersonTarget;
        float m_MenuAngle;

        public Camera Camera => m_Camera;

        public static CameraDirector Create()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var camera = go.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 200f;
            camera.fieldOfView = 70f;
            go.AddComponent<AudioListener>();
            return go.AddComponent<CameraDirector>();
        }

        void Awake()
        {
            m_Camera = GetComponent<Camera>();
            transform.SetPositionAndRotation(s_TopViewPosition, s_TopViewRotation);
        }

        void LateUpdate()
        {
            MatchNetwork match = MatchNetwork.Instance;
            PlayerAvatar local = PlayerAvatar.Local;
            float deltaTime = Time.unscaledDeltaTime;

            if (match == null || !match.IsSpawned)
            {
                SetThirdPersonTarget(null);
                UpdateMenuOrbit(deltaTime);
                return;
            }

            MatchSnapshot snapshot = match.Snapshot;
            switch (snapshot.Phase)
            {
                case MatchPhase.Lobby:
                    SetThirdPersonTarget(null);
                    MoveTowards(s_TopViewPosition, s_TopViewRotation, deltaTime, 4f);
                    break;

                case MatchPhase.RoundIntro:
                    SetThirdPersonTarget(null);
                    if (local != null)
                    {
                        float elapsed = (float)(match.ServerNow - snapshot.PhaseStartTime);
                        float t = Mathf.Clamp01(elapsed / k_IntroZoomSeconds);
                        t = t * t * (3f - 2f * t); // 부드럽게 가속/감속
                        Transform eye = local.Eye;
                        transform.SetPositionAndRotation(
                            Vector3.Lerp(s_TopViewPosition, eye.position, t),
                            Quaternion.Slerp(s_TopViewRotation, eye.rotation, t));
                    }

                    break;

                case MatchPhase.Finale:
                    if (PlayerAvatar.TryGet(snapshot.MatchWinnerId, out PlayerAvatar winner))
                    {
                        SetThirdPersonTarget(winner);
                        Transform body = winner.transform;
                        Vector3 focus = body.position + Vector3.up * 1.4f;
                        float orbit = (float)(match.ServerNow - snapshot.PhaseStartTime) * 12f;
                        Vector3 offset = Quaternion.Euler(0f, body.eulerAngles.y + 20f + orbit, 0f) * new Vector3(0f, 0.5f, 3f);
                        MoveTowards(focus + offset, Quaternion.LookRotation(focus - (focus + offset)), deltaTime, 3f);
                    }
                    else
                    {
                        SetThirdPersonTarget(null);
                        MoveTowards(s_TopViewPosition, s_TopViewRotation, deltaTime, 4f);
                    }

                    break;

                default:
                    SetThirdPersonTarget(null);
                    if (local != null)
                    {
                        transform.SetPositionAndRotation(local.Eye.position, local.Eye.rotation);
                    }
                    else
                    {
                        MoveTowards(s_TopViewPosition, s_TopViewRotation, deltaTime, 4f);
                    }

                    break;
            }
        }

        void UpdateMenuOrbit(float deltaTime)
        {
            m_MenuAngle = Mathf.Repeat(m_MenuAngle + deltaTime * 6f, 360f);
            Vector3 position = Quaternion.Euler(0f, m_MenuAngle, 0f) * new Vector3(0f, 7f, -11f);
            Quaternion rotation = Quaternion.LookRotation(-position + Vector3.up * 0.5f);
            MoveTowards(position, rotation, deltaTime, 2f);
        }

        void MoveTowards(Vector3 position, Quaternion rotation, float deltaTime, float speed)
        {
            float blend = 1f - Mathf.Exp(-speed * deltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, position, blend),
                Quaternion.Slerp(transform.rotation, rotation, blend));
        }

        /// <summary>피날레 동안 승자 캐릭터가 내 캐릭터여도 전신이 보이게 한다.</summary>
        void SetThirdPersonTarget(PlayerAvatar target)
        {
            if (m_ThirdPersonTarget == target)
            {
                return;
            }

            if (m_ThirdPersonTarget != null)
            {
                m_ThirdPersonTarget.SetThirdPersonView(false);
            }

            m_ThirdPersonTarget = target;
            if (target != null)
            {
                target.SetThirdPersonView(true);
            }
        }
    }
}
