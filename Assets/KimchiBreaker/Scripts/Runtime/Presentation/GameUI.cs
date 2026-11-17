using System;
using System.Collections.Generic;
using KimchiBreaker.Core;
using KimchiBreaker.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KimchiBreaker.Presentation
{
    /// <summary>
    /// [임시] 메뉴/대기실/인게임 HUD/결과 화면. 기능 확인용으로 IMGUI 로 단순하게 만든다.
    /// 남현 님의 정식 UI 가 준비되면 이 클래스를 대체한다. 화면은 게임 로직을 직접 바꾸지 않고
    /// <see cref="NetworkSession"/> 과 <see cref="MatchNetwork"/> 의 공개 API 만 호출한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameUI : MonoBehaviour
    {
        const float k_ReferenceHeight = 1080f;

        static readonly string[] s_FontCandidates =
        {
            "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "AppleGothic", "Noto Sans CJK KR",
            "Noto Sans KR", "NanumGothic", "Nanum Gothic", "Gulim", "Arial",
        };

        NetworkSession m_Session;
        CameraDirector m_Camera;
        Font m_Font;
        bool m_StylesReady;
        GUIStyle m_Title;
        GUIStyle m_Label;
        GUIStyle m_Small;
        GUIStyle m_Big;
        GUIStyle m_Center;
        GUIStyle m_Button;
        GUIStyle m_Field;
        GUIStyle m_Box;
        GUIStyle m_Error;

        string m_NicknameInput = string.Empty;
        string m_CodeInput = string.Empty;
        string m_AddressInput = "127.0.0.1";
        bool m_AllowLan;
        List<RoomListing> m_Rooms = new List<RoomListing>();
        bool m_RoomsLoading;
        string m_LobbyNotice = string.Empty;
        MatchEndReason m_ShownEndReason;
        float m_Scale = 1f;
        float m_Width = 1920f;

        /// <summary>로딩 화면 표시 여부와 진행도(0~1). <see cref="App.GameBootstrap"/> 이 설정한다.</summary>
        public bool Loading { get; set; } = true;

        public float LoadingProgress { get; set; }

        public string LoadingMessage { get; set; } = "준비 중...";

        public void Initialize(NetworkSession session, CameraDirector cameraDirector)
        {
            m_Session = session;
            m_Camera = cameraDirector;
            m_NicknameInput = session != null ? session.Nickname : string.Empty;
        }

        void Awake()
        {
            try
            {
                m_Font = Font.CreateDynamicFontFromOSFont(s_FontCandidates, 18);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[KimchiBreaker] 시스템 글꼴을 불러오지 못해 기본 글꼴을 씁니다: " + exception.Message);
            }
        }

        readonly Queue<Action> m_Deferred = new Queue<Action>();

        /// <summary>
        /// 버튼 동작은 다음 Update 에서 실행한다. IMGUI 는 한 프레임 안에서 화면 구성이 바뀌면
        /// 레이아웃 오류가 나므로, 그리기 도중에는 상태를 바꾸지 않는다.
        /// </summary>
        void Defer(Action action)
        {
            if (action != null && m_Deferred.Count < 16)
            {
                m_Deferred.Enqueue(action);
            }
        }

        void Update()
        {
            while (m_Deferred.Count > 0)
            {
                Action action = m_Deferred.Dequeue();
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            UpdateMenuToggle();
            UpdateCursor();
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // ------------------------------------------------------------------ 마우스/메뉴 상태

        static bool IsInMatchPhase(out MatchPhase phase)
        {
            MatchNetwork match = MatchNetwork.Instance;
            phase = match != null && match.IsSpawned ? match.Snapshot.Phase : MatchPhase.Lobby;
            return match != null && match.IsSpawned && phase != MatchPhase.Lobby && phase != MatchPhase.Finale;
        }

        void UpdateMenuToggle()
        {
            Keyboard keyboard = Keyboard.current;
            bool inRoom = m_Session != null && m_Session.Status == SessionStatus.InRoom;
            if (!inRoom)
            {
                LocalControl.MenuOpen = false;
                return;
            }

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                LocalControl.MenuOpen = !LocalControl.MenuOpen;
            }
        }

        void UpdateCursor()
        {
            bool wantLock = IsInMatchPhase(out MatchPhase phase) &&
                            (phase == MatchPhase.RoundIntro || phase == MatchPhase.RoundPlaying) &&
                            !LocalControl.MenuOpen && Application.isFocused;
            CursorLockMode mode = wantLock ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != mode)
            {
                Cursor.lockState = mode;
                Cursor.visible = !wantLock;
            }
        }

        // ------------------------------------------------------------------ 그리기

        void OnGUI()
        {
            EnsureStyles();
            m_Scale = Mathf.Max(0.5f, Screen.height / k_ReferenceHeight);
            m_Width = Screen.width / m_Scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(m_Scale, m_Scale, 1f));

            try
            {
                if (Loading)
                {
                    DrawLoading();
                    return;
                }

                if (m_Session == null)
                {
                    return;
                }

                if (m_Session.FatalError.Length > 0)
                {
                    DrawCenteredPanel(520f, 220f, () =>
                    {
                        GUILayout.Label("게임을 시작할 수 없습니다", m_Title);
                        GUILayout.Label(m_Session.FatalError, m_Error);
                    });
                    return;
                }

                switch (m_Session.Status)
                {
                    case SessionStatus.Offline:
                        DrawMainMenu();
                        break;
                    case SessionStatus.Busy:
                        DrawBusy();
                        break;
                    case SessionStatus.InRoom:
                        DrawRoom();
                        break;
                }
            }
            finally
            {
                GUI.matrix = Matrix4x4.identity;
            }
        }

        void DrawLoading()
        {
            GUI.Box(new Rect(0f, 0f, m_Width, k_ReferenceHeight), GUIContent.none, m_Box);
            GUI.Label(new Rect(0f, 380f, m_Width, 80f), "Kimchi Breaker", m_Big);
            GUI.Label(new Rect(0f, 470f, m_Width, 40f), LoadingMessage, m_Center);
            float barWidth = 600f;
            var bar = new Rect((m_Width - barWidth) * 0.5f, 530f, barWidth, 18f);
            GUI.Box(bar, GUIContent.none);
            GUI.Box(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(LoadingProgress), bar.height), GUIContent.none, m_Button);
        }

        void DrawMainMenu()
        {
            DrawCenteredPanel(640f, NetworkSession.DirectConnectAllowed ? 860f : 700f, () =>
            {
                GUILayout.Label("Kimchi Breaker", m_Title);
                if (m_Session.LastError != NetworkSession.NotLinkedMessage)
                {
                    DrawError(m_Session.LastError); // 미연결 안내는 아래 온라인 영역에 따로 표시한다
                }

                GUILayout.Label("닉네임", m_Small);
                GUILayout.BeginHorizontal();
                m_NicknameInput = GUILayout.TextField(m_NicknameInput ?? string.Empty, 24, m_Field, GUILayout.Height(40f));
                if (GUILayout.Button("저장", m_Button, GUILayout.Width(100f), GUILayout.Height(40f)))
                {
                    Defer(() =>
                    {
                        m_Session.SetNickname(m_NicknameInput);
                        m_NicknameInput = m_Session.Nickname;
                    });
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(16f);

                // 프로젝트가 Unity Cloud 에 연결되지 않았으면 온라인 기능을 막고 이유를 미리 보여 준다.
                bool online = NetworkSession.OnlineServicesLinked;
                if (!online)
                {
                    GUILayout.Label(NetworkSession.NotLinkedMessage, m_Error);
                }

                GUI.enabled = online;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("공개 방 만들기", m_Button, GUILayout.Height(52f)))
                {
                    Defer(() =>
                    {
                        ApplyNickname();
                        m_Session.CreateOnlineRoom(false);
                    });
                }

                if (GUILayout.Button("비공개 방 만들기", m_Button, GUILayout.Height(52f)))
                {
                    Defer(() =>
                    {
                        ApplyNickname();
                        m_Session.CreateOnlineRoom(true);
                    });
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(10f);

                GUILayout.BeginHorizontal();
                m_CodeInput = GUILayout.TextField(m_CodeInput ?? string.Empty, 16, m_Field, GUILayout.Height(44f));
                if (GUILayout.Button("코드로 참가", m_Button, GUILayout.Width(180f), GUILayout.Height(44f)))
                {
                    Defer(() =>
                    {
                        ApplyNickname();
                        m_Session.JoinOnlineByCode(m_CodeInput);
                    });
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(10f);

                GUILayout.BeginHorizontal();
                GUILayout.Label("공개 방 목록", m_Label);
                GUI.enabled = online && !m_RoomsLoading;
                if (GUILayout.Button(m_RoomsLoading ? "불러오는 중..." : "새로고침", m_Button, GUILayout.Width(180f), GUILayout.Height(36f)))
                {
                    Defer(() =>
                    {
                        RefreshRooms();
                    });
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();

                if (m_Rooms.Count == 0)
                {
                    GUILayout.Label(m_RoomsLoading ? " " : "표시할 방이 없습니다.", m_Small);
                }

                for (int i = 0; i < m_Rooms.Count && i < 6; i++)
                {
                    RoomListing room = m_Rooms[i];
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{room.Name}  ({room.Players}/{room.MaxPlayers})", m_Label);
                    if (GUILayout.Button("참가", m_Button, GUILayout.Width(100f), GUILayout.Height(34f)))
                    {
                        Defer(() =>
                        {
                            ApplyNickname();
                            m_Session.JoinOnlineById(room.Id);
                        });
                    }

                    GUILayout.EndHorizontal();
                }

                if (NetworkSession.DirectConnectAllowed)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("개발용 직접 연결 (정식 빌드에서는 숨겨짐)", m_Small);
                    GUILayout.BeginHorizontal();
                    m_AllowLan = GUILayout.Toggle(m_AllowLan, " 같은 네트워크 허용", GUILayout.Width(220f));
                    if (GUILayout.Button("직접 연결 방 열기", m_Button, GUILayout.Height(40f)))
                    {
                        Defer(() =>
                        {
                            ApplyNickname();
                            m_Session.HostDirect(m_AllowLan);
                        });
                    }

                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    m_AddressInput = GUILayout.TextField(m_AddressInput ?? string.Empty, 40, m_Field, GUILayout.Height(40f));
                    if (GUILayout.Button("IP로 참가", m_Button, GUILayout.Width(180f), GUILayout.Height(40f)))
                    {
                        Defer(() =>
                        {
                            ApplyNickname();
                            m_Session.JoinDirect(m_AddressInput);
                        });
                    }

                    GUILayout.EndHorizontal();
                }
            });
        }

        void ApplyNickname()
        {
            if (m_Session.Nickname != m_NicknameInput)
            {
                m_Session.SetNickname(m_NicknameInput);
                m_NicknameInput = m_Session.Nickname;
            }
        }

        async void RefreshRooms()
        {
            // async void 는 예외가 밖으로 새면 안 되므로 전체를 감싼다.
            if (m_RoomsLoading || m_Session == null)
            {
                return;
            }

            m_RoomsLoading = true;
            try
            {
                m_Session.ClearError();
                m_Rooms = await m_Session.QueryRoomsAsync() ?? new List<RoomListing>();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                m_Rooms = new List<RoomListing>();
            }
            finally
            {
                m_RoomsLoading = false;
            }
        }

        void DrawBusy()
        {
            DrawCenteredPanel(520f, 200f, () =>
            {
                GUILayout.Label(m_Session.BusyMessage, m_Title);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("취소", m_Button, GUILayout.Height(44f)))
                {
                    Defer(() =>
                    {
                        m_Session.Leave();
                    });
                }
            });
        }

        void DrawRoom()
        {
            MatchNetwork match = MatchNetwork.Instance;
            if (match == null || !match.IsSpawned)
            {
                DrawCenteredPanel(420f, 140f, () => GUILayout.Label("방 정보를 받는 중...", m_Title));
                return;
            }

            MatchSnapshot snapshot = match.Snapshot;
            TrackEndReason(snapshot);

            switch (snapshot.Phase)
            {
                case MatchPhase.Lobby:
                    DrawLobby(match, snapshot);
                    break;
                case MatchPhase.Finale:
                    DrawFinale(match, snapshot);
                    break;
                default:
                    DrawHud(match, snapshot);
                    break;
            }

            if (LocalControl.MenuOpen)
            {
                DrawPauseMenu(match, snapshot);
            }
        }

        void TrackEndReason(MatchSnapshot snapshot)
        {
            if (snapshot.Phase != MatchPhase.Lobby)
            {
                m_ShownEndReason = MatchEndReason.None;
                m_LobbyNotice = string.Empty;
                return;
            }

            if (snapshot.LastEndReason != m_ShownEndReason)
            {
                m_ShownEndReason = snapshot.LastEndReason;
                switch (snapshot.LastEndReason)
                {
                    case MatchEndReason.NotEnoughPlayers:
                        m_LobbyNotice = "인원이 부족해 게임이 중단되었습니다.";
                        break;
                    case MatchEndReason.HostAborted:
                        m_LobbyNotice = "방장이 게임을 중단했습니다.";
                        break;
                    default:
                        m_LobbyNotice = string.Empty;
                        break;
                }
            }
        }

        // ------------------------------------------------------------------ 대기실

        void DrawLobby(MatchNetwork match, MatchSnapshot snapshot)
        {
            bool isHost = match.IsLocalHost;
            DrawCenteredPanel(700f, 720f, () =>
            {
                GUILayout.Label("대기실", m_Title);
                if (m_Session.RoomCode.Length > 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"방 코드: {m_Session.RoomCode}  ({(m_Session.IsPrivateRoom ? "비공개" : "공개")})", m_Label);
                    if (GUILayout.Button("복사", m_Button, GUILayout.Width(100f), GUILayout.Height(34f)))
                    {
                        Defer(() =>
                        {
                            GUIUtility.systemCopyBuffer = m_Session.RoomCode;
                        });
                    }

                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.Label($"직접 연결 방 (포트 {NetworkSession.DirectPort})", m_Label);
                }

                DrawError(m_LobbyNotice);
                GUILayout.Space(8f);

                for (int i = 0; i < MatchSnapshot.SlotCount; i++)
                {
                    GUILayout.BeginHorizontal(GUILayout.Height(44f));
                    if (snapshot.TryGetSlot(i, out PlayerSlot slot))
                    {
                        DrawColorChip(slot.Seat);
                        string role = slot.ClientId == snapshot.HostClientId ? " (방장)" : string.Empty;
                        string me = slot.ClientId == match.NetworkManager.LocalClientId ? " ← 나" : string.Empty;
                        GUILayout.Label(slot.DisplayName + role + me, m_Label);
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(slot.Ready ? "준비 완료" : "대기 중", m_Label, GUILayout.Width(140f));
                    }
                    else
                    {
                        GUILayout.Label("빈 자리", m_Small);
                    }

                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(12f);
                GUILayout.Label($"목표 승수: {snapshot.TargetWins}승   라운드 시간: {snapshot.RoundSeconds}초", m_Label);

                if (isHost)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("승수 -", m_Button, GUILayout.Height(38f)))
                    {
                        Defer(() =>
                        {
                            match.RequestSettings(snapshot.TargetWins - 1, snapshot.RoundSeconds);
                        });
                    }

                    if (GUILayout.Button("승수 +", m_Button, GUILayout.Height(38f)))
                    {
                        Defer(() =>
                        {
                            match.RequestSettings(snapshot.TargetWins + 1, snapshot.RoundSeconds);
                        });
                    }

                    if (GUILayout.Button("시간 -15초", m_Button, GUILayout.Height(38f)))
                    {
                        Defer(() =>
                        {
                            match.RequestSettings(snapshot.TargetWins, Mathf.Max(GameRules.MinRoundSeconds, snapshot.RoundSeconds - 15));
                        });
                    }

                    if (GUILayout.Button("시간 +15초", m_Button, GUILayout.Height(38f)))
                    {
                        Defer(() =>
                        {
                            match.RequestSettings(snapshot.TargetWins, Mathf.Min(GameRules.MaxRoundSeconds, snapshot.RoundSeconds + 15));
                        });
                    }

                    GUILayout.EndHorizontal();
                    GUILayout.Space(8f);

                    bool canStart = match.CanLocalHostStart(out string reason);
                    GUI.enabled = canStart;
                    if (GUILayout.Button("게임 시작", m_Button, GUILayout.Height(56f)))
                    {
                        Defer(() =>
                        {
                            match.RequestStart();
                        });
                    }

                    GUI.enabled = true;
                    if (!canStart)
                    {
                        GUILayout.Label(reason, m_Small);
                    }
                }
                else if (match.TryGetLocalPlayer(out PlayerSlot me))
                {
                    if (GUILayout.Button(me.Ready ? "준비 취소" : "준비 완료", m_Button, GUILayout.Height(56f)))
                    {
                        Defer(() =>
                        {
                            match.RequestSetReady(!me.Ready);
                        });
                    }
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button(isHost ? "방 닫기" : "방 나가기", m_Button, GUILayout.Height(40f)))
                {
                    Defer(() =>
                    {
                        m_Session.Leave();
                    });
                }
            });
        }

        // ------------------------------------------------------------------ 인게임

        void DrawHud(MatchNetwork match, MatchSnapshot snapshot)
        {
            double now = match.ServerNow;
            float remaining = snapshot.GetRemainingSeconds(now);

            // 상단: 타이머와 플레이어 현황
            if (snapshot.Phase == MatchPhase.RoundPlaying)
            {
                int seconds = Mathf.CeilToInt(Mathf.Max(0f, remaining));
                Color previous = GUI.color;
                if (seconds <= 10)
                {
                    GUI.color = new Color(1f, 0.3f, 0.3f);
                }

                GUI.Label(new Rect(0f, 16f, m_Width, 70f), $"{seconds / 60}:{seconds % 60:00}", m_Big);
                GUI.color = previous;
            }

            GUI.Label(new Rect(0f, 86f, m_Width, 30f), $"{snapshot.RoundNumber} 라운드  ·  {snapshot.TargetWins}승 선승", m_Center);
            DrawScoreRow(snapshot, 24f);

            switch (snapshot.Phase)
            {
                case MatchPhase.RoundIntro:
                    GUI.Label(new Rect(0f, 420f, m_Width, 120f), remaining > 0.5f ? Mathf.CeilToInt(remaining).ToString() : "시작!", m_Big);
                    break;

                case MatchPhase.RoundPlaying:
                    DrawCrosshair();
                    DrawSkillPanel(match);
                    DrawNameLabels(snapshot);
                    break;

                case MatchPhase.RoundSettlement:
                    DrawSettlement(snapshot, "정산 중 - 김치를 장독대에 넣는 중!");
                    break;

                case MatchPhase.Lottery:
                    DrawLottery(snapshot);
                    break;

                case MatchPhase.RoundResult:
                    DrawSettlement(snapshot, "라운드 결과");
                    break;
            }
        }

        void DrawScoreRow(MatchSnapshot snapshot, float y)
        {
            float cellWidth = 240f;
            float x = 20f;
            for (int i = 0; i < MatchSnapshot.SlotCount; i++)
            {
                if (!snapshot.TryGetSlot(i, out PlayerSlot slot))
                {
                    continue;
                }

                var rect = new Rect(i < 2 ? x + i * cellWidth : m_Width - 20f - (4 - i) * cellWidth, y, cellWidth - 10f, 54f);
                GUI.Box(rect, GUIContent.none, m_Box);
                Color previous = GUI.color;
                GUI.color = AvatarVisuals.SeatColor(slot.Seat);
                GUI.DrawTexture(new Rect(rect.x + 8f, rect.y + 12f, 14f, 30f), Texture2D.whiteTexture);
                GUI.color = previous;
                GUI.Label(new Rect(rect.x + 30f, rect.y + 2f, rect.width - 34f, 26f), slot.DisplayName, m_Label);
                GUI.Label(new Rect(rect.x + 30f, rect.y + 26f, rect.width - 34f, 26f), $"김치 {slot.RoundKimchi}  ·  {slot.Wins}승", m_Small);
            }
        }

        void DrawCrosshair()
        {
            float centerX = m_Width * 0.5f;
            float centerY = k_ReferenceHeight * 0.5f;
            GUI.DrawTexture(new Rect(centerX - 2f, centerY - 12f, 4f, 24f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centerX - 12f, centerY - 2f, 24f, 4f), Texture2D.whiteTexture);
        }

        void DrawSkillPanel(MatchNetwork match)
        {
            if (!match.TryGetLocalPlayer(out PlayerSlot me) || !SkillCatalog.IsDefined(me.Skill))
            {
                return;
            }

            if (SkillCatalog.IsPassive(me.Skill))
            {
                GUI.Box(new Rect(20f, k_ReferenceHeight - 110f, 420f, 90f), GUIContent.none, m_Box);
                GUI.Label(new Rect(34f, k_ReferenceHeight - 104f, 400f, 32f), "패시브: " + SkillCatalog.DisplayName(me.Skill), m_Label);
                GUI.Label(new Rect(34f, k_ReferenceHeight - 70f, 400f, 44f), SkillCatalog.Description(me.Skill), m_Small);
            }
            else
            {
                var rect = new Rect(m_Width - 440f, k_ReferenceHeight - 110f, 420f, 90f);
                GUI.Box(rect, GUIContent.none, m_Box);
                string state = me.ActiveSkillUsed ? "(사용함)" : "[E / 우클릭]";
                GUI.Label(new Rect(rect.x + 14f, rect.y + 6f, 400f, 32f), $"{SkillCatalog.DisplayName(me.Skill)} {state}", m_Label);
                GUI.Label(new Rect(rect.x + 14f, rect.y + 40f, 400f, 44f), SkillCatalog.Description(me.Skill), m_Small);
            }
        }

        void DrawNameLabels(MatchSnapshot snapshot)
        {
            Camera camera = m_Camera != null ? m_Camera.Camera : null;
            if (camera == null)
            {
                return;
            }

            foreach (PlayerAvatar avatar in PlayerAvatar.All)
            {
                if (avatar == null || avatar == PlayerAvatar.Local || !snapshot.TryFindPlayer(avatar.OwnerClientId, out PlayerSlot slot))
                {
                    continue;
                }

                Vector3 screen = camera.WorldToScreenPoint(avatar.transform.position + Vector3.up * 2.2f);
                if (screen.z <= 0f)
                {
                    continue;
                }

                float x = screen.x / m_Scale;
                float y = (Screen.height - screen.y) / m_Scale;
                GUI.Label(new Rect(x - 120f, y - 16f, 240f, 32f), slot.DisplayName, m_Center);
            }
        }

        void DrawSettlement(MatchSnapshot snapshot, string title)
        {
            DrawCenteredPanel(760f, 420f, () =>
            {
                GUILayout.Label(title, m_Title);
                if (snapshot.TryFindPlayer(snapshot.RoundWinnerId, out PlayerSlot winner))
                {
                    GUILayout.Label($"이번 라운드 승리: {winner.DisplayName}", m_Label);
                }
                else
                {
                    GUILayout.Label("이번 라운드는 승자가 없습니다.", m_Label);
                }

                GUILayout.Space(8f);
                for (int i = 0; i < MatchSnapshot.SlotCount; i++)
                {
                    if (!snapshot.TryGetSlot(i, out PlayerSlot slot))
                    {
                        continue;
                    }

                    GUILayout.BeginHorizontal();
                    DrawColorChip(slot.Seat);
                    GUILayout.Label(slot.DisplayName, m_Label, GUILayout.Width(240f));
                    GUILayout.Label($"김치 {slot.RoundKimchi}", m_Label, GUILayout.Width(120f));
                    GUILayout.Label($"번호표 {slot.TicketCount}장", m_Label, GUILayout.Width(150f));
                    GUILayout.Label($"{slot.Wins}승", m_Label);
                    GUILayout.EndHorizontal();
                }

                if (snapshot.Phase == MatchPhase.RoundResult && snapshot.TryFindPlayer(snapshot.LotteryWinnerId, out PlayerSlot lucky))
                {
                    GUILayout.Space(8f);
                    GUILayout.Label($"다음 라운드 스킬: {lucky.DisplayName} - {SkillCatalog.DisplayName(snapshot.LotterySkill)}", m_Label);
                }
            });
        }

        void DrawLottery(MatchSnapshot snapshot)
        {
            DrawCenteredPanel(700f, 300f, () =>
            {
                GUILayout.Label("김치전사의 추첨!", m_Title);
                GUILayout.Label($"장독대 속 번호표: {snapshot.TotalTickets}장", m_Label);
                if (snapshot.WinningTicket > 0 && snapshot.TryFindPlayer(snapshot.LotteryWinnerId, out PlayerSlot winner))
                {
                    GUILayout.Label($"당첨 번호: {snapshot.WinningTicket}번", m_Big);
                    GUILayout.Label($"{winner.DisplayName} 님이 [{SkillCatalog.DisplayName(snapshot.LotterySkill)}] 획득!", m_Label);
                }
                else
                {
                    GUILayout.Label("번호표가 없어 이번에는 추첨이 없습니다.", m_Label);
                }
            });
        }

        void DrawFinale(MatchNetwork match, MatchSnapshot snapshot)
        {
            string winner = snapshot.TryFindPlayer(snapshot.MatchWinnerId, out PlayerSlot slot) ? slot.DisplayName : "?";
            GUI.Label(new Rect(0f, 80f, m_Width, 90f), $"최종 우승: {winner}!", m_Big);

            float bottom = k_ReferenceHeight - 120f;
            if (match.IsLocalHost)
            {
                bool ready = match.ServerNow - snapshot.PhaseStartTime >= new MatchTimings().FinaleMinSeconds;
                GUI.enabled = ready;
                if (GUI.Button(new Rect(m_Width * 0.5f - 310f, bottom, 300f, 60f), "대기실로 돌아가기", m_Button))
                {
                    Defer(() =>
                    {
                        match.RequestReturnToLobby();
                    });
                }

                GUI.enabled = true;
                if (GUI.Button(new Rect(m_Width * 0.5f + 10f, bottom, 300f, 60f), "방 닫기", m_Button))
                {
                    Defer(() =>
                    {
                        m_Session.Leave();
                    });
                }
            }
            else
            {
                GUI.Label(new Rect(0f, bottom, m_Width, 40f), "방장이 다음 진행을 고르는 중입니다.", m_Center);
                if (GUI.Button(new Rect(m_Width * 0.5f - 150f, bottom + 44f, 300f, 50f), "방 나가기", m_Button))
                {
                    Defer(() =>
                    {
                        m_Session.Leave();
                    });
                }
            }
        }

        void DrawPauseMenu(MatchNetwork match, MatchSnapshot snapshot)
        {
            DrawCenteredPanel(420f, 300f, () =>
            {
                GUILayout.Label("일시 메뉴", m_Title);
                if (GUILayout.Button("계속하기", m_Button, GUILayout.Height(48f)))
                {
                    Defer(() =>
                    {
                        LocalControl.MenuOpen = false;
                    });
                }

                if (match.IsLocalHost && snapshot.Phase != MatchPhase.Lobby)
                {
                    if (GUILayout.Button("게임 중단 (대기실로)", m_Button, GUILayout.Height(48f)))
                    {
                        Defer(() =>
                        {
                            LocalControl.MenuOpen = false;
                            match.RequestAbort();
                        });
                    }
                }

                if (GUILayout.Button(match.IsLocalHost ? "방 닫기" : "방 나가기", m_Button, GUILayout.Height(48f)))
                {
                    Defer(() =>
                    {
                        LocalControl.MenuOpen = false;
                        m_Session.Leave();
                    });
                }
            });
        }

        // ------------------------------------------------------------------ 도우미

        void DrawCenteredPanel(float width, float height, Action content)
        {
            width = Mathf.Min(width, m_Width - 40f);
            var rect = new Rect((m_Width - width) * 0.5f, (k_ReferenceHeight - height) * 0.5f, width, height);
            GUI.Box(rect, GUIContent.none, m_Box);
            GUILayout.BeginArea(new Rect(rect.x + 24f, rect.y + 20f, rect.width - 48f, rect.height - 40f));
            try
            {
                content();
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        void DrawError(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                GUILayout.Label(message, m_Error);
            }
        }

        static void DrawColorChip(int seat)
        {
            Color previous = GUI.color;
            GUI.color = AvatarVisuals.SeatColor(seat);
            GUILayout.Box(Texture2D.whiteTexture, GUIStyle.none, GUILayout.Width(16f), GUILayout.Height(28f));
            GUI.color = previous;
        }

        void EnsureStyles()
        {
            if (m_StylesReady)
            {
                return;
            }

            m_StylesReady = true;
            if (m_Font != null)
            {
                GUI.skin.font = m_Font;
            }

            m_Label = new GUIStyle(GUI.skin.label) { fontSize = 22, richText = false, wordWrap = true };
            m_Small = new GUIStyle(m_Label) { fontSize = 17 };
            m_Title = new GUIStyle(m_Label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            m_Big = new GUIStyle(m_Label) { fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            m_Center = new GUIStyle(m_Label) { alignment = TextAnchor.MiddleCenter };
            m_Error = new GUIStyle(m_Small);
            m_Error.normal.textColor = new Color(1f, 0.55f, 0.5f);
            m_Button = new GUIStyle(GUI.skin.button) { fontSize = 20, richText = false };
            m_Field = new GUIStyle(GUI.skin.textField) { fontSize = 22, richText = false, alignment = TextAnchor.MiddleLeft };
            m_Box = new GUIStyle(GUI.skin.box);
            m_Box.normal.background = MakeTexture(new Color(0.08f, 0.06f, 0.08f, 0.82f));
        }

        Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            m_OwnedTextures.Add(texture);
            return texture;
        }

        readonly List<Texture2D> m_OwnedTextures = new List<Texture2D>();

        void OnDestroy()
        {
            foreach (Texture2D texture in m_OwnedTextures)
            {
                if (texture != null)
                {
                    Destroy(texture);
                }
            }

            m_OwnedTextures.Clear();
            if (m_Font != null)
            {
                Destroy(m_Font);
            }
        }
    }
}
