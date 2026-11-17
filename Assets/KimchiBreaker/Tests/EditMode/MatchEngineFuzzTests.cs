using System;
using System.Collections.Generic;
using KimchiBreaker.Core;
using NUnit.Framework;

namespace KimchiBreaker.Tests
{
    /// <summary>
    /// 무작위 요청을 대량으로 퍼부어도 상태 머신이 예외 없이 규칙(불변 조건)을 지키는지 확인한다.
    /// 악의적인 클라이언트가 아무 요청이나 아무 순서로 보내는 상황을 흉내 낸다.
    /// </summary>
    public class MatchEngineFuzzTests
    {
        const ulong Host = 0;

        static readonly MatchPhase[][] s_AllowedNext =
        {
            /* Lobby           */ new[] { MatchPhase.RoundIntro },
            /* RoundIntro      */ new[] { MatchPhase.RoundPlaying, MatchPhase.Lobby },
            /* RoundPlaying    */ new[] { MatchPhase.RoundSettlement, MatchPhase.Lobby },
            /* RoundSettlement */ new[] { MatchPhase.Lottery, MatchPhase.Lobby },
            /* Lottery         */ new[] { MatchPhase.RoundResult, MatchPhase.Lobby },
            /* RoundResult     */ new[] { MatchPhase.RoundIntro, MatchPhase.Finale, MatchPhase.Lobby },
            /* Finale          */ new[] { MatchPhase.Lobby },
        };

        [Test]
        public void RandomOperations_NeverBreakInvariants()
        {
            for (int seed = 0; seed < 400; seed++)
            {
                RunScenario(seed, 600);
            }
        }

        static void RunScenario(int seed, int steps)
        {
            var random = new Random(seed);
            var timings = new MatchTimings
            {
                RoundIntroSeconds = random.Next(0, 3),
                SettlementSeconds = random.Next(0, 3),
                LotterySeconds = random.Next(0, 3),
                ResultSeconds = random.Next(0, 3),
                FinaleMinSeconds = random.Next(0, 3),
            };
            int minPlayers = random.Next(1, 3);
            var engine = new MatchEngine(Host, timings, new SeededRandom(seed), null, minPlayers);
            engine.TryAddPlayer(Host, "방장", out _);

            string context = "seed=" + seed;
            engine.PhaseChanged += (previous, next) =>
            {
                Assert.Contains(next, s_AllowedNext[(int)previous], context + " 잘못된 단계 전환 " + previous + "→" + next);
            };

            double now = 0.0;
            ulong[] ids = { Host, 1, 2, 3, 4, 5, 999 };
            for (int step = 0; step < steps; step++)
            {
                ulong id = ids[random.Next(ids.Length)];
                int roundBefore = engine.RoundNumber;
                MatchPhase phaseBefore = engine.Phase;

                switch (random.Next(13))
                {
                    case 0:
                        if (id != Host)
                        {
                            engine.TryAddPlayer(id, RandomName(random), out _);
                        }

                        break;
                    case 1:
                        if (id != Host && random.Next(4) == 0)
                        {
                            engine.RemovePlayer(id, now);
                        }

                        break;
                    case 2:
                        engine.TrySetReady(id, random.Next(4) != 0);
                        break;
                    case 3:
                        engine.TrySetSettings(id, new MatchSettings(random.Next(-2, 25), random.Next(0, 200)));
                        break;
                    case 4:
                        engine.TryStartMatch(id, now);
                        break;
                    case 5:
                    case 6:
                    case 7:
                        engine.TryAwardKimchi(id, random.Next(-1, 6));
                        break;
                    case 8:
                        engine.TryUseActiveSkill(id, out _);
                        break;
                    case 9:
                        engine.TryReturnToLobby(id, now);
                        break;
                    case 10:
                        if (random.Next(40) == 0)
                        {
                            engine.TryAbortMatch(id, now);
                        }

                        break;
                    default:
                        now += random.NextDouble() * 20.0;
                        engine.Tick(now);
                        break;
                }

                CheckInvariants(engine, context + " step=" + step);

                if (engine.RoundNumber != roundBefore && engine.Phase != MatchPhase.Lobby)
                {
                    Assert.AreEqual(roundBefore + 1, engine.RoundNumber, context + " 라운드 번호는 1씩 증가");
                }

                if (phaseBefore == MatchPhase.Lobby && engine.Phase != MatchPhase.Lobby)
                {
                    Assert.AreEqual(1, engine.RoundNumber, context);
                }
            }
        }

