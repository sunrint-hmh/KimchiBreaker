using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using KimchiBreaker.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace KimchiBreaker.Net
{
    public enum SessionStatus
    {
        /// <summary>방에 들어가 있지 않음(메인 메뉴).</summary>
        Offline,

        /// <summary>방 만들기/참가/나가기 진행 중.</summary>
        Busy,

        /// <summary>방에 들어와 있음.</summary>
        InRoom,
    }

    /// <summary>공개 방 목록의 한 줄.</summary>
    public readonly struct RoomListing
    {
        public readonly string Id;
        public readonly string Name;
        public readonly int Players;
        public readonly int MaxPlayers;

        public RoomListing(string id, string name, int players, int maxPlayers)
        {
            Id = id;
            Name = name;
            Players = players;
            MaxPlayers = maxPlayers;
        }
    }

    /// <summary>
    /// 방 만들기/참가/나가기와 연결 상태를 관리한다(앱 전체에 하나).
    ///
    /// 연결 방식
    /// - 온라인(기본): Unity Relay 를 거치는 P2P 호스트. 방 코드로 참가하거나 공개 방 목록에서 고른다.
    ///   통신은 DTLS 로 암호화되고, 호스트 PC 의 포트를 열 필요가 없다.
    /// - 직접 연결(개발/테스트 전용): IP 주소로 접속. 개발 빌드와 에디터에서만 쓸 수 있다.
    ///
    /// 보안
    /// - 접속 요청은 서버에서 승인 단계를 거친다(데이터 형식, 버전, 인원, 진행 중 여부).
    /// - 닉네임은 서버가 다시 정리해서 퍼뜨린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkSession : MonoBehaviour
    {
        public const ushort DirectPort = 7777;

        const int k_OperationTimeoutMs = 20000;
        const float k_DirectConnectTimeout = 15f;
        const string k_NicknamePrefsKey = "kb.nickname";

        // ------------------------------------------------------------------ 공개 상태

        public static NetworkSession Instance { get; private set; }

        /// <summary>상태가 바뀔 때마다 호출된다(화면 갱신용).</summary>
        public event Action Changed;

        public SessionStatus Status { get; private set; } = SessionStatus.Offline;

        /// <summary>진행 중인 작업 설명("방 만드는 중..." 등).</summary>
        public string BusyMessage { get; private set; } = string.Empty;

        /// <summary>마지막 오류 안내 문구. 화면에 한 번 보여 준 뒤 <see cref="ClearError"/> 로 지운다.</summary>
        public string LastError { get; private set; } = string.Empty;

        /// <summary>온라인 방의 참가 코드. 직접 연결이면 빈 문자열.</summary>
        public string RoomCode { get; private set; } = string.Empty;

        /// <summary>현재 방이 비공개인지.</summary>
        public bool IsPrivateRoom { get; private set; }

        public bool IsOnlineRoom => m_Session != null;

        public bool IsHost => m_NetworkManager != null && m_NetworkManager.IsHost;

        public NetworkManager Manager => m_NetworkManager;

        /// <summary>내 닉네임(정리된 값). 빈 문자열이면 서버가 "플레이어N" 으로 정한다.</summary>
        public string Nickname { get; private set; } = string.Empty;

        /// <summary>IP 직접 연결 사용 가능 여부. 정식 빌드에서는 막아 둔다.</summary>
        public static bool DirectConnectAllowed => Debug.isDebugBuild;

        /// <summary>초기화 실패(프리팹 누락 등) 사유. 비어 있으면 정상.</summary>
        public string FatalError { get; private set; } = string.Empty;

        public void SetNickname(string raw)
        {
            Nickname = TextSanitizer.SanitizeNickname(raw, string.Empty);
            try
            {
                PlayerPrefs.SetString(k_NicknamePrefsKey, Nickname);
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[KimchiBreaker] 닉네임 저장 실패: " + exception.Message);
            }

            NotifyChanged();
        }

        public void ClearError()
        {
            if (LastError.Length > 0)
            {
                LastError = string.Empty;
                NotifyChanged();
            }
        }

        // ------------------------------------------------------------------ 내부 상태

        NetworkManager m_NetworkManager;
        UnityTransport m_Transport;
        ISession m_Session;
        readonly Dictionary<ulong, string> m_PendingNames = new Dictionary<ulong, string>();
        CancellationTokenSource m_OperationCancel;
        bool m_LeavingIntentionally;
        bool m_CleanupRequested;
        string m_CleanupMessage = string.Empty;
        float m_DirectConnectDeadline = -1f;
        bool m_SessionLocked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            try
            {
                Nickname = TextSanitizer.SanitizeNickname(PlayerPrefs.GetString(k_NicknamePrefsKey, string.Empty), string.Empty);
            }
            catch (Exception)
            {
                Nickname = string.Empty;
            }

            if (!NetworkPrefabCatalog.TryLoadAll(out string error))
            {
                FatalError = error;
                Debug.LogError("[KimchiBreaker] " + error);
                return;
            }

            CreateNetworkManager();
            MatchNetwork.InstanceChanged += HandleMatchInstanceChanged;
        }

        void OnDestroy()
        {
            MatchNetwork.InstanceChanged -= HandleMatchInstanceChanged;
            if (Instance != this)
            {
                return;
            }

            Instance = null;
            CancelOperation();
            UnsubscribeSession();
            if (m_NetworkManager != null)
            {
                UnsubscribeNetworkManager();
                if (m_NetworkManager.IsListening && !m_NetworkManager.ShutdownInProgress)
                {
                    m_NetworkManager.Shutdown();
                }
            }
        }

        void OnApplicationQuit()
        {
            m_LeavingIntentionally = true;
            CancelOperation();
            if (m_Session != null)
            {
                ISession session = m_Session;
                m_Session = null;
                _ = LeaveSessionQuietlyAsync(session);
            }
            else if (m_NetworkManager != null && m_NetworkManager.IsListening && !m_NetworkManager.ShutdownInProgress)
            {
                m_NetworkManager.Shutdown();
            }
        }

        void CreateNetworkManager()
        {
            // 비활성 상태에서 구성한 뒤 켜야 NetworkManager 의 초기화(Awake/OnEnable)가 완성된 설정을 본다.
            var go = new GameObject("NetworkManager");
            go.SetActive(false);
            DontDestroyOnLoad(go);

            m_Transport = go.AddComponent<UnityTransport>();
            m_Transport.ConnectTimeoutMS = 1000;
            m_Transport.MaxConnectAttempts = 10;
            m_Transport.DisconnectTimeoutMS = 15000;

            m_NetworkManager = go.AddComponent<NetworkManager>();
            m_NetworkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = m_Transport,
                ConnectionApproval = true,
                EnableSceneManagement = false,
                ForceSamePrefabs = true,
                ClientConnectionBufferTimeout = 10,
                TickRate = 30,
                PlayerPrefab = null,
            };
            NetworkPrefabCatalog.RegisterAll(m_NetworkManager);
            go.SetActive(true);

            m_NetworkManager.ConnectionApprovalCallback = HandleApproval;
            m_NetworkManager.OnServerStarted += HandleServerStarted;
            m_NetworkManager.OnClientConnectedCallback += HandleClientConnected;
            m_NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            m_NetworkManager.OnTransportFailure += HandleTransportFailure;
            m_NetworkManager.OnClientStopped += HandleClientStopped;
        }

        void UnsubscribeNetworkManager()
        {
            m_NetworkManager.ConnectionApprovalCallback = null;
            m_NetworkManager.OnServerStarted -= HandleServerStarted;
            m_NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
            m_NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            m_NetworkManager.OnTransportFailure -= HandleTransportFailure;
            m_NetworkManager.OnClientStopped -= HandleClientStopped;
        }

        void Update()
        {
            if (m_CleanupRequested)
            {
                m_CleanupRequested = false;
                CleanupAfterDisconnect(m_CleanupMessage);
            }

            if (m_DirectConnectDeadline > 0f && Time.unscaledTime > m_DirectConnectDeadline)
            {
                m_DirectConnectDeadline = -1f;
                if (Status == SessionStatus.Busy)
                {
                    RequestCleanup("서버에 연결하지 못했습니다. 주소와 방이 열려 있는지 확인하세요.");
                }
            }
        }

        // ------------------------------------------------------------------ 방 만들기 / 참가

        /// <summary>[개발용] 이 PC 에 방을 연다. <paramref name="allowLan"/> 이 true 면 같은 네트워크의 다른 PC 도 들어올 수 있다.</summary>
        public void HostDirect(bool allowLan)
        {
            if (!DirectConnectAllowed || !BeginOperation("방 만드는 중..."))
            {
                return;
            }

            PrepareConnectionData();
            m_Transport.SetConnectionData("127.0.0.1", DirectPort, allowLan ? "0.0.0.0" : "127.0.0.1");
            bool started;
            try
            {
                started = m_NetworkManager.StartHost();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                started = false;
            }

            if (!started)
            {
                FinishWithError($"방을 열지 못했습니다. 포트 {DirectPort} 를 다른 프로그램이 쓰고 있는지 확인하세요.");
                ShutdownNetworkManager();
                return;
            }

            RoomCode = string.Empty;
            IsPrivateRoom = true;
            SetStatus(SessionStatus.InRoom, string.Empty);
        }

        /// <summary>[개발용] IP 주소로 방에 참가한다.</summary>
        public void JoinDirect(string address)
        {
            if (!DirectConnectAllowed)
            {
                return;
            }

            if (!TryParseAddress(address, out string ipv4))
            {
                LastError = "IPv4 주소 형식이 아닙니다. 예: 192.168.0.10";
                NotifyChanged();
                return;
            }

            if (!BeginOperation("접속 중..."))
            {
                return;
            }

            PrepareConnectionData();
            m_Transport.SetConnectionData(ipv4, DirectPort);
            bool started;
            try
            {
                started = m_NetworkManager.StartClient();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                started = false;
            }

            if (!started)
            {
                FinishWithError("접속을 시작하지 못했습니다.");
                ShutdownNetworkManager();
                return;
            }

            m_DirectConnectDeadline = Time.unscaledTime + k_DirectConnectTimeout;
        }

        /// <summary>온라인 방을 만든다(Relay). 비공개 방은 목록에 보이지 않고 코드로만 들어올 수 있다.</summary>
        public void CreateOnlineRoom(bool isPrivate) => _ = CreateOnlineRoomAsync(isPrivate);

        /// <summary>방 코드로 온라인 방에 참가한다.</summary>
        public void JoinOnlineByCode(string rawCode)
        {
            if (!JoinCode.TryNormalize(rawCode, out string code))
            {
                LastError = $"방 코드는 영문/숫자 {JoinCode.MinLength}~{JoinCode.MaxLength}자입니다.";
                NotifyChanged();
                return;
            }

            _ = JoinOnlineAsync(code, null);
        }

        /// <summary>공개 방 목록에서 고른 방에 참가한다.</summary>
        public void JoinOnlineById(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId) || sessionId.Length > 128)
            {
                return;
            }

            _ = JoinOnlineAsync(null, sessionId);
        }

        /// <summary>참가할 수 있는 공개 방 목록을 가져온다. 실패하면 빈 목록과 함께 <see cref="LastError"/> 가 설정된다.</summary>
        public async Task<List<RoomListing>> QueryRoomsAsync()
        {
            var rooms = new List<RoomListing>();
            if (Status != SessionStatus.Offline || FatalError.Length > 0)
            {
                return rooms;
            }

            try
            {
                using (var timeout = new CancellationTokenSource(k_OperationTimeoutMs))
                {
                    await EnsureServicesAsync(timeout.Token);
                    QuerySessionsResults results = await MultiplayerService.Instance.QuerySessionsAsync(
                        new QuerySessionsOptions { Count = 30 }, timeout.Token);
                    if (results?.Sessions == null)
                    {
                        return rooms;
                    }

                    foreach (ISessionInfo info in results.Sessions)
                    {
                        if (info == null || info.IsLocked || info.HasPassword || info.AvailableSlots <= 0)
                        {
                            continue;
                        }

                        int max = Mathf.Clamp(info.MaxPlayers, 1, GameRules.MaxPlayers);
                        int players = Mathf.Clamp(info.MaxPlayers - info.AvailableSlots, 0, max);
                        string name = TextSanitizer.SanitizeNickname(info.Name, "이름 없는 방");
                        rooms.Add(new RoomListing(info.Id, name, players, max));
                    }
                }
            }
            catch (Exception exception)
            {
                LastError = DescribeServiceError(exception);
                NotifyChanged();
            }

            return rooms;
        }

        /// <summary>방에서 나간다. 방장이 나가면 방이 닫힌다.</summary>
        public void Leave() => _ = LeaveAsync();

        // ------------------------------------------------------------------ 비동기 작업

        async Task CreateOnlineRoomAsync(bool isPrivate)
        {
            if (!BeginOperation("온라인 방 만드는 중..."))
            {
                return;
            }

            CancellationToken token = m_OperationCancel.Token;
            try
            {
                await EnsureServicesAsync(token);
                PrepareConnectionData();
                string roomName = (Nickname.Length > 0 ? Nickname : "김장") + "의 방";
                var options = new SessionOptions
                {
                    MaxPlayers = GameRules.MaxPlayers,
                    IsPrivate = isPrivate,
                    Name = roomName,
                }.WithRelayNetwork();

                IHostSession session = await MultiplayerService.Instance.CreateSessionAsync(options, token);
                if (!AdoptSession(session, token))
                {
                    return;
                }

                RoomCode = session.Code ?? string.Empty;
                IsPrivateRoom = isPrivate;
                SetStatus(SessionStatus.InRoom, string.Empty);
            }
            catch (Exception exception)
            {
                HandleOperationFailure(exception);
            }
        }

        async Task JoinOnlineAsync(string code, string sessionId)
        {
            if (!BeginOperation("방에 들어가는 중..."))
            {
                return;
            }

            CancellationToken token = m_OperationCancel.Token;
            try
            {
                await EnsureServicesAsync(token);
                PrepareConnectionData();
                ISession session = code != null
                    ? await MultiplayerService.Instance.JoinSessionByCodeAsync(code, new JoinSessionOptions(), token)
                    : await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId, new JoinSessionOptions(), token);
                if (!AdoptSession(session, token))
                {
                    return;
                }

                RoomCode = session.Code ?? string.Empty;
                IsPrivateRoom = session.IsPrivate;
                if (m_NetworkManager.IsConnectedClient)
                {
                    SetStatus(SessionStatus.InRoom, string.Empty);
                }
                else
                {
                    // 세션에는 들어왔지만 게임 서버 승인 전에 거절된 경우(방이 가득 참 등)
                    RequestCleanup(DisconnectReasons.ToMessage(m_NetworkManager.DisconnectReason));
                }
            }
            catch (Exception exception)
            {
                HandleOperationFailure(exception);
            }
        }

        /// <summary>작업이 끝난 세션을 받아들인다. 그 사이 취소되었으면 바로 나가고 false.</summary>
        bool AdoptSession(ISession session, CancellationToken token)
        {
            if (session == null)
            {
                FinishWithError("방 정보를 받지 못했습니다.");
                return false;
            }

            // 기다리는 동안 취소되었거나, 연결이 끊겨 이미 정리된 경우에는 받아들이지 않는다.
            if (token.IsCancellationRequested || Instance != this || Status != SessionStatus.Busy)
            {
                _ = LeaveSessionQuietlyAsync(session);
                return false;
            }

            m_Session = session;
            m_SessionLocked = false;
            m_Session.RemovedFromSession += HandleRemovedFromSession;
            m_Session.Deleted += HandleSessionDeleted;
            return true;
        }

        async Task LeaveAsync()
        {
            if (Status == SessionStatus.Offline)
            {
                return;
            }

            m_LeavingIntentionally = true;
            CancelOperation();
            SetStatus(SessionStatus.Busy, "나가는 중...");
            ISession session = m_Session;
            UnsubscribeSession();
            m_Session = null;

            if (session != null)
            {
                await LeaveSessionQuietlyAsync(session);
            }

            ShutdownNetworkManager();
            ResetRoomState();
            SetStatus(SessionStatus.Offline, string.Empty);
            m_LeavingIntentionally = false;
        }

        static async Task LeaveSessionQuietlyAsync(ISession session)
        {
            try
            {
                await session.LeaveAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[KimchiBreaker] 세션 나가기 중 오류(무시): " + exception.Message);
            }
        }

        async Task EnsureServicesAsync(CancellationToken token)
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                var options = new InitializationOptions();
                string profile = LocalProfileName();
                if (profile != null)
                {
                    options.SetProfile(profile);
                }

                await UnityServices.InitializeAsync(options);
            }

            while (UnityServices.State == ServicesInitializationState.Initializing)
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(100, token);
            }

            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                throw new ServicesInitializationException("Unity Services 초기화 실패");
            }

            token.ThrowIfCancellationRequested();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            token.ThrowIfCancellationRequested();
        }

        /// <summary>
        /// 한 PC 에서 여러 개를 띄워 테스트할 때(에디터/개발 빌드) 서로 다른 익명 계정을 쓰도록 프로필을 나눈다.
        /// 정식 빌드는 기본 프로필을 쓴다.
        /// </summary>
        static string LocalProfileName()
        {
            if (!Debug.isDebugBuild)
            {
                return null;
            }

            try
            {
                return "dev" + System.Diagnostics.Process.GetCurrentProcess().Id;
            }
            catch (Exception)
            {
                return "dev" + UnityEngine.Random.Range(0, int.MaxValue);
            }
        }

        // ------------------------------------------------------------------ 상태 관리

        bool BeginOperation(string message)
        {
            if (Status != SessionStatus.Offline || FatalError.Length > 0 || m_NetworkManager == null)
            {
                return false;
            }

            if (m_NetworkManager.IsListening || m_NetworkManager.ShutdownInProgress)
            {
                LastError = "이전 연결을 정리하는 중입니다. 잠시 후 다시 시도하세요.";
                NotifyChanged();
                return false;
            }

            CancelOperation();
            m_OperationCancel = new CancellationTokenSource(k_OperationTimeoutMs);
            m_LeavingIntentionally = false;
            m_PendingNames.Clear();
            LastError = string.Empty;
            SetStatus(SessionStatus.Busy, message);
            return true;
        }

        void CancelOperation()
        {
            if (m_OperationCancel != null)
            {
                try
                {
                    m_OperationCancel.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }

                m_OperationCancel.Dispose();
                m_OperationCancel = null;
            }
        }

        void HandleOperationFailure(Exception exception)
        {
            if (exception is OperationCanceledException && m_LeavingIntentionally)
            {
                return; // 사용자가 직접 취소함
            }

            Debug.LogWarning("[KimchiBreaker] 온라인 작업 실패: " + exception);
            string message = DescribeServiceError(exception);
            ISession session = m_Session;
            UnsubscribeSession();
            m_Session = null;
            if (session != null)
            {
                _ = LeaveSessionQuietlyAsync(session);
            }

            ShutdownNetworkManager();
            ResetRoomState();
            FinishWithError(message);
        }

        void FinishWithError(string message)
        {
            CancelOperation();
            LastError = message;
            SetStatus(SessionStatus.Offline, string.Empty);
        }

        void SetStatus(SessionStatus status, string message)
        {
            Status = status;
            BusyMessage = message ?? string.Empty;
            if (status != SessionStatus.Busy)
            {
                m_DirectConnectDeadline = -1f;
            }

            NotifyChanged();
        }

        void NotifyChanged()
        {
            try
            {
                Changed?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        void ResetRoomState()
        {
            RoomCode = string.Empty;
            IsPrivateRoom = false;
            m_PendingNames.Clear();
            m_SessionLocked = false;
        }

        void PrepareConnectionData()
        {
            m_NetworkManager.NetworkConfig.ConnectionData = ConnectionPayload.Encode(Nickname);
        }

        void ShutdownNetworkManager()
        {
            if (m_NetworkManager != null && (m_NetworkManager.IsListening || m_NetworkManager.IsClient || m_NetworkManager.IsServer) &&
                !m_NetworkManager.ShutdownInProgress)
            {
                m_NetworkManager.Shutdown();
            }
        }

        void UnsubscribeSession()
        {
            if (m_Session != null)
            {
                m_Session.RemovedFromSession -= HandleRemovedFromSession;
                m_Session.Deleted -= HandleSessionDeleted;
            }
        }

        /// <summary>연결이 끊긴 뒤 정리는 다음 프레임에 한다(네트워크 콜백 도중에 종료하지 않기 위해).</summary>
        void RequestCleanup(string message)
        {
            if (m_LeavingIntentionally)
            {
                return;
            }

            m_CleanupRequested = true;
            m_CleanupMessage = message ?? string.Empty;
        }

        void CleanupAfterDisconnect(string message)
        {
            if (m_LeavingIntentionally || Status == SessionStatus.Offline)
            {
                return;
            }

            CancelOperation();
            ISession session = m_Session;
            UnsubscribeSession();
            m_Session = null;
            if (session != null)
            {
                _ = LeaveSessionQuietlyAsync(session);
            }

            ShutdownNetworkManager();
            ResetRoomState();
            LastError = message;
            SetStatus(SessionStatus.Offline, string.Empty);
        }

        // ------------------------------------------------------------------ NetworkManager 콜백

        void HandleApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;
            response.Pending = false;
            ulong clientId = request.ClientNetworkId;

            if (clientId == NetworkManager.ServerClientId)
            {
                response.Approved = true; // 호스트 자신
                m_PendingNames[clientId] = Nickname;
                return;
            }

            try
            {
                if (!ConnectionPayload.TryDecode(request.Payload, out int version, out string name))
                {
                    Reject(response, DisconnectReasons.BadRequest);
                    return;
                }

                if (version != GameRules.ProtocolVersion)
                {
                    Reject(response, DisconnectReasons.VersionMismatch);
                    return;
                }

                MatchNetwork match = MatchNetwork.Instance;
                if (match == null)
                {
                    Reject(response, DisconnectReasons.ServerError);
                    return;
                }

                switch (match.ServerCheckJoin(clientId))
                {
                    case JoinError.None:
                        break;
                    case JoinError.MatchInProgress:
                        Reject(response, DisconnectReasons.MatchInProgress);
                        return;
                    case JoinError.RoomFull:
                        Reject(response, DisconnectReasons.RoomFull);
                        return;
                    default:
                        Reject(response, DisconnectReasons.BadRequest);
                        return;
                }

                if (m_PendingNames.Count >= GameRules.MaxPlayers * 2)
                {
                    Reject(response, DisconnectReasons.RoomFull); // 비정상적으로 많은 동시 접속 시도
                    return;
                }

                m_PendingNames[clientId] = TextSanitizer.SanitizeNickname(name, string.Empty);
                response.Approved = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Reject(response, DisconnectReasons.ServerError);
            }
        }

        static void Reject(NetworkManager.ConnectionApprovalResponse response, string reason)
        {
            response.Approved = false;
            response.Reason = reason;
        }

        void HandleServerStarted()
        {
            GameObject prefab = NetworkPrefabCatalog.MatchState;
            if (prefab == null || MatchNetwork.Instance != null)
            {
                return;
            }

            GameObject instance = Instantiate(prefab);
            try
            {
                instance.GetComponent<NetworkObject>().Spawn(true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Destroy(instance);
                RequestCleanup("방을 준비하지 못했습니다.");
            }
        }

        void HandleClientConnected(ulong clientId)
        {
            if (m_NetworkManager.IsServer)
            {
                m_PendingNames.TryGetValue(clientId, out string name);
                m_PendingNames.Remove(clientId);
                MatchNetwork match = MatchNetwork.Instance;
                JoinError error = match != null ? match.ServerAddPlayer(clientId, name ?? string.Empty) : JoinError.MatchInProgress;
                if (error != JoinError.None && clientId != NetworkManager.ServerClientId)
                {
                    m_NetworkManager.DisconnectClient(clientId, error == JoinError.RoomFull ? DisconnectReasons.RoomFull : DisconnectReasons.MatchInProgress);
                }
                else if (error != JoinError.None)
                {
                    RequestCleanup("방을 준비하지 못했습니다.");
                }

                return;
            }

            if (clientId == m_NetworkManager.LocalClientId && Status == SessionStatus.Busy && m_Session == null)
            {
                SetStatus(SessionStatus.InRoom, string.Empty); // 직접 연결 성공
            }
        }

        void HandleClientDisconnected(ulong clientId)
        {
            if (m_NetworkManager.IsServer)
            {
                if (clientId == NetworkManager.ServerClientId)
                {
                    return;
                }

                m_PendingNames.Remove(clientId);
                MatchNetwork match = MatchNetwork.Instance;
                if (match != null)
                {
                    match.ServerRemovePlayer(clientId);
                }

                return;
            }

            if (clientId == m_NetworkManager.LocalClientId || clientId == NetworkManager.ServerClientId)
            {
                RequestCleanup(DescribeDisconnect(m_NetworkManager.DisconnectReason));
            }
        }

        void HandleTransportFailure()
        {
            RequestCleanup("네트워크 오류로 연결이 끊어졌습니다.");
        }

        void HandleClientStopped(bool wasHost)
        {
            if (!m_LeavingIntentionally && Status != SessionStatus.Offline)
            {
                RequestCleanup(wasHost ? "방이 닫혔습니다." : DescribeDisconnect(m_NetworkManager.DisconnectReason));
            }
        }

        void HandleRemovedFromSession() => RequestCleanup("방에서 나가졌습니다.");

        void HandleSessionDeleted() => RequestCleanup(DisconnectReasons.ToMessage(DisconnectReasons.HostLeft));

        // ------------------------------------------------------------------ 방 잠금(진행 중에는 목록에서 숨김)

        void HandleMatchInstanceChanged(MatchNetwork match)
        {
            if (match != null)
            {
                match.PhaseChanged += HandlePhaseChanged;
            }
        }

        void HandlePhaseChanged(MatchPhase previous, MatchPhase next)
        {
            if (!(m_Session is IHostSession host) || !IsHost)
            {
                return;
            }

            bool shouldLock = next != MatchPhase.Lobby;
            if (shouldLock != m_SessionLocked)
            {
                m_SessionLocked = shouldLock;
                _ = SetSessionLockedAsync(host, shouldLock);
            }
        }

        static async Task SetSessionLockedAsync(IHostSession host, bool locked)
        {
            try
            {
                host.IsLocked = locked;
                await host.SavePropertiesAsync();
            }
            catch (Exception exception)
            {
                // 잠금은 목록 표시용일 뿐이다. 실패해도 접속 승인 단계에서 진행 중인 방 참가를 막는다.
                Debug.LogWarning("[KimchiBreaker] 방 잠금 상태 변경 실패(무시): " + exception.Message);
            }
        }

        // ------------------------------------------------------------------ 도우미

        static bool TryParseAddress(string raw, out string ipv4)
        {
            ipv4 = string.Empty;
            if (string.IsNullOrWhiteSpace(raw) || raw.Length > 64)
            {
                return false;
            }

            string trimmed = raw.Trim();
            if (string.Equals(trimmed, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                ipv4 = "127.0.0.1";
                return true;
            }

            if (IPAddress.TryParse(trimmed, out IPAddress address) && address.AddressFamily == AddressFamily.InterNetwork &&
                trimmed.Split('.').Length == 4)
            {
                ipv4 = address.ToString();
                return true;
            }

            return false;
        }

        static string DescribeDisconnect(string reason)
        {
            if (string.IsNullOrEmpty(reason) || !reason.StartsWith("kb:", StringComparison.Ordinal))
            {
                return "방장과의 연결이 끊어졌습니다. (방이 닫혔거나 네트워크 문제)";
            }

            return DisconnectReasons.ToMessage(reason);
        }

        static string DescribeServiceError(Exception exception)
        {
            switch (exception)
            {
                case OperationCanceledException _:
                    return "응답이 너무 늦어 취소했습니다. 인터넷 연결을 확인하세요.";
                case SessionException session:
                    switch (session.Error)
                    {
                        case SessionError.SessionNotFound:
                        case SessionError.SessionDeleted:
                        case SessionError.InvalidSessionIdentifier:
                            return "방을 찾을 수 없습니다. 방 코드를 다시 확인하세요.";
                        case SessionError.RateLimitExceeded:
                            return "요청이 너무 많습니다. 잠시 후 다시 시도하세요.";
                        case SessionError.Forbidden:
                        case SessionError.NotAuthorized:
                            return "이 방에 들어갈 권한이 없습니다(가득 찼거나 잠긴 방).";
                        case SessionError.NetworkManagerStartFailed:
                        case SessionError.NetworkSetupFailed:
                            return "게임 연결을 시작하지 못했습니다. 잠시 후 다시 시도하세요.";
                        default:
                            return "온라인 방 처리 중 오류가 발생했습니다.";
                    }

                case ServicesInitializationException _:
                case AuthenticationException _:
                    return "온라인 서비스에 로그인하지 못했습니다. 인터넷 연결과 Unity 프로젝트 연결 상태를 확인하세요.";
                case RequestFailedException _:
                    return "온라인 서비스 요청이 실패했습니다. 인터넷 연결을 확인하세요.";
                default:
                    return "알 수 없는 오류로 작업을 완료하지 못했습니다.";
            }
        }
    }
}
