using System.Collections.Generic;
using KimchiBreaker.Core;
using NUnit.Framework;

namespace KimchiBreaker.Tests
{
    public class MatchEngineTests
    {
        const ulong Host = 0;
        const ulong P1 = 11;
        const ulong P2 = 22;
        const ulong P3 = 33;

        ScriptedRandom m_Random;
        MatchTimings m_Timings;
        double m_Now;

        [SetUp]
        public void SetUp()
        {
            m_Random = new ScriptedRandom();
            m_Timings = new MatchTimings
            {
                RoundIntroSeconds = 4,
                SettlementSeconds = 4,
                LotterySeconds = 7,
                ResultSeconds = 6,
                FinaleMinSeconds = 5,
            };
            m_Now = 100.0;
        }

        MatchEngine NewEngine(int minPlayers = GameRules.MinPlayersToStart)
        {
            return new MatchEngine(Host, m_Timings, m_Random, null, minPlayers);
        }

        MatchEngine NewLobby(params ulong[] others)
        {
            MatchEngine engine = NewEngine();
            Assert.AreEqual(JoinError.None, engine.TryAddPlayer(Host, "방장", out _));
            foreach (ulong id in others)
            {
                Assert.AreEqual(JoinError.None, engine.TryAddPlayer(id, "손님" + id, out _));
                Assert.IsTrue(engine.TrySetReady(id, true));
            }

            return engine;
        }

        /// <summary>시간을 흘려 현재 단계를 끝낸다.</summary>
        void FinishPhase(MatchEngine engine)
        {
            m_Now = engine.PhaseEndTime;
            engine.Tick(m_Now);
        }

        void StartPlaying(MatchEngine engine)
        {
            Assert.AreEqual(StartError.None, engine.TryStartMatch(Host, m_Now));
            Assert.AreEqual(MatchPhase.RoundIntro, engine.Phase);
            FinishPhase(engine);
            Assert.AreEqual(MatchPhase.RoundPlaying, engine.Phase);
        }

        /// <summary>진행 중인 라운드를 끝내고 다음 라운드(또는 피날레)까지 넘긴다.</summary>
        void FinishRound(MatchEngine engine)
        {
            Assert.AreEqual(MatchPhase.RoundPlaying, engine.Phase);
            FinishPhase(engine);
            Assert.AreEqual(MatchPhase.RoundSettlement, engine.Phase);
            FinishPhase(engine);
            Assert.AreEqual(MatchPhase.Lottery, engine.Phase);
            FinishPhase(engine);
            Assert.AreEqual(MatchPhase.RoundResult, engine.Phase);
            FinishPhase(engine);
        }

        // ---------------------------------------------------------------- 대기실

        [Test]
        public void NewEngine_StartsInLobbyWithDefaults()
        {
            MatchEngine engine = NewEngine();
            Assert.AreEqual(MatchPhase.Lobby, engine.Phase);
            Assert.AreEqual(MatchSettings.Default, engine.Settings);
            Assert.AreEqual(0, engine.Players.Count);
            Assert.AreEqual(0, engine.RoundNumber);
            Assert.AreEqual(GameRules.NoPlayer, engine.MatchWinnerId);
            Assert.IsTrue(double.IsPositiveInfinity(engine.PhaseEndTime));
        }

        [Test]
        public void Players_AreSeatedInJoinOrder()
        {
            MatchEngine engine = NewLobby(P1, P2, P3);
            Assert.AreEqual(4, engine.Players.Count);
            Assert.AreEqual(0, engine.FindPlayer(Host).Seat);
            Assert.AreEqual(1, engine.FindPlayer(P1).Seat);
            Assert.AreEqual(2, engine.FindPlayer(P2).Seat);
            Assert.AreEqual(3, engine.FindPlayer(P3).Seat);
        }

        [Test]
        public void FifthPlayer_IsRejected()
        {
            MatchEngine engine = NewLobby(P1, P2, P3);
            Assert.AreEqual(JoinError.RoomFull, engine.CheckJoin(44));
            Assert.AreEqual(JoinError.RoomFull, engine.TryAddPlayer(44, "늦은손님", out PlayerRecord record));
            Assert.IsNull(record);
            Assert.AreEqual(4, engine.Players.Count);
        }

        [Test]
        public void SameClient_CannotJoinTwice()
        {
            MatchEngine engine = NewLobby(P1);
            Assert.AreEqual(JoinError.AlreadyJoined, engine.TryAddPlayer(P1, "복제", out _));
            Assert.AreEqual(2, engine.Players.Count);
        }

