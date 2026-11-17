using System;
using System.Collections.Generic;

namespace KimchiBreaker.Core
{
    /// <summary>매치에 참가한 플레이어 한 명의 상태. 값 변경은 <see cref="MatchEngine"/> 만 한다.</summary>
    public sealed class PlayerRecord
    {
        internal PlayerRecord(ulong clientId, int seat, string name)
        {
            ClientId = clientId;
            Seat = seat;
            Name = name;
        }

        public ulong ClientId { get; }

        /// <summary>좌석 번호(0~3).</summary>
        public int Seat { get; }

        public string Name { get; }

        public bool Ready { get; internal set; }

        /// <summary>이번 매치 누적 승수.</summary>
        public int Wins { get; internal set; }

        /// <summary>이번 라운드에 획득한 김치 수.</summary>
        public int RoundKimchi { get; internal set; }

        /// <summary>현재 김치 수에 도달한 순서 번호. 동점일 때 먼저 도달한 사람을 가린다.</summary>
        public long ScoreStamp { get; internal set; }

        /// <summary>이번 라운드에 장착된 스킬(추첨 당첨자만 가진다).</summary>
        public SkillId Skill { get; internal set; }

        /// <summary>액티브 스킬을 이번 라운드에 이미 썼는지.</summary>
        public bool ActiveSkillUsed { get; internal set; }

        /// <summary>직전 정산에서 받은 번호표의 첫 번호(1부터). 없으면 0.</summary>
        public int TicketStart { get; internal set; }

        /// <summary>직전 정산에서 받은 번호표 장수.</summary>
        public int TicketCount { get; internal set; }
    }

    public enum JoinError : byte
    {
        None = 0,
        RoomFull = 1,
        MatchInProgress = 2,
        AlreadyJoined = 3,
    }

    public enum StartError : byte
    {
        None = 0,
        NotHost = 1,
        NotInLobby = 2,
        NotEnoughPlayers = 3,
        PlayersNotReady = 4,
    }

    /// <summary>라운드 승자를 정하는 규칙. 특수 승리 조건을 추가할 때 새 구현을 만든다.</summary>
    public interface IRoundWinRule
    {
        /// <summary>라운드 승자의 ClientId. 승자가 없으면 <see cref="GameRules.NoPlayer"/>.</summary>
        ulong DetermineWinner(IReadOnlyList<PlayerRecord> players);
    }

    /// <summary>기본 규칙: 김치를 가장 많이 모은 사람이 승리. 동점이면 먼저 그 개수에 도달한 사람.</summary>
    public sealed class MostKimchiWinRule : IRoundWinRule
    {
        public ulong DetermineWinner(IReadOnlyList<PlayerRecord> players)
        {
            PlayerRecord best = null;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerRecord player = players[i];
                if (player.RoundKimchi <= 0)
                {
                    continue;
                }

                if (best == null ||
                    player.RoundKimchi > best.RoundKimchi ||
                    (player.RoundKimchi == best.RoundKimchi && player.ScoreStamp < best.ScoreStamp))
                {
                    best = player;
                }
            }

