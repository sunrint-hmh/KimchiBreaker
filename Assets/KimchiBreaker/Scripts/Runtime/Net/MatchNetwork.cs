using System;
using System.Collections.Generic;
using KimchiBreaker.Core;
using KimchiBreaker.Gameplay;
using Unity.Netcode;
using UnityEngine;

namespace KimchiBreaker.Net
{
    /// <summary>
    /// 매치 진행을 네트워크로 동기화하는 오브젝트(방마다 하나, 서버가 생성).
    ///
    /// - 서버: <see cref="MatchEngine"/> 을 실행하고, 상태가 바뀌면 <see cref="Snapshot"/> 으로 모든 클라이언트에 보낸다.
    /// - 클라이언트: 스냅숏을 받아 화면에 표시하고, 조작 요청을 RPC 로 서버에 보낸다.
    ///
    /// 클라이언트가 보낸 요청은 전부 서버에서 다시 검증한다(보낸 사람 확인, 단계/권한/값 범위, 요청 빈도).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchNetwork : NetworkBehaviour
    {
        // ------------------------------------------------------------------ 공개 API (모든 피어)

        /// <summary>현재 방의 매치 오브젝트. 방에 접속해 있지 않으면 null.</summary>
        public static MatchNetwork Instance { get; private set; }

        /// <summary>매치 오브젝트가 생기거나(인자 non-null) 사라질 때(null) 호출된다.</summary>
        public static event Action<MatchNetwork> InstanceChanged;

        /// <summary>단계가 바뀔 때 호출된다. (이전 단계, 새 단계). 모든 피어에서 발생.</summary>
        public event Action<MatchPhase, MatchPhase> PhaseChanged;

        /// <summary>스냅숏이 바뀔 때마다 호출된다.</summary>
        public event Action<MatchSnapshot> SnapshotChanged;

        /// <summary>누군가 액티브 스킬을 발동했을 때 모든 피어에서 호출된다. 스킬 효과/연출은 여기에 연결한다.</summary>
        public event Action<SkillActivation> SkillActivated;

        public MatchSnapshot Snapshot => m_Snapshot.Value;

        /// <summary>서버 기준 현재 시각(초). 모든 피어에서 같은 기준을 쓴다.</summary>
        public double ServerNow => NetworkManager != null ? NetworkManager.ServerTime.Time : 0.0;

        /// <summary>내가 방장인지.</summary>
        public bool IsLocalHost => IsSpawned && NetworkManager.LocalClientId == Snapshot.HostClientId;

        public bool TryGetLocalPlayer(out PlayerSlot slot)
        {
            slot = default;
            return IsSpawned && Snapshot.TryFindPlayer(NetworkManager.LocalClientId, out slot);
        }

        /// <summary>방장이 지금 시작 버튼을 누를 수 있는지(화면 표시용. 최종 판정은 서버가 한다).</summary>
        public bool CanLocalHostStart(out string reason)
        {
            MatchSnapshot snapshot = Snapshot;
            reason = string.Empty;
            if (!IsLocalHost)
            {
                reason = "방장만 시작할 수 있습니다.";
                return false;
            }

            if (snapshot.Phase != MatchPhase.Lobby)
            {
                reason = "이미 진행 중입니다.";
                return false;
            }

            if (snapshot.PlayerCount < MinPlayersToStart)
            {
                reason = $"최소 {MinPlayersToStart}명이 필요합니다.";
                return false;
            }

            for (int i = 0; i < MatchSnapshot.SlotCount; i++)
            {
                if (snapshot.TryGetSlot(i, out PlayerSlot slot) && !slot.Ready)
                {
                    reason = "모든 참가자가 준비해야 합니다.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>시작에 필요한 최소 인원. 개발 빌드에서는 혼자서도 테스트할 수 있도록 1명.</summary>
        public static int MinPlayersToStart => Debug.isDebugBuild ? 1 : GameRules.MinPlayersToStart;

        public void RequestSetReady(bool ready)
        {
            if (IsSpawned)
            {
                SetReadyRpc(ready);
            }
        }

        public void RequestSettings(int targetWins, int roundSeconds)
        {
            if (IsSpawned)
            {
                SetSettingsRpc(targetWins, roundSeconds);
            }
        }

        public void RequestStart()
        {
            if (IsSpawned)
            {
                StartMatchRpc();
            }
        }

        public void RequestReturnToLobby()
        {
            if (IsSpawned)
            {
                ReturnToLobbyRpc();
            }
        }

        public void RequestAbort()
        {
            if (IsSpawned)
            {
                AbortMatchRpc();
            }
        }

        /// <summary>내 액티브 스킬을 발동한다. <paramref name="aimPoint"/> 는 조준한 월드 좌표.</summary>
        public void RequestUseActiveSkill(Vector3 aimPoint)
        {
            if (IsSpawned)
            {
                UseActiveSkillRpc(aimPoint);
            }
        }

        // ------------------------------------------------------------------ 서버 전용 API

        /// <summary>
        /// [서버 전용] 김치 획득을 기록한다. 재료 완성(막타) 판정을 내린 서버 코드에서 호출한다.
        /// 라운드 진행 중이 아니거나 값이 잘못되면 false.
        /// </summary>
        public bool ServerAwardKimchi(ulong clientId, int amount)
        {
            if (!IsServer || m_Engine == null)
            {
                return false;
            }

            return m_Engine.TryAwardKimchi(clientId, amount);
        }

        /// <summary>[서버 전용] 플레이어가 이번 라운드에 가진 스킬. 패시브 효과 계산에 쓴다.</summary>
        public SkillId ServerGetSkill(ulong clientId)
        {
            PlayerRecord player = m_Engine?.FindPlayer(clientId);
            return player == null ? SkillId.None : player.Skill;
        }

        /// <summary>[서버 전용] 라운드가 진행 중이어서 재료를 칠 수 있는 상태인지.</summary>
        public bool ServerIsRoundPlaying => IsServer && m_Engine != null && m_Engine.Phase == MatchPhase.RoundPlaying;

        /// <summary>[서버 전용] 플레이어의 좌석 번호. 없으면 <see cref="GameRules.NoSeat"/>.</summary>
        public int ServerGetSeat(ulong clientId)
        {
            PlayerRecord player = m_Engine?.FindPlayer(clientId);
            return player == null ? GameRules.NoSeat : player.Seat;
        }

        /// <summary>
        /// [서버 전용] 요청 빈도를 확인한다. 허용되지 않으면 false 이고, 위반이 쌓이면 해당 클라이언트를 내보낸다.
        /// </summary>
        public bool ServerAllowRequest(ulong clientId, RequestKind kind)
        {
            if (!IsServer)
            {
                return false;
            }

            if (!m_Guards.TryGetValue(clientId, out RequestGuard guard))
            {
                return false; // 매치에 등록되지 않은 연결(아직 처리 중이거나 이미 나간 연결)
            }

            bool allowed = guard.Allow(kind, Time.realtimeSinceStartupAsDouble);
            if (!allowed)
            {
                KickIfNeeded(clientId, guard);
            }

            return allowed;
        }

        /// <summary>[서버 전용] 규칙에 어긋난 요청을 기록한다.</summary>
        public void ServerReportViolation(ulong clientId, double weight)
        {
            if (IsServer && m_Guards.TryGetValue(clientId, out RequestGuard guard))
            {
                guard.ReportViolation(weight, Time.realtimeSinceStartupAsDouble);
                KickIfNeeded(clientId, guard);
            }
        }

        /// <summary>[서버 전용] 새 연결을 받아도 되는지(접속 승인 단계).</summary>
        internal JoinError ServerCheckJoin(ulong clientId) => m_Engine == null ? JoinError.MatchInProgress : m_Engine.CheckJoin(clientId);

        /// <summary>[서버 전용] 접속 완료된 클라이언트를 매치에 등록하고 아바타를 만든다.</summary>
        internal JoinError ServerAddPlayer(ulong clientId, string nickname)
        {
            if (!IsServer || m_Engine == null)
            {
                return JoinError.MatchInProgress;
            }

            JoinError error = m_Engine.TryAddPlayer(clientId, nickname, out PlayerRecord record);
            if (error != JoinError.None)
            {
                return error;
            }

            m_Guards[clientId] = new RequestGuard();
            PlayerAvatar.ServerSpawn(NetworkManager, clientId, record.Seat);
            PublishIfChanged();
            return JoinError.None;
        }

        /// <summary>[서버 전용] 나간 클라이언트를 정리한다.</summary>
        internal void ServerRemovePlayer(ulong clientId)
        {
            m_Guards.Remove(clientId);
            if (IsServer && m_Engine != null && m_Engine.RemovePlayer(clientId, ServerNow))
            {
                PublishIfChanged();
            }
        }

        // ------------------------------------------------------------------ 내부 상태

        readonly NetworkVariable<MatchSnapshot> m_Snapshot = new NetworkVariable<MatchSnapshot>(MatchSnapshot.Empty);

        readonly Dictionary<ulong, RequestGuard> m_Guards = new Dictionary<ulong, RequestGuard>();
        readonly List<ulong> m_KickQueue = new List<ulong>();

        MatchEngine m_Engine;
        SecureRandomSource m_Random;
        PracticeIngredientSpawner m_Spawner;
        uint m_PublishedRevision;
        MatchPhase m_LastSeenPhase = MatchPhase.Lobby;

        public override void OnNetworkSpawn()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[KimchiBreaker] 매치 오브젝트가 두 개 생겼습니다. 나중 것을 무시합니다.");
                return;
            }

            Instance = this;
            m_LastSeenPhase = m_Snapshot.Value.Phase;
            m_Snapshot.OnValueChanged += HandleSnapshotChanged;

            if (IsServer)
            {
                m_Random = new SecureRandomSource();
                m_Engine = new MatchEngine(
                    NetworkManager.LocalClientId,
                    new MatchTimings(),
                    m_Random,
                    null,
                    MinPlayersToStart);
                m_Engine.PhaseChanged += HandleEnginePhaseChanged;
                m_Spawner = new PracticeIngredientSpawner(NetworkManager);
                m_PublishedRevision = m_Engine.Revision - 1; // 처음 한 번은 반드시 보낸다
                PublishIfChanged();
            }

            RaiseInstanceChanged(this);
        }

        static void RaiseInstanceChanged(MatchNetwork value)
        {
            try
            {
                InstanceChanged?.Invoke(value);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public override void OnNetworkDespawn()
        {
            m_Snapshot.OnValueChanged -= HandleSnapshotChanged;
            if (m_Engine != null)
            {
                m_Engine.PhaseChanged -= HandleEnginePhaseChanged;
                m_Engine = null;
            }

            // 방이 닫히는 중에는 Netcode 가 남은 오브젝트를 직접 정리하므로 목록만 비운다.
            m_Spawner?.Forget();
            m_Spawner = null;
            m_Random?.Dispose();
            m_Random = null;
            m_Guards.Clear();
            m_KickQueue.Clear();

            if (Instance == this)
            {
                Instance = null;
                RaiseInstanceChanged(null);
            }
        }

        public override void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                RaiseInstanceChanged(null);
            }

            m_Random?.Dispose();
            m_Random = null;
            base.OnDestroy();
        }

        void Update()
        {
            if (!IsSpawned || !IsServer || m_Engine == null)
            {
                return;
            }

            double now = ServerNow;
            m_Engine.Tick(now);
            m_Spawner?.ServerUpdate(m_Engine.Phase, now);
            PublishIfChanged();
            FlushKicks();
        }

        void PublishIfChanged()
        {
            if (m_Engine == null || m_Engine.Revision == m_PublishedRevision)
            {
                return;
            }

            m_PublishedRevision = m_Engine.Revision;
            m_Snapshot.Value = MatchSnapshot.FromEngine(m_Engine);
        }

        void HandleEnginePhaseChanged(MatchPhase previous, MatchPhase next)
        {
            // 라운드를 시작할 때와 대기실로 돌아올 때는 모두를 자기 좌석으로 돌려보낸다.
            if (next == MatchPhase.RoundIntro || next == MatchPhase.Lobby)
            {
                foreach (PlayerRecord player in m_Engine.Players)
                {
                    if (PlayerAvatar.TryGet(player.ClientId, out PlayerAvatar avatar))
                    {
                        avatar.ServerPlaceAtSeat(player.Seat);
                    }
                }
            }

            if (next != MatchPhase.RoundPlaying)
            {
                m_Spawner?.DespawnAll();
            }
        }

        void HandleSnapshotChanged(MatchSnapshot previous, MatchSnapshot current)
        {
            // 구독자(화면/연출) 쪽 오류가 네트워크 처리를 깨뜨리지 않도록 각각 보호한다.
            try
            {
                SnapshotChanged?.Invoke(current);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            if (current.Phase != m_LastSeenPhase)
            {
                MatchPhase old = m_LastSeenPhase;
                m_LastSeenPhase = current.Phase;
                try
                {
                    PhaseChanged?.Invoke(old, current.Phase);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        void KickIfNeeded(ulong clientId, RequestGuard guard)
        {
            if (guard.ShouldKick && clientId != NetworkManager.ServerClientId && !m_KickQueue.Contains(clientId))
            {
                m_KickQueue.Add(clientId);
            }
        }

        void FlushKicks()
        {
            if (m_KickQueue.Count == 0)
            {
                return;
            }

            // RPC 처리 도중이 아니라 Update 에서 연결을 끊어 내부 상태가 꼬이지 않게 한다.
            for (int i = 0; i < m_KickQueue.Count; i++)
            {
                ulong clientId = m_KickQueue[i];
                Debug.LogWarning($"[KimchiBreaker] 클라이언트 {clientId} 를 비정상 요청 반복으로 내보냅니다.");
                if (NetworkManager.ConnectedClients.ContainsKey(clientId))
                {
                    NetworkManager.DisconnectClient(clientId, DisconnectReasons.Kicked);
                }
            }

            m_KickQueue.Clear();
        }

        /// <summary>서버에서 RPC 를 처리하기 전 공통 확인. 보낸 사람이 매치 참가자이고 빈도 제한 안이어야 한다.</summary>
        bool Gate(ulong sender, RequestKind kind)
        {
            return IsServer && m_Engine != null && m_Engine.FindPlayer(sender) != null && ServerAllowRequest(sender, kind);
        }

        // ------------------------------------------------------------------ RPC (클라이언트 → 서버)

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void SetReadyRpc(bool ready, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (!Gate(sender, RequestKind.Lobby) || sender == m_Engine.HostClientId)
            {
                return; // 방장은 항상 준비 상태라 바꿀 것이 없다
            }

            if (!m_Engine.TrySetReady(sender, ready))
            {
                ServerReportViolation(sender, 1.0); // 대기실이 아닐 때의 준비 변경
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void SetSettingsRpc(int targetWins, int roundSeconds, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (Gate(sender, RequestKind.Lobby) && !m_Engine.TrySetSettings(sender, new MatchSettings(targetWins, roundSeconds)))
            {
                ServerReportViolation(sender, 2.0);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void StartMatchRpc(RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (!Gate(sender, RequestKind.Lobby))
            {
                return;
            }

            StartError error = m_Engine.TryStartMatch(sender, ServerNow);
            if (error == StartError.NotHost)
            {
                ServerReportViolation(sender, 5.0);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void ReturnToLobbyRpc(RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (!Gate(sender, RequestKind.Lobby))
            {
                return;
            }

            if (sender != m_Engine.HostClientId)
            {
                ServerReportViolation(sender, 5.0);
                return;
            }

            m_Engine.TryReturnToLobby(sender, ServerNow);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void AbortMatchRpc(RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (!Gate(sender, RequestKind.Lobby))
            {
                return;
            }

            if (sender != m_Engine.HostClientId)
            {
                ServerReportViolation(sender, 5.0);
                return;
            }

            m_Engine.TryAbortMatch(sender, ServerNow);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void UseActiveSkillRpc(Vector3 aimPoint, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (!Gate(sender, RequestKind.Skill))
            {
                return;
            }

            if (!IsFinite(aimPoint))
            {
                ServerReportViolation(sender, 5.0);
                return;
            }

            if (!m_Engine.TryUseActiveSkill(sender, out SkillId skill))
            {
                return; // 스킬이 없거나 이미 썼거나 라운드 중이 아님: 단순 무시
            }

            Vector3 clampedAim = GameArena.ClampToArena(aimPoint);
            PublishIfChanged();
            SkillActivatedRpc(sender, skill, clampedAim);
        }

        // ------------------------------------------------------------------ RPC (서버 → 모두)

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        void SkillActivatedRpc(ulong clientId, SkillId skill, Vector3 aimPoint)
        {
            if (!SkillCatalog.IsActive(skill) || !IsFinite(aimPoint))
            {
                return;
            }

            try
            {
                SkillActivated?.Invoke(new SkillActivation(clientId, skill, aimPoint));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception); // 구독자 오류가 네트워크 처리를 깨뜨리지 않게 한다
            }
        }

        static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
                   !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }
    }
}