        [Test]
        public void FreedSeat_IsReusedByNextPlayer_AndListStaysSorted()
        {
            MatchEngine engine = NewLobby(P1, P2, P3);
            Assert.IsTrue(engine.RemovePlayer(P1, m_Now));
            Assert.AreEqual(JoinError.None, engine.TryAddPlayer(44, "새손님", out PlayerRecord record));
            Assert.AreEqual(1, record.Seat);
            for (int i = 0; i < engine.Players.Count; i++)
            {
                Assert.AreEqual(i, engine.Players[i].Seat);
            }
        }

        [Test]
        public void RemoveUnknownPlayer_ReturnsFalse()
        {
            MatchEngine engine = NewLobby(P1);
            Assert.IsFalse(engine.RemovePlayer(999, m_Now));
        }

        [Test]
        public void Nickname_IsSanitized_AndFallsBackToSeatName()
        {
            MatchEngine engine = NewEngine();
            engine.TryAddPlayer(Host, "<color=red>방장</color>", out PlayerRecord host);
            Assert.AreEqual("color=red방장/", host.Name);
            engine.TryAddPlayer(P1, "   ", out PlayerRecord guest);
            Assert.AreEqual("플레이어2", guest.Name);
            engine.TryAddPlayer(P2, null, out PlayerRecord guest2);
            Assert.AreEqual("플레이어3", guest2.Name);
        }

        [Test]
        public void Host_IsAlwaysReady_AndCannotToggle()
        {
            MatchEngine engine = NewLobby();
            Assert.IsTrue(engine.FindPlayer(Host).Ready);
            Assert.IsFalse(engine.TrySetReady(Host, false));
            Assert.IsTrue(engine.FindPlayer(Host).Ready);
        }

        [Test]
        public void Ready_CanBeToggledInLobbyOnly()
        {
            MatchEngine engine = NewLobby(P1);
            Assert.IsTrue(engine.TrySetReady(P1, false));
            Assert.IsFalse(engine.FindPlayer(P1).Ready);
            Assert.IsTrue(engine.TrySetReady(P1, true));
            Assert.IsFalse(engine.TrySetReady(999, true));
            StartPlaying(engine);
            Assert.IsFalse(engine.TrySetReady(P1, false));
        }

        [Test]
        public void Settings_OnlyHostInLobby_AndOnlyValidValues()
        {
            MatchEngine engine = NewLobby(P1);
            var custom = new MatchSettings(3, 60);
            Assert.IsFalse(engine.TrySetSettings(P1, custom), "손님은 설정을 바꿀 수 없다");
            Assert.AreEqual(MatchSettings.Default, engine.Settings);
            Assert.IsTrue(engine.TrySetSettings(Host, custom));
            Assert.AreEqual(custom, engine.Settings);

            Assert.IsFalse(engine.TrySetSettings(Host, new MatchSettings(0, 60)));
            Assert.IsFalse(engine.TrySetSettings(Host, new MatchSettings(GameRules.MaxTargetWins + 1, 60)));
            Assert.IsFalse(engine.TrySetSettings(Host, new MatchSettings(3, GameRules.MinRoundSeconds - 1)));
            Assert.IsFalse(engine.TrySetSettings(Host, new MatchSettings(3, GameRules.MaxRoundSeconds + 1)));
            Assert.IsFalse(engine.TrySetSettings(Host, new MatchSettings(int.MinValue, int.MaxValue)));
            Assert.AreEqual(custom, engine.Settings);

            StartPlaying(engine);
            Assert.IsFalse(engine.TrySetSettings(Host, MatchSettings.Default), "진행 중에는 바꿀 수 없다");
        }

        [Test]
        public void Start_RequiresHost()
        {
            MatchEngine engine = NewLobby(P1);
            Assert.AreEqual(StartError.NotHost, engine.TryStartMatch(P1, m_Now));
            Assert.AreEqual(MatchPhase.Lobby, engine.Phase);
        }

        [Test]
        public void Start_RequiresMinimumPlayers()
        {
            MatchEngine engine = NewLobby();
            Assert.AreEqual(StartError.NotEnoughPlayers, engine.TryStartMatch(Host, m_Now));
        }

        [Test]
        public void Start_RequiresEveryoneReady()
        {
            MatchEngine engine = NewLobby(P1, P2);
            engine.TrySetReady(P2, false);
            Assert.AreEqual(StartError.PlayersNotReady, engine.TryStartMatch(Host, m_Now));
            engine.TrySetReady(P2, true);
            Assert.AreEqual(StartError.None, engine.TryStartMatch(Host, m_Now));
        }

