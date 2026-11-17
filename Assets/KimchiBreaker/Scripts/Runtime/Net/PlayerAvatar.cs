using System;
using System.Collections.Generic;
using KimchiBreaker.Core;
using KimchiBreaker.Gameplay;
using KimchiBreaker.Presentation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KimchiBreaker.Net
{
    /// <summary>네트워크로 보내는 자세(발 위치 + 몸 방향 + 고개 각도).</summary>
    public struct NetPose : INetworkSerializeByMemcpy, IEquatable<NetPose>
    {
        public Vector3 Position;
        public float Yaw;
        public float Pitch;

        public NetPose(Vector3 position, float yaw, float pitch)
        {
            Position = position;
            Yaw = yaw;
            Pitch = pitch;
        }

        public PoseSample ToSample() => new PoseSample { X = Position.x, Y = Position.y, Z = Position.z, Yaw = Yaw, Pitch = Pitch };

        public static NetPose FromSample(PoseSample sample) => new NetPose(new Vector3(sample.X, sample.Y, sample.Z), sample.Yaw, sample.Pitch);

        public bool Equals(NetPose other) => Position == other.Position && Yaw.Equals(other.Yaw) && Pitch.Equals(other.Pitch);

        public override bool Equals(object obj) => obj is NetPose other && Equals(other);

        public override int GetHashCode() => Position.GetHashCode() ^ Yaw.GetHashCode() ^ (Pitch.GetHashCode() << 2);
    }

    /// <summary>
    /// 플레이어 캐릭터. 접속한 클라이언트마다 하나씩 서버가 만들고, 그 클라이언트가 소유한다.
    ///
    /// 이동 방식: 소유자가 직접 움직이고(반응 지연 없음) 자세를 서버에 보낸다. 서버는 속도/범위/단계를 검증해
    /// 인정한 자세만 다른 플레이어에게 퍼뜨리고, 어긋나면 소유자를 인정된 위치로 되돌린다.
    /// 타격: 소유자가 클릭한 대상을 서버에 알리면, 서버가 거리/시야각/빈도를 검증한 뒤 적용한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerAvatar : NetworkBehaviour
    {
        /// <summary>걷기 속도(미터/초).</summary>
        public const float WalkSpeed = 4f;

        /// <summary>서버가 허용하는 최대 속도. 프레임 오차를 감안해 걷기 속도보다 약간 크다.</summary>
        const float k_ServerMaxSpeed = WalkSpeed * 1.1f;

        const float k_SendInterval = 1f / 30f;
        const float k_KeepAliveInterval = 1f;
        const float k_ClickRange = HitGeometry.MaxReach;

        // ------------------------------------------------------------------ 공개 API

        /// <summary>이 PC 의 플레이어 캐릭터. 없으면 null.</summary>
        public static PlayerAvatar Local { get; private set; }

        /// <summary>내가 클릭으로 무언가를 때렸을 때(서버 확인 전, 즉시) 호출된다. 타격음/파티클 등 즉시 반응용.</summary>
        public static event Action<PlayerAvatar, Vector3, IKimchiHittable> LocalHitPerformed;

        /// <summary>이 캐릭터가 타격 동작을 할 때마다 호출된다(모든 피어). 손 동작/효과용.</summary>
        public event Action HitAnimationPlayed;

        /// <summary>시점 카메라가 붙을 위치(눈).</summary>
        public Transform Eye => m_Visuals != null ? m_Visuals.Eye : transform;

        /// <summary>서버가 인정한 최신 자세.</summary>
        public NetPose ServerPose => m_Pose.Value;

        public static bool TryGet(ulong clientId, out PlayerAvatar avatar) => s_Avatars.TryGetValue(clientId, out avatar) && avatar != null;

        public static IEnumerable<PlayerAvatar> All => s_Avatars.Values;

        // ------------------------------------------------------------------ 서버 생성

        /// <summary>[서버 전용] 플레이어 캐릭터를 좌석에 만들고 해당 클라이언트 소유로 생성한다.</summary>
        internal static void ServerSpawn(NetworkManager networkManager, ulong clientId, int seat)
        {
            GameObject prefab = NetworkPrefabCatalog.PlayerAvatar;
            if (prefab == null)
            {
                Debug.LogError("[KimchiBreaker] 플레이어 프리팹이 없어 캐릭터를 만들 수 없습니다.");
                return;
            }

            GameObject instance = Instantiate(prefab, GameArena.SeatPosition(seat), GameArena.SeatRotation(seat));
            var avatar = instance.GetComponent<PlayerAvatar>();
            avatar.m_InitialSeat = seat;
            var networkObject = instance.GetComponent<NetworkObject>();
            try
            {
                networkObject.SpawnAsPlayerObject(clientId, true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Destroy(instance);
            }
        }

        /// <summary>[서버 전용] 캐릭터를 좌석으로 되돌리고 소유자 화면도 맞춘다.</summary>
        internal void ServerPlaceAtSeat(int seat)
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            var pose = new NetPose(GameArena.SeatPosition(seat), GameArena.SeatYaw(seat), 10f);
            m_Validator.Reset(pose.ToSample(), Time.realtimeSinceStartupAsDouble);
            m_Pose.Value = NetPose.FromSample(m_Validator.Last);
            SendCorrection(m_Pose.Value, true);
        }

        // ------------------------------------------------------------------ 내부 상태

        static readonly Dictionary<ulong, PlayerAvatar> s_Avatars = new Dictionary<ulong, PlayerAvatar>();

        readonly NetworkVariable<NetPose> m_Pose = new NetworkVariable<NetPose>();

        CharacterController m_Controller;
        AvatarVisuals m_Visuals;
        int m_InitialSeat;

        // 서버
        MovementValidator m_Validator;
        uint m_CorrectionId;

        // 소유자
        float m_Yaw;
        float m_Pitch;
        uint m_AckedCorrectionId;
        bool m_HasAuthorityPose;
        float m_SendTimer;
        float m_KeepAliveTimer;
        NetPose m_LastSent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Avatars.Clear();
            Local = null;
            LocalHitPerformed = null;
        }

        void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
            m_Visuals = new AvatarVisuals(transform);
        }

        public override void OnNetworkSpawn()
        {
            s_Avatars[OwnerClientId] = this;
            m_Visuals.SetFirstPerson(IsOwner);
            m_Visuals.SetSeatColor(m_InitialSeat);

            if (IsServer)
            {
                m_Validator = new MovementValidator(GameArena.Layout, k_ServerMaxSpeed);
                MatchNetwork match = MatchNetwork.Instance;
                int seat = match != null ? match.ServerGetSeat(OwnerClientId) : m_InitialSeat;
                ServerPlaceAtSeat(seat == GameRules.NoSeat ? m_InitialSeat : seat);
            }

            if (IsOwner)
            {
                Local = this;
                m_Yaw = transform.eulerAngles.y;
                m_Pitch = 10f;
            }
            else
            {
                // 다른 사람 캐릭터는 받은 자세로 바로 맞춘다.
                SnapTo(m_Pose.Value);
            }

        }

        public override void OnNetworkDespawn()
        {
            if (TryGet(OwnerClientId, out PlayerAvatar registered) && registered == this)
            {
                s_Avatars.Remove(OwnerClientId);
            }

            if (Local == this)
            {
                Local = null;
            }
        }

        public override void OnDestroy()
        {
            if (Local == this)
            {
                Local = null;
            }

            m_Visuals?.Dispose();
            base.OnDestroy();
        }

        void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsOwner)
            {
                UpdateOwner(Time.deltaTime);
            }
            else
            {
                UpdateRemote(Time.deltaTime);
            }

            m_Visuals.Tick(Time.deltaTime);
        }

        // ------------------------------------------------------------------ 소유자 처리

        void UpdateOwner(float deltaTime)
        {
            MatchNetwork match = MatchNetwork.Instance;
            MatchPhase phase = match != null ? match.Snapshot.Phase : MatchPhase.Lobby;
            bool inputActive = LocalControl.GameplayInputActive;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (inputActive && mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * LocalControl.MouseSensitivity;
                if (!float.IsNaN(delta.x) && !float.IsNaN(delta.y))
                {
                    m_Yaw = MovementValidator.NormalizeYaw(m_Yaw + delta.x);
                    m_Pitch = Mathf.Clamp(m_Pitch - delta.y, -MovementValidator.MaxPitch, MovementValidator.MaxPitch);
                }
            }

            bool canMove = inputActive && phase == MatchPhase.RoundPlaying && m_HasAuthorityPose;
            if (canMove && keyboard != null)
            {
                Vector2 input = Vector2.zero;
                if (keyboard.wKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed) input.y -= 1f;
                if (keyboard.dKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed) input.x -= 1f;
                if (input.sqrMagnitude > 1f)
                {
                    input.Normalize();
                }

                Quaternion facing = Quaternion.Euler(0f, m_Yaw, 0f);
                Vector3 motion = facing * new Vector3(input.x, 0f, input.y) * (WalkSpeed * Mathf.Min(deltaTime, 0.1f));
                if (motion.sqrMagnitude > 0f && m_Controller.enabled)
                {
                    m_Controller.Move(motion);
                }
            }

            // 바닥은 평평하므로 높이를 0 으로 고정하고, 경기장 밖으로 나가지 않게 한다.
            Vector3 position = GameArena.ClampToArena(transform.position);
            position.y = 0f;
            if (position != transform.position)
            {
                SetPositionImmediate(position);
            }

            transform.rotation = Quaternion.Euler(0f, m_Yaw, 0f);
            m_Visuals.SetLook(m_Pitch);

            if (inputActive && phase == MatchPhase.RoundPlaying && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                TryHitUnderCrosshair();
            }

            if (inputActive && phase == MatchPhase.RoundPlaying &&
                ((keyboard != null && keyboard.eKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame)))
            {
                TryUseActiveSkill(match);
            }

            SendPoseIfNeeded(deltaTime);
        }

        void SendPoseIfNeeded(float deltaTime)
        {
            m_SendTimer -= deltaTime;
            m_KeepAliveTimer -= deltaTime;
            if (m_SendTimer > 0f || !m_HasAuthorityPose)
            {
                return;
            }

            var pose = new NetPose(transform.position, m_Yaw, m_Pitch);
            bool changed = (pose.Position - m_LastSent.Position).sqrMagnitude > 0.000001f ||
                           Mathf.Abs(Mathf.DeltaAngle(pose.Yaw, m_LastSent.Yaw)) > 0.1f ||
                           Mathf.Abs(pose.Pitch - m_LastSent.Pitch) > 0.1f;
            if (!changed && m_KeepAliveTimer > 0f)
            {
                return;
            }

            m_SendTimer = k_SendInterval;
            m_KeepAliveTimer = k_KeepAliveInterval;
            m_LastSent = pose;
            SubmitPoseRpc(pose, m_AckedCorrectionId);
        }

        void TryHitUnderCrosshair()
        {
            Transform eye = Eye;
            if (!Physics.Raycast(eye.position, eye.forward, out RaycastHit hit, k_ClickRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            var hittable = hit.collider.GetComponentInParent<IKimchiHittable>();
            var networkObject = hit.collider.GetComponentInParent<NetworkObject>();
            if (hittable == null || networkObject == null || !networkObject.IsSpawned || !hittable.CanBeHit)
            {
                return;
            }

            RaiseHitAnimation();
            try
            {
                LocalHitPerformed?.Invoke(this, hit.point, hittable);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            RequestHitRpc(new NetworkObjectReference(networkObject), hit.point);
        }

        void TryUseActiveSkill(MatchNetwork match)
        {
            if (match == null || !match.TryGetLocalPlayer(out PlayerSlot me) || !SkillCatalog.IsActive(me.Skill) || me.ActiveSkillUsed)
            {
                return;
            }

            Transform eye = Eye;
            Vector3 aim = Physics.Raycast(eye.position, eye.forward, out RaycastHit hit, k_ClickRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.point
                : eye.position + eye.forward * 6f;
            match.RequestUseActiveSkill(aim);
        }

        // ------------------------------------------------------------------ 다른 사람 캐릭터 처리

        void UpdateRemote(float deltaTime)
        {
            NetPose target = m_Pose.Value;
            Vector3 current = transform.position;
            if ((target.Position - current).sqrMagnitude > 9f)
            {
                SnapTo(target);
                return;
            }

            float blend = 1f - Mathf.Exp(-15f * deltaTime);
            SetPositionImmediate(Vector3.Lerp(current, target.Position, blend));
            float yaw = Mathf.LerpAngle(transform.eulerAngles.y, target.Yaw, blend);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            m_Visuals.SetLook(Mathf.Lerp(m_Visuals.Pitch, target.Pitch, blend));
        }

        void SnapTo(NetPose pose)
        {
            SetPositionImmediate(pose.Position);
            transform.rotation = Quaternion.Euler(0f, pose.Yaw, 0f);
            m_Visuals.SetLook(pose.Pitch);
        }

        void SetPositionImmediate(Vector3 position)
        {
            // CharacterController 는 켜진 상태에서 위치를 직접 바꾸면 무시될 수 있어 잠깐 끈다.
            bool wasEnabled = m_Controller.enabled;
            m_Controller.enabled = false;
            transform.position = position;
            m_Controller.enabled = wasEnabled;
        }

        // ------------------------------------------------------------------ 서버 처리

        void SendCorrection(NetPose pose, bool includeLook)
        {
            unchecked
            {
                m_CorrectionId++;
            }

            CorrectPoseRpc(pose, m_CorrectionId, includeLook);
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Owner)]
        void SubmitPoseRpc(NetPose pose, uint correctionAck)
        {
            MatchNetwork match = MatchNetwork.Instance;
            if (!IsServer || m_Validator == null || match == null || !match.ServerAllowRequest(OwnerClientId, RequestKind.Pose))
            {
                return;
            }

            if (correctionAck != m_CorrectionId)
            {
                return; // 서버 보정 이전에 보낸 자세: 무시한다
            }

            bool allowMove = match.ServerIsRoundPlaying;
            PoseVerdict verdict = m_Validator.Validate(pose.ToSample(), Time.realtimeSinceStartupAsDouble, allowMove, out PoseSample accepted);
            NetPose acceptedPose = NetPose.FromSample(accepted);
            m_Pose.Value = acceptedPose;

            if (verdict == PoseVerdict.Rejected)
            {
                if (allowMove)
                {
                    match.ServerReportViolation(OwnerClientId, 1.0); // 너무 빠른 이동
                }

                SendCorrection(acceptedPose, false);
            }
            else if (verdict == PoseVerdict.Clamped)
            {
                SendCorrection(acceptedPose, false);
            }
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        void CorrectPoseRpc(NetPose pose, uint correctionId, bool includeLook)
        {
            if (!IsOwner)
            {
                return;
            }

            SetPositionImmediate(GameArena.ClampToArena(pose.Position));
            if (includeLook)
            {
                m_Yaw = MovementValidator.NormalizeYaw(pose.Yaw);
                m_Pitch = Mathf.Clamp(pose.Pitch, -MovementValidator.MaxPitch, MovementValidator.MaxPitch);
                transform.rotation = Quaternion.Euler(0f, m_Yaw, 0f);
            }

            m_AckedCorrectionId = correctionId;
            m_HasAuthorityPose = true;
            m_SendTimer = 0f;
            m_KeepAliveTimer = 0f;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void RequestHitRpc(NetworkObjectReference target, Vector3 point)
        {
            MatchNetwork match = MatchNetwork.Instance;
            if (!IsServer || m_Validator == null || match == null || !match.ServerIsRoundPlaying)
            {
                return; // 라운드가 막 끝난 경우 등: 조용히 무시
            }

            if (!match.ServerAllowRequest(OwnerClientId, RequestKind.Hit))
            {
                return;
            }

            if (!target.TryGet(out NetworkObject networkObject, NetworkManager) || networkObject == null || !networkObject.IsSpawned)
            {
                return; // 이미 사라진 재료
            }

            var hittable = networkObject.GetComponent<IKimchiHittable>();
            if (hittable == null)
            {
                match.ServerReportViolation(OwnerClientId, 3.0); // 칠 수 없는 대상
                return;
            }

            if (!hittable.CanBeHit)
            {
                return;
            }

            Vector3 center = hittable.HitCenter;
            float radius = Mathf.Clamp(hittable.HitRadius, 0.05f, 3f);
            if (!HitGeometry.CanReach(m_Validator.Last, center.x, center.y, center.z, radius))
            {
                match.ServerReportViolation(OwnerClientId, 1.0);
                return;
            }

            // 클라이언트가 보낸 타격 지점은 재료 표면 근처일 때만 믿고, 아니면 중심을 쓴다.
            Vector3 hitPoint = IsFinite(point) && (point - center).sqrMagnitude <= (radius + 0.5f) * (radius + 0.5f) ? point : center;
            Vector3 eye = m_Validator.Last.ToEye();
            Vector3 direction = hitPoint - eye;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

            try
            {
                hittable.ServerApplyHit(new HitContext(OwnerClientId, hitPoint, direction, match.ServerGetSkill(OwnerClientId)));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            PlayHitAnimationRpc();
        }

        [Rpc(SendTo.NotOwner, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Server)]
        void PlayHitAnimationRpc() => RaiseHitAnimation();

        void RaiseHitAnimation()
        {
            m_Visuals.PlayHit();
            try
            {
                HitAnimationPlayed?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
                   !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }
    }

    static class PoseSampleExtensions
    {
        public static Vector3 ToEye(this PoseSample pose) => new Vector3(pose.X, pose.Y + HitGeometry.EyeHeight, pose.Z);
    }
}