        static string RandomName(Random random)
        {
            switch (random.Next(5))
            {
                case 0: return null;
                case 1: return string.Empty;
                case 2: return "<b>‮악성\0이름</b>";
                case 3: return new string((char)random.Next(1, 0xFFFF), random.Next(1, 200));
                default: return "손님" + random.Next(100);
            }
        }

        static void CheckInvariants(MatchEngine engine, string context)
        {
            IReadOnlyList<PlayerRecord> players = engine.Players;
            Assert.LessOrEqual(players.Count, GameRules.MaxPlayers, context);
            Assert.IsNotNull(engine.FindPlayer(Host), context + " 방장은 항상 존재");
            Assert.IsTrue(engine.Settings.IsValid, context + " 설정은 항상 유효 범위");

            var seats = new HashSet<int>();
            var ids = new HashSet<ulong>();
            int previousSeat = -1;
            int ticketSum = 0;
            int skillHolders = 0;
            foreach (PlayerRecord player in players)
            {
                Assert.That(player.Seat, Is.InRange(0, GameRules.MaxPlayers - 1), context);
                Assert.IsTrue(seats.Add(player.Seat), context + " 좌석 중복");
                Assert.IsTrue(ids.Add(player.ClientId), context + " 플레이어 중복");
                Assert.Greater(player.Seat, previousSeat, context + " 좌석 순 정렬");
                previousSeat = player.Seat;

                Assert.IsFalse(string.IsNullOrEmpty(player.Name), context);
                Assert.LessOrEqual(player.Name.Length, GameRules.MaxNicknameLength * 2, context);
                Assert.That(player.Wins, Is.InRange(0, engine.Settings.TargetWins), context + " 승수 범위");
                Assert.That(player.RoundKimchi, Is.InRange(0, GameRules.MaxKimchiPerRound), context);
                Assert.GreaterOrEqual(player.TicketCount, 0, context);
                Assert.IsTrue(player.Skill == SkillId.None || SkillCatalog.IsDefined(player.Skill), context);
                if (player.ActiveSkillUsed)
                {
                    Assert.IsTrue(SkillCatalog.IsActive(player.Skill), context + " 사용 표시는 액티브 스킬에만");
                }

                if (player.Skill != SkillId.None)
                {
                    skillHolders++;
                }

                if (player.TicketCount > 0)
                {
                    Assert.GreaterOrEqual(player.TicketStart, 1, context);
                    Assert.LessOrEqual(player.TicketStart + player.TicketCount - 1, engine.TotalTickets, context);
                }

                ticketSum += player.TicketCount;
            }

            Assert.LessOrEqual(skillHolders, 1, context + " 스킬은 추첨 당첨자 한 명만");
            Assert.LessOrEqual(ticketSum, engine.TotalTickets, context);

            if (engine.Phase == MatchPhase.Lobby)
            {
                Assert.AreEqual(0, engine.RoundNumber, context);
                Assert.IsTrue(double.IsPositiveInfinity(engine.PhaseEndTime), context);
                Assert.AreEqual(GameRules.NoPlayer, engine.MatchWinnerId, context);
                foreach (PlayerRecord player in players)
                {
                    Assert.AreEqual(0, player.Wins, context);
                    Assert.AreEqual(0, player.RoundKimchi, context);
                    Assert.AreEqual(SkillId.None, player.Skill, context);
                }
            }
            else
            {
                Assert.GreaterOrEqual(engine.RoundNumber, 1, context);
            }

            if (engine.Phase == MatchPhase.Finale)
            {
                Assert.AreNotEqual(GameRules.NoPlayer, engine.MatchWinnerId, context);
                Assert.IsTrue(double.IsPositiveInfinity(engine.PhaseEndTime), context);
            }

            if (engine.Phase == MatchPhase.RoundIntro || engine.Phase == MatchPhase.RoundPlaying)
            {
                Assert.AreEqual(0, engine.TotalTickets, context + " 새 라운드에는 이전 번호표가 남지 않는다");
                Assert.AreEqual(GameRules.NoPlayer, engine.LotteryWinnerId, context);
                Assert.AreEqual(GameRules.NoPlayer, engine.MatchWinnerId, context);
            }

            if (engine.WinningTicket != 0)
            {
                Assert.That(engine.WinningTicket, Is.InRange(1, engine.TotalTickets), context);
                Assert.AreNotEqual(GameRules.NoPlayer, engine.LotteryWinnerId, context);
                Assert.IsTrue(SkillCatalog.IsDefined(engine.LotterySkill), context);
                PlayerRecord winner = engine.FindPlayer(engine.LotteryWinnerId);
                if (winner != null)
                {
                    Assert.That(engine.WinningTicket, Is.InRange(winner.TicketStart, winner.TicketStart + winner.TicketCount - 1), context + " 당첨 번호는 당첨자의 번호표");
                }
            }
            else
            {
                Assert.AreEqual(GameRules.NoPlayer, engine.LotteryWinnerId, context);
                Assert.AreEqual(SkillId.None, engine.LotterySkill, context);
            }

            // 목표 승수에 도달한 사람은 많아야 한 명이고, 그 사람이 매치 승자다.
            int reached = 0;
            foreach (PlayerRecord player in players)
            {
                if (player.Wins >= engine.Settings.TargetWins)
                {
                    reached++;
                    Assert.AreEqual(player.ClientId, engine.MatchWinnerId, context);
                }
            }

            Assert.LessOrEqual(reached, 1, context);
        }