        [Test]
        public void Start_TwiceIsRejected()
        {
            MatchEngine engine = NewLobby(P1);
            Assert.AreEqual(StartError.None, engine.TryStartMatch(Host, m_Now));
            Assert.AreEqual(StartError.NotInLobby, engine.TryStartMatch(Host, m_Now));
        }

        [Test]
        public void SoloStart_AllowedOnlyWhenMinimumLowered()
        {
            MatchEngine solo = NewEngine(1);
            solo.TryAddPlayer(Host, "혼자", out _);
            Assert.AreEqual(StartError.None, solo.TryStartMatch(Host, m_Now));
        }

        [Test]
        public void Join_IsRejectedWhileMatchInProgress()
        {
            MatchEngine engine = NewLobby(P1);
            StartPlaying(engine);
            Assert.AreEqual(JoinError.MatchInProgress, engine.CheckJoin(P2));
            Assert.AreEqual(JoinError.MatchInProgress, engine.TryAddPlayer(P2, "난입", out _));
        }

        // ---------------------------------------------------------------- 단계 진행

        [Test]
        public void Phases_FollowTheSpecOrderWithConfiguredDurations()
        {
            MatchEngine engine = NewLobby(P1);
            engine.TrySetSettings(Host, new MatchSettings(1, 60));
            var seen = new List<MatchPhase>();
            engine.PhaseChanged += (previous, next) => seen.Add(next);

            engine.TryStartMatch(Host, m_Now);
            Assert.AreEqual(1, engine.RoundNumber);
            Assert.AreEqual(m_Now + 4, engine.PhaseEndTime, 1e-9);

            FinishPhase(engine);
            Assert.AreEqual(m_Now + 60, engine.PhaseEndTime, 1e-9);
            engine.TryAwardKimchi(P1, 1);

            FinishPhase(engine);
            Assert.AreEqual(m_Now + 4, engine.PhaseEndTime, 1e-9);
            FinishPhase(engine);
            Assert.AreEqual(m_Now + 7, engine.PhaseEndTime, 1e-9);
            FinishPhase(engine);
            Assert.AreEqual(m_Now + 6, engine.PhaseEndTime, 1e-9);
            FinishPhase(engine);

            CollectionAssert.AreEqual(
                new[]
                {
                    MatchPhase.RoundIntro, MatchPhase.RoundPlaying, MatchPhase.RoundSettlement,
                    MatchPhase.Lottery, MatchPhase.RoundResult, MatchPhase.Finale,
                },
                seen);
            Assert.IsTrue(double.IsPositiveInfinity(engine.PhaseEndTime));
        }

        [Test]
        public void Tick_BeforeDeadline_DoesNothing()
        {
            MatchEngine engine = NewLobby(P1);
            engine.TryStartMatch(Host, m_Now);
            engine.Tick(m_Now + 3.999);
            Assert.AreEqual(MatchPhase.RoundIntro, engine.Phase);
            engine.Tick(m_Now + 4.0);
            Assert.AreEqual(MatchPhase.RoundPlaying, engine.Phase);
        }

        [Test]
        public void Tick_InLobby_DoesNothing()
        {
            MatchEngine engine = NewLobby(P1);
            uint revision = engine.Revision;
            engine.Tick(1e12);
            Assert.AreEqual(MatchPhase.Lobby, engine.Phase);
            Assert.AreEqual(revision, engine.Revision);
        }

        [Test]
        public void Tick_WithNaN_IsIgnored()
        {
            MatchEngine engine = NewLobby(P1);
            engine.TryStartMatch(Host, m_Now);
            engine.Tick(double.NaN);
            Assert.AreEqual(MatchPhase.RoundIntro, engine.Phase);
        }

        [Test]
        public void Tick_AfterLongFreeze_DoesNotSkipTheWholeRound()
        {
            MatchEngine engine = NewLobby(P1);
            engine.TryStartMatch(Host, m_Now);
            // 호스트가 아주 오래 멈췄다 돌아와도 새 단계의 길이는 "지금"부터 다시 센다.
            engine.Tick(m_Now + 100000);
            Assert.AreEqual(MatchPhase.RoundPlaying, engine.Phase);
            Assert.AreEqual(m_Now + 100000 + engine.Settings.RoundSeconds, engine.PhaseEndTime, 1e-6);
        }