            return best == null ? GameRules.NoPlayer : best.ClientId;
        }
    }

    /// <summary>
    /// 매치 진행 규칙 전체를 담당하는 상태 머신. 호스트에서만 실행된다.
    /// Unity/네트워크에 의존하지 않으며 시간은 바깥에서 넘겨받는다(단위: 초).
    /// 모든 공개 메서드는 잘못된 요청을 예외 없이 거절(false/오류 코드)한다.
    /// </summary>
    public sealed class MatchEngine
    {
        readonly ulong m_HostClientId;
        readonly MatchTimings m_Timings;
        readonly IRandomSource m_Random;
        readonly IRoundWinRule m_WinRule;
        readonly int m_MinPlayersToStart;
        readonly List<PlayerRecord> m_Players = new List<PlayerRecord>(GameRules.MaxPlayers);
        long m_ScoreCounter;

        /// <param name="hostClientId">방장의 ClientId.</param>
        /// <param name="minPlayersToStart">시작에 필요한 최소 인원. 혼자 테스트할 때만 1로 낮춘다.</param>
        public MatchEngine(
            ulong hostClientId,
            MatchTimings timings,
            IRandomSource random,
            IRoundWinRule winRule = null,
            int minPlayersToStart = GameRules.MinPlayersToStart)
        {
            m_HostClientId = hostClientId;
            m_Timings = (timings ?? new MatchTimings()).Sanitized();
            m_Random = random ?? throw new ArgumentNullException(nameof(random));
            m_WinRule = winRule ?? new MostKimchiWinRule();
            m_MinPlayersToStart = Math.Max(1, Math.Min(GameRules.MaxPlayers, minPlayersToStart));
            Settings = MatchSettings.Default;
            ResetOutcome();
            PhaseEndTime = double.PositiveInfinity;
        }

        // ------------------------------------------------------------------ 상태

        public MatchPhase Phase { get; private set; } = MatchPhase.Lobby;

        /// <summary>현재 단계가 끝나는 시각. 시간 제한이 없는 단계(대기실, 피날레)는 +무한대.</summary>
        public double PhaseEndTime { get; private set; }

        /// <summary>현재 단계가 시작된 시각.</summary>
        public double PhaseStartTime { get; private set; }

        public MatchSettings Settings { get; private set; }

        /// <summary>현재 라운드 번호(1부터). 대기실에서는 0.</summary>
        public int RoundNumber { get; private set; }

        /// <summary>좌석 순서로 정렬된 참가자 목록.</summary>
        public IReadOnlyList<PlayerRecord> Players => m_Players;

        public ulong HostClientId => m_HostClientId;

        /// <summary>직전 정산의 라운드 승자.</summary>
        public ulong RoundWinnerId { get; private set; }

        /// <summary>직전 정산에서 장독대에 들어간 번호표 총 장수.</summary>
        public int TotalTickets { get; private set; }

        /// <summary>추첨으로 뽑힌 번호(1부터). 추첨이 없었으면 0.</summary>
        public int WinningTicket { get; private set; }

        public ulong LotteryWinnerId { get; private set; }

        /// <summary>추첨 당첨자가 다음 라운드에 받을 스킬.</summary>
        public SkillId LotterySkill { get; private set; }

        /// <summary>매치 최종 승자. 정해지지 않았으면 <see cref="GameRules.NoPlayer"/>.</summary>
        public ulong MatchWinnerId { get; private set; }

        /// <summary>마지막으로 대기실로 돌아온 이유.</summary>
        public MatchEndReason LastEndReason { get; private set; }

        /// <summary>상태가 바뀔 때마다 1씩 증가한다. 네트워크 계층이 변경 여부를 판단하는 데 쓴다.</summary>
        public uint Revision { get; private set; }

        /// <summary>단계가 바뀐 직후 호출된다. (이전 단계, 새 단계)</summary>
        public event Action<MatchPhase, MatchPhase> PhaseChanged;

        // ------------------------------------------------------------------ 조회

        public PlayerRecord FindPlayer(ulong clientId)
        {
            for (int i = 0; i < m_Players.Count; i++)
            {
                if (m_Players[i].ClientId == clientId)
                {
                    return m_Players[i];
                }
            }

            return null;
        }

        /// <summary>지금 새 플레이어가 들어올 수 있는지 확인한다(접속 승인 단계에서 사용).</summary>
        public JoinError CheckJoin(ulong clientId)
        {
            if (FindPlayer(clientId) != null)
            {
                return JoinError.AlreadyJoined;
            }

            if (Phase != MatchPhase.Lobby)
            {
                return JoinError.MatchInProgress;
            }

            return m_Players.Count >= GameRules.MaxPlayers ? JoinError.RoomFull : JoinError.None;
        }

        /// <summary>현재 단계의 남은 시간(초). 시간 제한이 없으면 +무한대.</summary>
        public double GetPhaseRemaining(double now)
        {
            if (double.IsPositiveInfinity(PhaseEndTime))
            {
                return double.PositiveInfinity;
            }

            return Math.Max(0.0, PhaseEndTime - now);
        }

        // ------------------------------------------------------------------ 대기실

        /// <summary>플레이어를 추가하고 가장 낮은 번호의 빈 좌석에 앉힌다.</summary>
        public JoinError TryAddPlayer(ulong clientId, string rawName, out PlayerRecord record)
        {
            record = null;
            JoinError error = CheckJoin(clientId);
            if (error != JoinError.None)
            {
                return error;
            }

            int seat = FindFreeSeat();
            if (seat == GameRules.NoSeat)
            {
                return JoinError.RoomFull;
            }

            string name = TextSanitizer.SanitizeNickname(rawName, "플레이어" + (seat + 1));
            record = new PlayerRecord(clientId, seat, name)
            {
                Ready = clientId == m_HostClientId, // 방장은 항상 준비 완료로 취급
            };

            int index = 0;
            while (index < m_Players.Count && m_Players[index].Seat < seat)
            {
                index++;
            }

            m_Players.Insert(index, record);
            Touch();
            return JoinError.None;
        }

        /// <summary>
        /// 플레이어를 제거한다(접속 종료). 진행 중 인원이 최소 인원보다 적어지면 대기실로 돌아간다.
        /// </summary>
        public bool RemovePlayer(ulong clientId, double now)
        {
            PlayerRecord player = FindPlayer(clientId);
            if (player == null)
            {
                return false;
            }

            m_Players.Remove(player);
            Touch();

            if (Phase != MatchPhase.Lobby && m_Players.Count < m_MinPlayersToStart)
            {
                ReturnToLobby(MatchEndReason.NotEnoughPlayers, now);
            }

            return true;
        }

        public bool TrySetReady(ulong clientId, bool ready)
        {
            PlayerRecord player = FindPlayer(clientId);
            if (player == null || Phase != MatchPhase.Lobby || clientId == m_HostClientId)
            {
                return false;
            }

            if (player.Ready != ready)
            {
                player.Ready = ready;
                Touch();
            }

            return true;
        }

        /// <summary>방장만, 대기실에서만 설정을 바꿀 수 있다. 범위를 벗어난 값은 거절한다.</summary>
        public bool TrySetSettings(ulong requester, MatchSettings settings)
        {
            if (requester != m_HostClientId || Phase != MatchPhase.Lobby || !settings.IsValid)
            {
                return false;
            }

            if (Settings != settings)
            {
                Settings = settings;
                Touch();
            }

            return true;
        }

        public StartError CheckStart(ulong requester)
        {
            if (requester != m_HostClientId)
            {
                return StartError.NotHost;
            }

            if (Phase != MatchPhase.Lobby)
            {
                return StartError.NotInLobby;
            }

            if (m_Players.Count < m_MinPlayersToStart)
            {
                return StartError.NotEnoughPlayers;
            }

            for (int i = 0; i < m_Players.Count; i++)
            {
                if (!m_Players[i].Ready)
                {
                    return StartError.PlayersNotReady;
                }
            }

            return StartError.None;
        }

        public StartError TryStartMatch(ulong requester, double now)
        {
            StartError error = CheckStart(requester);
            if (error != StartError.None)
            {
                return error;
            }

            for (int i = 0; i < m_Players.Count; i++)
            {
                PlayerRecord player = m_Players[i];
                player.Wins = 0;
                ClearRoundState(player);
            }

            m_ScoreCounter = 0;
            RoundNumber = 1;
            LastEndReason = MatchEndReason.None;
            ResetOutcome();
            EnterPhase(MatchPhase.RoundIntro, now, m_Timings.RoundIntroSeconds);
            return StartError.None;
        }

        // ------------------------------------------------------------------ 라운드

        /// <summary>
        /// 플레이어에게 김치를 지급한다. 재료 완성 판정을 내린 서버 코드만 호출해야 한다.
        /// 라운드 진행 중이 아니거나 수량이 범위를 벗어나면 거절한다.
        /// </summary>
        public bool TryAwardKimchi(ulong clientId, int amount)
        {
            if (Phase != MatchPhase.RoundPlaying || amount < 1 || amount > GameRules.MaxKimchiPerAward)
            {
                return false;
            }

            PlayerRecord player = FindPlayer(clientId);
            if (player == null || player.RoundKimchi >= GameRules.MaxKimchiPerRound)
            {
                return false;
            }

            player.RoundKimchi = Math.Min(GameRules.MaxKimchiPerRound, player.RoundKimchi + amount);
            player.ScoreStamp = ++m_ScoreCounter;
            Touch();
            return true;
        }

        /// <summary>
        /// 액티브 스킬 사용을 시도한다. 라운드 진행 중이고, 액티브 스킬을 갖고 있고, 아직 안 썼을 때만 성공.
        /// </summary>
        public bool TryUseActiveSkill(ulong clientId, out SkillId skill)
        {
            skill = SkillId.None;
            if (Phase != MatchPhase.RoundPlaying)
            {
                return false;
            }

            PlayerRecord player = FindPlayer(clientId);
            if (player == null || player.ActiveSkillUsed || !SkillCatalog.IsActive(player.Skill))
            {
                return false;
            }

            player.ActiveSkillUsed = true;
            skill = player.Skill;
            Touch();
            return true;
        }

        /// <summary>피날레에서 방장이 대기실 복귀를 선택한다. 최소 연출 시간이 지나야 한다.</summary>
        public bool TryReturnToLobby(ulong requester, double now)
        {
            if (requester != m_HostClientId || Phase != MatchPhase.Finale)
            {
                return false;
            }

            if (now < PhaseStartTime + m_Timings.FinaleMinSeconds)
            {
                return false;
            }

            ReturnToLobby(MatchEndReason.HostReturnedToLobby, now);
            return true;
        }

        /// <summary>방장이 진행 중인 매치를 중단하고 대기실로 돌아간다.</summary>
        public bool TryAbortMatch(ulong requester, double now)
        {
            if (requester != m_HostClientId || Phase == MatchPhase.Lobby)
            {
                return false;
            }

            ReturnToLobby(MatchEndReason.HostAborted, now);
            return true;
        }

        /// <summary>
        /// 시간 경과를 반영한다. 호스트가 매 프레임(또는 매 틱) 호출한다.
        /// 한 번 호출에 최대 한 단계만 넘어간다. 그래야 모든 클라이언트가 모든 단계를 최소 한 번은 받는다.
        /// </summary>
        /// <returns>단계가 바뀌었으면 true.</returns>
        public bool Tick(double now)
        {
            if (double.IsNaN(now) || Phase == MatchPhase.Lobby || Phase == MatchPhase.Finale || now < PhaseEndTime)
            {
                return false;
            }

            Advance(now);
            return true;
        }

        // ------------------------------------------------------------------ 내부

        void Advance(double now)
        {
            switch (Phase)
            {
                case MatchPhase.RoundIntro:
                    EnterPhase(MatchPhase.RoundPlaying, now, Settings.RoundSeconds);
                    break;

                case MatchPhase.RoundPlaying:
                    SettleRound();
                    EnterPhase(MatchPhase.RoundSettlement, now, m_Timings.SettlementSeconds);
                    break;

                case MatchPhase.RoundSettlement:
                    DrawLottery();
                    EnterPhase(MatchPhase.Lottery, now, m_Timings.LotterySeconds);
                    break;

                case MatchPhase.Lottery:
                    EnterPhase(MatchPhase.RoundResult, now, m_Timings.ResultSeconds);
                    break;

                case MatchPhase.RoundResult:
                    if (MatchWinnerId != GameRules.NoPlayer)
                    {
                        EnterPhase(MatchPhase.Finale, now, double.PositiveInfinity);
                    }
                    else
                    {
                        BeginNextRound();
                        EnterPhase(MatchPhase.RoundIntro, now, m_Timings.RoundIntroSeconds);
                    }

                    break;
            }
        }

        void SettleRound()
        {
            ulong winnerId = m_WinRule.DetermineWinner(m_Players);
            PlayerRecord winner = winnerId == GameRules.NoPlayer ? null : FindPlayer(winnerId);
            RoundWinnerId = winner == null ? GameRules.NoPlayer : winner.ClientId;
            if (winner != null)
            {
                winner.Wins++;
                if (winner.Wins >= Settings.TargetWins)
                {
                    MatchWinnerId = winner.ClientId;
                }
            }

            // 번호표는 좌석 순서대로 연속된 번호 구간을 받는다. 김치 1개 = 번호표 1장.
            int next = 1;
            for (int i = 0; i < m_Players.Count; i++)
            {
                PlayerRecord player = m_Players[i];
                player.TicketCount = player.RoundKimchi;
                player.TicketStart = player.TicketCount > 0 ? next : 0;
                next += player.TicketCount;
            }

            TotalTickets = next - 1;
        }

        void DrawLottery()
        {
            WinningTicket = 0;
            LotteryWinnerId = GameRules.NoPlayer;
            LotterySkill = SkillId.None;

            // 정산 이후 나간 플레이어의 번호표는 제외하고, 남아 있는 번호표 중에서 뽑는다.
            int present = 0;
            for (int i = 0; i < m_Players.Count; i++)
            {
                present += m_Players[i].TicketCount;
            }

            if (present <= 0)
            {
                return;
            }

            int pick = m_Random.NextInt(present);
            for (int i = 0; i < m_Players.Count; i++)
            {
                PlayerRecord player = m_Players[i];
                if (pick < player.TicketCount)
                {
                    WinningTicket = player.TicketStart + pick;
                    LotteryWinnerId = player.ClientId;
                    break;
                }

                pick -= player.TicketCount;
            }

            if (LotteryWinnerId == GameRules.NoPlayer)
            {
                return; // 난수 공급원이 범위를 벗어난 값을 준 경우에만 도달한다
            }

            IReadOnlyList<SkillId> skills = SkillCatalog.All;
            int skillIndex = m_Random.NextInt(skills.Count);
            if (skillIndex < 0 || skillIndex >= skills.Count)
            {
                skillIndex = 0;
            }

            LotterySkill = skills[skillIndex];
        }

        void BeginNextRound()
        {
            ulong skillOwner = LotteryWinnerId;
            SkillId skill = LotterySkill;
            for (int i = 0; i < m_Players.Count; i++)
            {
                PlayerRecord player = m_Players[i];
                ClearRoundState(player);
                if (player.ClientId == skillOwner)
                {
                    player.Skill = skill;
                }
            }

            m_ScoreCounter = 0;
            RoundNumber++;
            ResetOutcome();
        }

        void ReturnToLobby(MatchEndReason reason, double now)
        {
            for (int i = 0; i < m_Players.Count; i++)
            {
                PlayerRecord player = m_Players[i];
                player.Wins = 0;
                player.Ready = player.ClientId == m_HostClientId;
                ClearRoundState(player);
            }

            RoundNumber = 0;
            LastEndReason = reason;
            ResetOutcome();
            EnterPhase(MatchPhase.Lobby, double.IsNaN(now) ? PhaseStartTime : now, double.PositiveInfinity);
        }

        static void ClearRoundState(PlayerRecord player)
        {
            player.RoundKimchi = 0;
            player.ScoreStamp = 0;
            player.Skill = SkillId.None;
            player.ActiveSkillUsed = false;
            player.TicketStart = 0;
            player.TicketCount = 0;
        }

        void ResetOutcome()
        {
            RoundWinnerId = GameRules.NoPlayer;
            TotalTickets = 0;
            WinningTicket = 0;
            LotteryWinnerId = GameRules.NoPlayer;
            LotterySkill = SkillId.None;
            MatchWinnerId = GameRules.NoPlayer;
        }

        void EnterPhase(MatchPhase next, double now, double duration)
        {
            MatchPhase previous = Phase;
            Phase = next;
            PhaseStartTime = now;
            PhaseEndTime = double.IsPositiveInfinity(duration) ? double.PositiveInfinity : now + duration;
            Touch();
            if (previous != next)
            {
                PhaseChanged?.Invoke(previous, next);
            }
        }

        int FindFreeSeat()
        {
            for (int seat = 0; seat < GameRules.MaxPlayers; seat++)
            {
                bool taken = false;
                for (int i = 0; i < m_Players.Count; i++)
                {
                    if (m_Players[i].Seat == seat)
                    {
                        taken = true;
                        break;
                    }
                }

                if (!taken)
                {
                    return seat;
                }
            }

            return GameRules.NoSeat;
        }

        void Touch()
        {
            unchecked
            {
                Revision++;
            }
        }
    }
}