        [Test]
        public void LotteryOdds_MatchTicketShares()
        {
            // 번호표 7 : 2 : 1 → 당첨 비율도 70% : 20% : 10% 에 수렴해야 한다.
            var wins = new Dictionary<ulong, int> { { Host, 0 }, { 1, 0 }, { 2, 0 } };
            var timings = new MatchTimings { RoundIntroSeconds = 0, SettlementSeconds = 0, LotterySeconds = 0, ResultSeconds = 0 };
            const int rounds = 20000;
            using (var secure = new SecureRandomSource())
            {
                var engine = new MatchEngine(Host, timings, secure);
                engine.TryAddPlayer(Host, "a", out _);
                engine.TryAddPlayer(1, "b", out _);
                engine.TryAddPlayer(2, "c", out _);
                engine.TrySetReady(1, true);
                engine.TrySetReady(2, true);
                engine.TrySetSettings(Host, new MatchSettings(GameRules.MaxTargetWins, GameRules.MinRoundSeconds));

                double now = 0.0;
                int drawn = 0;
                while (drawn < rounds)
                {
                    if (engine.Phase == MatchPhase.Lobby)
                    {
                        engine.TrySetReady(1, true);
                        engine.TrySetReady(2, true);
                        Assert.AreEqual(StartError.None, engine.TryStartMatch(Host, now));
                    }
                    else if (engine.Phase == MatchPhase.Finale)
                    {
                        Assert.IsTrue(engine.TryReturnToLobby(Host, now + 100));
                    }
                    else if (engine.Phase == MatchPhase.RoundPlaying && engine.FindPlayer(Host).RoundKimchi == 0)
                    {
                        for (int i = 0; i < 7; i++)
                        {
                            engine.TryAwardKimchi(Host, 1);
                        }

                        engine.TryAwardKimchi(1, 2);
                        engine.TryAwardKimchi(2, 1);
                    }
                    else
                    {
                        MatchPhase before = engine.Phase;
                        now = Math.Max(now, engine.PhaseEndTime);
                        engine.Tick(now);
                        if (before == MatchPhase.RoundSettlement)
                        {
                            Assert.AreEqual(10, engine.TotalTickets);
                            wins[engine.LotteryWinnerId]++;
                            drawn++;
                        }
                    }
                }
            }

            Assert.That(wins[Host] / (double)rounds, Is.InRange(0.67, 0.73));
            Assert.That(wins[1] / (double)rounds, Is.InRange(0.18, 0.22));
            Assert.That(wins[2] / (double)rounds, Is.InRange(0.085, 0.115));
        }
    }
}