        [Test]
        public void Tick_AdvancesAtMostOnePhasePerCall_EvenWithZeroDurations()
        {
            m_Timings = new MatchTimings { RoundIntroSeconds = 0, SettlementSeconds = 0, LotterySeconds = 0, ResultSeconds = 0 };
            MatchEngine engine = NewLobby(P1);
            engine.TryStartMatch(Host, m_Now);
            Assert.AreEqual(MatchPhase.RoundIntro, engine.Phase);
            Assert.IsTrue(engine.Tick(m_Now));
            Assert.AreEqual(MatchPhase.RoundPlaying, engine.Phase);
            m_Now = engine.PhaseEndTime;
            Assert.IsTrue(engine.Tick(m_Now));
            Assert.AreEqual(MatchPhase.RoundSettlement, engine.Phase);
            Assert.IsTrue(engine.Tick(m_Now));
            Assert.AreEqual(MatchPhase.Lottery, engine.Phase);
            Assert.IsTrue(engine.Tick(m_Now));
            Assert.AreEqual(MatchPhase.RoundResult, engine.Phase);
            Assert.IsTrue(engine.Tick(m_Now));
            Assert.AreEqual(MatchPhase.RoundIntro, engine.Phase);
            Assert.AreEqual(2, engine.RoundNumber);
        }

        [Test]
        public void Tick_ReturnsFalseWhenNothingChanges()
        {
            MatchEngine engine = NewLobby(P1);
            Assert.IsFalse(engine.Tick(m_Now));
            engine.TryStartMatch(Host, m_Now);
            Assert.IsFalse(engine.Tick(m_Now + 1));
        }

        [Test]
        public void GetPhaseRemaining_CountsDownAndNeverGoesNegative()
        {
            MatchEngine engine = NewLobby(P1);
            Assert.IsTrue(double.IsPositiveInfinity(engine.GetPhaseRemaining(m_Now)));
            engine.TryStartMatch(Host, m_Now);
            Assert.AreEqual(4.0, engine.GetPhaseRemaining(m_Now), 1e-9);
            Assert.AreEqual(1.5, engine.GetPhaseRemaining(m_Now + 2.5), 1e-9);
            Assert.AreEqual(0.0, engine.GetPhaseRemaining(m_Now + 50), 1e-9);
        }

        // ---------------------------------------------------------------- 점수

        [Test]
        public void Award_OnlyWhileRoundIsPlaying()
        {
            MatchEngine engine = NewLobby(P1);
            Assert.IsFalse(engine.TryAwardKimchi(P1, 1), "대기실");
            engine.TryStartMatch(Host, m_Now);
            Assert.IsFalse(engine.TryAwardKimchi(P1, 1), "라운드 시작 연출 중");
            FinishPhase(engine);
            Assert.IsTrue(engine.TryAwardKimchi(P1, 1));
            FinishPhase(engine);
            Assert.IsFalse(engine.TryAwardKimchi(P1, 1), "정산 중");
            Assert.AreEqual(1, engine.FindPlayer(P1).RoundKimchi);
        }

        [Test]
        public void Award_RejectsInvalidAmountsAndUnknownPlayers()
        {
            MatchEngine engine = NewLobby(P1);
            StartPlaying(engine);
            Assert.IsFalse(engine.TryAwardKimchi(P1, 0));
            Assert.IsFalse(engine.TryAwardKimchi(P1, -5));
            Assert.IsFalse(engine.TryAwardKimchi(P1, GameRules.MaxKimchiPerAward + 1));
            Assert.IsFalse(engine.TryAwardKimchi(P1, int.MaxValue));
            Assert.IsFalse(engine.TryAwardKimchi(999, 1));
            Assert.AreEqual(0, engine.FindPlayer(P1).RoundKimchi);
            Assert.IsTrue(engine.TryAwardKimchi(P1, GameRules.MaxKimchiPerAward));
            Assert.AreEqual(GameRules.MaxKimchiPerAward, engine.FindPlayer(P1).RoundKimchi);
        }

        [Test]
        public void Award_IsCappedPerRound()
        {
            MatchEngine engine = NewLobby(P1);
            StartPlaying(engine);
            for (int i = 0; i < GameRules.MaxKimchiPerRound + 50; i++)
            {
                engine.TryAwardKimchi(P1, GameRules.MaxKimchiPerAward);
            }

            Assert.AreEqual(GameRules.MaxKimchiPerRound, engine.FindPlayer(P1).RoundKimchi);
        }

        [Test]
        public void MostKimchi_WinsTheRound()
        {
            MatchEngine engine = NewLobby(P1, P2);
            StartPlaying(engine);
            engine.TryAwardKimchi(Host, 1);
            engine.TryAwardKimchi(P1, 3);
            engine.TryAwardKimchi(P2, 2);
            FinishPhase(engine);
            Assert.AreEqual(P1, engine.RoundWinnerId);
            Assert.AreEqual(1, engine.FindPlayer(P1).Wins);
            Assert.AreEqual(0, engine.FindPlayer(Host).Wins);
            Assert.AreEqual(0, engine.FindPlayer(P2).Wins);
        }

        [Test]
        public void Tie_GoesToWhoeverReachedTheScoreFirst()
        {
            MatchEngine engine = NewLobby(P1, P2);
            StartPlaying(engine);
            engine.TryAwardKimchi(P2, 2); // P2 가 먼저 2개 도달
            engine.TryAwardKimchi(P1, 1);
            engine.TryAwardKimchi(P1, 1); // P1 은 나중에 2개 도달
            FinishPhase(engine);
            Assert.AreEqual(P2, engine.RoundWinnerId);
        }

        [Test]
        public void NobodyScored_NobodyWins()
        {
            MatchEngine engine = NewLobby(P1);
            StartPlaying(engine);
            FinishPhase(engine);
            Assert.AreEqual(GameRules.NoPlayer, engine.RoundWinnerId);
            Assert.AreEqual(0, engine.FindPlayer(Host).Wins);
            Assert.AreEqual(0, engine.FindPlayer(P1).Wins);
            Assert.AreEqual(0, engine.TotalTickets);
        }

        // ---------------------------------------------------------------- 번호표와 추첨

        [Test]
        public void Tickets_AreContiguousBySeat_AndMatchKimchiCounts()
        {
            MatchEngine engine = NewLobby(P1, P2, P3);
            StartPlaying(engine);
            engine.TryAwardKimchi(Host, 2);
            engine.TryAwardKimchi(P2, 3);
            engine.TryAwardKimchi(P3, 1);
            FinishPhase(engine);

            Assert.AreEqual(6, engine.TotalTickets);
            Assert.AreEqual(1, engine.FindPlayer(Host).TicketStart);
            Assert.AreEqual(2, engine.FindPlayer(Host).TicketCount);
            Assert.AreEqual(0, engine.FindPlayer(P1).TicketStart, "김치가 없으면 번호표도 없다");
            Assert.AreEqual(0, engine.FindPlayer(P1).TicketCount);
            Assert.AreEqual(3, engine.FindPlayer(P2).TicketStart);
            Assert.AreEqual(3, engine.FindPlayer(P2).TicketCount);
            Assert.AreEqual(6, engine.FindPlayer(P3).TicketStart);
            Assert.AreEqual(1, engine.FindPlayer(P3).TicketCount);
        }

        [Test]
        public void Lottery_PicksTheOwnerOfTheDrawnTicket()
        {
            MatchEngine engine = NewLobby(P1, P2);
            StartPlaying(engine);
            engine.TryAwardKimchi(Host, 2); // 번호 1~2
            engine.TryAwardKimchi(P1, 3);   // 번호 3~5
            engine.TryAwardKimchi(P2, 1);   // 번호 6
            FinishPhase(engine);

            m_Random.Enqueue(3); // 0부터 세어 3 → 4번 번호표
            m_Random.Enqueue(6); // 스킬 목록의 7번째
            FinishPhase(engine);

            Assert.AreEqual(MatchPhase.Lottery, engine.Phase);
            Assert.AreEqual(4, engine.WinningTicket);
            Assert.AreEqual(P1, engine.LotteryWinnerId);
            Assert.AreEqual(SkillCatalog.All[6], engine.LotterySkill);
        }

        [Test]
        public void Lottery_FirstAndLastTicketsMapToTheRightOwners()
        {
            foreach (int pick in new[] { 0, 5 })
            {
                SetUp();
                MatchEngine engine = NewLobby(P1, P2);
                StartPlaying(engine);
                engine.TryAwardKimchi(Host, 2);
                engine.TryAwardKimchi(P1, 3);
                engine.TryAwardKimchi(P2, 1);
                FinishPhase(engine);
                m_Random.Enqueue(pick);
                FinishPhase(engine);
                Assert.AreEqual(pick + 1, engine.WinningTicket);
                Assert.AreEqual(pick == 0 ? Host : P2, engine.LotteryWinnerId);
            }
        }

        [Test]
        public void Lottery_IsSkippedWhenThereAreNoTickets()
        {
            MatchEngine engine = NewLobby(P1);
            StartPlaying(engine);
            FinishPhase(engine);
            FinishPhase(engine);
            Assert.AreEqual(MatchPhase.Lottery, engine.Phase);
            Assert.AreEqual(0, engine.WinningTicket);
            Assert.AreEqual(GameRules.NoPlayer, engine.LotteryWinnerId);
            Assert.AreEqual(SkillId.None, engine.LotterySkill);
            Assert.AreEqual(0, m_Random.Calls, "번호표가 없으면 난수를 뽑지 않는다");
        }

        [Test]
        public void Lottery_IgnoresTicketsOfPlayersWhoLeftAfterSettlement()
        {
            MatchEngine engine = NewLobby(P1, P2);
            StartPlaying(engine);
            engine.TryAwardKimchi(Host, 2); // 1~2
            engine.TryAwardKimchi(P1, 3);   // 3~5
            engine.TryAwardKimchi(P2, 1);   // 6
            FinishPhase(engine);
            engine.RemovePlayer(P1, m_Now);

            m_Random.Enqueue(2); // 남은 번호표 3장(1,2,6) 중 세 번째 → 6번
            FinishPhase(engine);
            Assert.AreEqual(6, engine.WinningTicket);
            Assert.AreEqual(P2, engine.LotteryWinnerId);
        }

        [Test]
        public void LotteryWinner_GetsTheSkillForTheNextRoundOnly()
        {
            MatchEngine engine = NewLobby(P1);
            StartPlaying(engine);
            engine.TryAwardKimchi(P1, 1);
            FinishPhase(engine);
            m_Random.Enqueue(0);
            m_Random.Enqueue(0); // FlameGloves
            FinishPhase(engine);
            Assert.AreEqual(SkillId.None, engine.FindPlayer(P1).Skill, "추첨 직후에는 아직 장착 전");
            FinishPhase(engine);
            FinishPhase(engine);

            Assert.AreEqual(MatchPhase.RoundIntro, engine.Phase);
            Assert.AreEqual(2, engine.RoundNumber);
            Assert.AreEqual(SkillId.FlameGloves, engine.FindPlayer(P1).Skill);
            Assert.AreEqual(SkillId.None, engine.FindPlayer(Host).Skill);
            Assert.AreEqual(0, engine.FindPlayer(P1).RoundKimchi, "새 라운드에서는 김치 수 초기화");
            Assert.AreEqual(GameRules.NoPlayer, engine.LotteryWinnerId, "이전 라운드 결과는 지워진다");

            // 2라운드에서 아무도 점수를 못 내면 3라운드에는 스킬이 사라진다.
            FinishPhase(engine);
            FinishRound(engine);
            Assert.AreEqual(3, engine.RoundNumber);
            Assert.AreEqual(SkillId.None, engine.FindPlayer(P1).Skill);
        }

        [Test]
        public void EverySkillInTheCatalog_CanBeDrawn()
        {
            for (int i = 0; i < SkillCatalog.All.Count; i++)
            {
                SetUp();
                MatchEngine engine = NewLobby(P1);
                StartPlaying(engine);
                engine.TryAwardKimchi(P1, 1);
                FinishPhase(engine);
                m_Random.Enqueue(0);
                m_Random.Enqueue(i);
                FinishPhase(engine);
                Assert.AreEqual(SkillCatalog.All[i], engine.LotterySkill);
            }
        }

        // ---------------------------------------------------------------- 스킬 사용

        MatchEngine RoundTwoWithSkill(SkillId skill, ulong owner)
        {
            MatchEngine engine = NewLobby(P1);
            StartPlaying(engine);
            engine.TryAwardKimchi(owner, 1);
            FinishPhase(engine);
            int index = 0;
            for (int i = 0; i < SkillCatalog.All.Count; i++)
            {
                if (SkillCatalog.All[i] == skill)
                {
                    index = i;
                }
            }

            m_Random.Enqueue(0);
            m_Random.Enqueue(index);
            FinishPhase(engine);
            FinishPhase(engine);
            FinishPhase(engine);
            Assert.AreEqual(MatchPhase.RoundIntro, engine.Phase);
            return engine;
        }

        [Test]
        public void ActiveSkill_CanBeUsedExactlyOncePerRound()
        {
            MatchEngine engine = RoundTwoWithSkill(SkillId.SeasoningBomb, P1);
            Assert.IsFalse(engine.TryUseActiveSkill(P1, out _), "라운드 시작 연출 중에는 사용 불가");
            FinishPhase(engine);

            Assert.IsTrue(engine.TryUseActiveSkill(P1, out SkillId used));
            Assert.AreEqual(SkillId.SeasoningBomb, used);
            Assert.IsTrue(engine.FindPlayer(P1).ActiveSkillUsed);
            Assert.IsFalse(engine.TryUseActiveSkill(P1, out _), "두 번째 사용은 거절");
        }

        [Test]
        public void PassiveSkill_CannotBeActivated()
        {
            MatchEngine engine = RoundTwoWithSkill(SkillId.FishSauceMagnet, P1);
            FinishPhase(engine);
            Assert.IsFalse(engine.TryUseActiveSkill(P1, out SkillId used));
            Assert.AreEqual(SkillId.None, used);
        }

        [Test]
        public void PlayerWithoutSkill_OrUnknownPlayer_CannotActivate()
        {
            MatchEngine engine = RoundTwoWithSkill(SkillId.PepperPowder, P1);
            FinishPhase(engine);
            Assert.IsFalse(engine.TryUseActiveSkill(Host, out _));
            Assert.IsFalse(engine.TryUseActiveSkill(999, out _));
        }

        // ---------------------------------------------------------------- 매치 종료

        [Test]
        public void FirstToTargetWins_TriggersFinale()
        {
            MatchEngine engine = NewLobby(P1);
            engine.TrySetSettings(Host, new MatchSettings(2, 30));
            StartPlaying(engine);

            engine.TryAwardKimchi(P1, 1);
            FinishRound(engine);
            Assert.AreEqual(MatchPhase.RoundIntro, engine.Phase);
            Assert.AreEqual(1, engine.FindPlayer(P1).Wins);
            Assert.AreEqual(GameRules.NoPlayer, engine.MatchWinnerId);

            FinishPhase(engine);
            engine.TryAwardKimchi(P1, 1);
            FinishRound(engine);
            Assert.AreEqual(MatchPhase.Finale, engine.Phase);
            Assert.AreEqual(P1, engine.MatchWinnerId);
            Assert.AreEqual(2, engine.FindPlayer(P1).Wins);
        }

        [Test]
        public void Finale_WaitsForTheHost_AndHonoursMinimumTime()
        {
            MatchEngine engine = NewLobby(P1);
            engine.TrySetSettings(Host, new MatchSettings(1, 30));
            StartPlaying(engine);
            engine.TryAwardKimchi(P1, 1);
            FinishRound(engine);
            Assert.AreEqual(MatchPhase.Finale, engine.Phase);

            engine.Tick(m_Now + 100000);
            Assert.AreEqual(MatchPhase.Finale, engine.Phase, "피날레는 시간이 지나도 스스로 끝나지 않는다");

            Assert.IsFalse(engine.TryReturnToLobby(P1, m_Now + 10), "손님은 복귀시킬 수 없다");
            Assert.IsFalse(engine.TryReturnToLobby(Host, m_Now + 4.9), "최소 연출 시간 전");
            Assert.IsTrue(engine.TryReturnToLobby(Host, m_Now + 5.0));

            Assert.AreEqual(MatchPhase.Lobby, engine.Phase);
            Assert.AreEqual(MatchEndReason.HostReturnedToLobby, engine.LastEndReason);
            Assert.AreEqual(0, engine.RoundNumber);
            Assert.AreEqual(GameRules.NoPlayer, engine.MatchWinnerId);
            Assert.AreEqual(0, engine.FindPlayer(P1).Wins);
            Assert.IsFalse(engine.FindPlayer(P1).Ready, "복귀 후에는 다시 준비해야 한다");
            Assert.IsTrue(engine.FindPlayer(Host).Ready);
        }

        [Test]
        public void ReturnToLobby_IsRejectedOutsideFinale()
        {
            MatchEngine engine = NewLobby(P1);
            Assert.IsFalse(engine.TryReturnToLobby(Host, m_Now));
            StartPlaying(engine);
            Assert.IsFalse(engine.TryReturnToLobby(Host, m_Now + 1000));
        }

        [Test]
        public void MatchCanBeReplayedAfterReturningToLobby()
        {
            MatchEngine engine = NewLobby(P1);
            engine.TrySetSettings(Host, new MatchSettings(1, 30));
            StartPlaying(engine);
            engine.TryAwardKimchi(Host, 1);
            FinishRound(engine);
            m_Now += 10;
            Assert.IsTrue(engine.TryReturnToLobby(Host, m_Now));

            Assert.AreEqual(StartError.PlayersNotReady, engine.TryStartMatch(Host, m_Now));
            engine.TrySetReady(P1, true);
            StartPlaying(engine);
            Assert.AreEqual(1, engine.RoundNumber);
            Assert.AreEqual(0, engine.FindPlayer(Host).Wins);
        }

        [Test]
        public void Abort_OnlyHost_OnlyDuringMatch()
        {
            MatchEngine engine = NewLobby(P1);
            Assert.IsFalse(engine.TryAbortMatch(Host, m_Now), "대기실에서는 중단할 매치가 없다");
            StartPlaying(engine);
            Assert.IsFalse(engine.TryAbortMatch(P1, m_Now));
            Assert.IsTrue(engine.TryAbortMatch(Host, m_Now));
            Assert.AreEqual(MatchPhase.Lobby, engine.Phase);
            Assert.AreEqual(MatchEndReason.HostAborted, engine.LastEndReason);
        }

        // ---------------------------------------------------------------- 이탈

        [Test]
        public void MatchContinues_WhenEnoughPlayersRemain()
        {
            MatchEngine engine = NewLobby(P1, P2);
            StartPlaying(engine);
            engine.TryAwardKimchi(P2, 1);
            Assert.IsTrue(engine.RemovePlayer(P2, m_Now));
            Assert.AreEqual(MatchPhase.RoundPlaying, engine.Phase);
            engine.TryAwardKimchi(P1, 1);
            FinishPhase(engine);
            Assert.AreEqual(P1, engine.RoundWinnerId, "나간 플레이어의 점수는 계산에서 빠진다");
            Assert.AreEqual(1, engine.TotalTickets);
        }

        [Test]
        public void MatchAborts_WhenTooFewPlayersRemain()
        {
            MatchEngine engine = NewLobby(P1);
            StartPlaying(engine);
            Assert.IsTrue(engine.RemovePlayer(P1, m_Now));
            Assert.AreEqual(MatchPhase.Lobby, engine.Phase);
            Assert.AreEqual(MatchEndReason.NotEnoughPlayers, engine.LastEndReason);
            Assert.AreEqual(1, engine.Players.Count);
        }

        [Test]
        public void LeavingInLobby_DoesNotChangePhaseOrReason()
        {
            MatchEngine engine = NewLobby(P1);
            engine.RemovePlayer(P1, m_Now);
            Assert.AreEqual(MatchPhase.Lobby, engine.Phase);
            Assert.AreEqual(MatchEndReason.None, engine.LastEndReason);
        }

        [Test]
        public void LotteryWinnerLeaving_SimplyLosesTheSkill()
        {
            MatchEngine engine = NewLobby(P1, P2);
            StartPlaying(engine);
            engine.TryAwardKimchi(P2, 1);
            FinishPhase(engine);
            m_Random.Enqueue(0);
            m_Random.Enqueue(5);
            FinishPhase(engine);
            Assert.AreEqual(P2, engine.LotteryWinnerId);
            engine.RemovePlayer(P2, m_Now);
            FinishPhase(engine);
            FinishPhase(engine);
            Assert.AreEqual(MatchPhase.RoundIntro, engine.Phase);
            Assert.AreEqual(SkillId.None, engine.FindPlayer(Host).Skill);
            Assert.AreEqual(SkillId.None, engine.FindPlayer(P1).Skill);
        }

        [Test]
        public void Revision_ChangesOnEveryStateChange_AndNotOnRejectedRequests()
        {
            MatchEngine engine = NewLobby(P1);
            uint revision = engine.Revision;

            engine.TrySetSettings(P1, new MatchSettings(2, 60));
            engine.TryAwardKimchi(P1, 1);
            engine.TryStartMatch(P1, m_Now);
            engine.TrySetReady(P1, true); // 이미 준비 상태
            Assert.AreEqual(revision, engine.Revision);

            engine.TrySetReady(P1, false);
            Assert.AreNotEqual(revision, engine.Revision);
        }

        [Test]
        public void CustomWinRule_IsUsed()
        {
            var engine = new MatchEngine(Host, m_Timings, m_Random, new FixedWinnerRule(P1));
            engine.TryAddPlayer(Host, "방장", out _);
            engine.TryAddPlayer(P1, "손님", out _);
            engine.TrySetReady(P1, true);
            StartPlaying(engine);
            engine.TryAwardKimchi(Host, 3);
            FinishPhase(engine);
            Assert.AreEqual(P1, engine.RoundWinnerId);
        }

        [Test]
        public void WinRuleNamingAnAbsentPlayer_IsTreatedAsNoWinner()
        {
            var engine = new MatchEngine(Host, m_Timings, m_Random, new FixedWinnerRule(999));
            engine.TryAddPlayer(Host, "방장", out _);
            engine.TryAddPlayer(P1, "손님", out _);
            engine.TrySetReady(P1, true);
            StartPlaying(engine);
            FinishPhase(engine);
            Assert.AreEqual(GameRules.NoPlayer, engine.RoundWinnerId);
        }

        sealed class FixedWinnerRule : IRoundWinRule
        {
            readonly ulong m_Winner;

            public FixedWinnerRule(ulong winner)
            {
                m_Winner = winner;
            }

            public ulong DetermineWinner(IReadOnlyList<PlayerRecord> players) => m_Winner;
        }
    }
}
