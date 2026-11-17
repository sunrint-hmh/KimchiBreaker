using System;
using KimchiBreaker.Core;
using Unity.Collections;
using Unity.Netcode;

namespace KimchiBreaker.Net
{
    /// <summary>
    /// 클라이언트에 동기화되는 플레이어 한 명의 공개 정보.
    /// 호스트가 보낸 값이라도 화면에 쓰기 전에 범위를 확인한다(<see cref="MatchSnapshot.TryGetSlot"/>).
    /// </summary>
    public struct PlayerSlot : INetworkSerializable, IEquatable<PlayerSlot>
    {
        public bool Occupied;
        public ulong ClientId;
        public byte Seat;
        public FixedString64Bytes Name;
        public bool Ready;
        public byte Wins;
        public ushort RoundKimchi;
        public SkillId Skill;
        public bool ActiveSkillUsed;
        public int TicketStart;
        public int TicketCount;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Occupied);
            if (!Occupied)
            {
                if (serializer.IsReader)
                {
                    this = default;
                }

                return;
            }

            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Seat);
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref Ready);
            serializer.SerializeValue(ref Wins);
            serializer.SerializeValue(ref RoundKimchi);
            serializer.SerializeValue(ref Skill);
            serializer.SerializeValue(ref ActiveSkillUsed);
            serializer.SerializeValue(ref TicketStart);
            serializer.SerializeValue(ref TicketCount);
        }

        public bool Equals(PlayerSlot other)
        {
            if (Occupied != other.Occupied)
            {
                return false;
            }

            if (!Occupied)
            {
                return true;
            }

            return ClientId == other.ClientId && Seat == other.Seat && Name.Equals(other.Name) &&
                   Ready == other.Ready && Wins == other.Wins && RoundKimchi == other.RoundKimchi &&
                   Skill == other.Skill && ActiveSkillUsed == other.ActiveSkillUsed &&
                   TicketStart == other.TicketStart && TicketCount == other.TicketCount;
        }

        public override bool Equals(object obj) => obj is PlayerSlot other && Equals(other);

        public override int GetHashCode() => Occupied ? ClientId.GetHashCode() ^ (RoundKimchi << 8) ^ Wins : 0;

        /// <summary>화면에 표시해도 안전한 이름.</summary>
        public string DisplayName => TextSanitizer.SanitizeNickname(Name.ToString(), "플레이어" + (Seat + 1));
    }

    /// <summary>
    /// 매치 전체 상태의 공개본. 호스트의 <see cref="MatchEngine"/> 상태를 그대로 옮겨 담아 모든 클라이언트에 보낸다.
    /// 한 덩어리로 보내므로 클라이언트는 항상 서로 어긋나지 않는 상태를 본다.
    /// </summary>
    public struct MatchSnapshot : INetworkSerializable, IEquatable<MatchSnapshot>
    {
        public MatchPhase Phase;

        /// <summary>현재 단계 시작 시각(서버 시간, 초).</summary>
        public double PhaseStartTime;

        /// <summary>현재 단계 종료 시각(서버 시간, 초). <see cref="HasDeadline"/> 가 false 면 의미 없음.</summary>
        public double PhaseEndTime;

        public bool HasDeadline;
        public int RoundNumber;
        public byte TargetWins;
        public short RoundSeconds;
        public ulong HostClientId;
        public ulong RoundWinnerId;
        public int TotalTickets;
        public int WinningTicket;
        public ulong LotteryWinnerId;
        public SkillId LotterySkill;
        public ulong MatchWinnerId;
        public MatchEndReason LastEndReason;
        public PlayerSlot Slot0;
        public PlayerSlot Slot1;
        public PlayerSlot Slot2;
        public PlayerSlot Slot3;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Phase);
            serializer.SerializeValue(ref PhaseStartTime);
            serializer.SerializeValue(ref HasDeadline);
            if (HasDeadline)
            {
                serializer.SerializeValue(ref PhaseEndTime);
            }
            else if (serializer.IsReader)
            {
                PhaseEndTime = 0.0;
            }

            serializer.SerializeValue(ref RoundNumber);
            serializer.SerializeValue(ref TargetWins);
            serializer.SerializeValue(ref RoundSeconds);
            serializer.SerializeValue(ref HostClientId);
            serializer.SerializeValue(ref RoundWinnerId);
            serializer.SerializeValue(ref TotalTickets);
            serializer.SerializeValue(ref WinningTicket);
            serializer.SerializeValue(ref LotteryWinnerId);
            serializer.SerializeValue(ref LotterySkill);
            serializer.SerializeValue(ref MatchWinnerId);
            serializer.SerializeValue(ref LastEndReason);
            serializer.SerializeValue(ref Slot0);
            serializer.SerializeValue(ref Slot1);
            serializer.SerializeValue(ref Slot2);
            serializer.SerializeValue(ref Slot3);
        }

        /// <summary>네트워크로 받기 전의 기본 상태(대기실, 아무도 없음).</summary>
        public static MatchSnapshot Empty => new MatchSnapshot
        {
            Phase = MatchPhase.Lobby,
            TargetWins = GameRules.DefaultTargetWins,
            RoundSeconds = GameRules.DefaultRoundSeconds,
            RoundWinnerId = GameRules.NoPlayer,
            LotteryWinnerId = GameRules.NoPlayer,
            MatchWinnerId = GameRules.NoPlayer,
        };

        public const int SlotCount = GameRules.MaxPlayers;

        /// <summary>좌석 순서의 i 번째 칸. 비어 있거나 범위를 벗어나면 false.</summary>
        public bool TryGetSlot(int index, out PlayerSlot slot)
        {
            switch (index)
            {
                case 0: slot = Slot0; break;
                case 1: slot = Slot1; break;
                case 2: slot = Slot2; break;
                case 3: slot = Slot3; break;
                default: slot = default; return false;
            }

            return slot.Occupied && slot.Seat < GameRules.MaxPlayers;
        }

        public bool TryFindPlayer(ulong clientId, out PlayerSlot slot)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (TryGetSlot(i, out slot) && slot.ClientId == clientId)
                {
                    return true;
                }
            }

            slot = default;
            return false;
        }

        public int PlayerCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < SlotCount; i++)
                {
                    if (TryGetSlot(i, out _))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void SetSlot(int index, PlayerSlot slot)
        {
            switch (index)
            {
                case 0: Slot0 = slot; break;
                case 1: Slot1 = slot; break;
                case 2: Slot2 = slot; break;
                case 3: Slot3 = slot; break;
            }
        }

        /// <summary>현재 단계 남은 시간(초). 제한이 없으면 -1.</summary>
        public float GetRemainingSeconds(double serverNow)
        {
            if (!HasDeadline || double.IsNaN(serverNow))
            {
                return -1f;
            }

            double remaining = PhaseEndTime - serverNow;
            if (double.IsNaN(remaining) || remaining < 0.0)
            {
                return 0f;
            }

            return remaining > 3600.0 ? 3600f : (float)remaining;
        }

        public bool Equals(MatchSnapshot other)
        {
            return Phase == other.Phase && PhaseStartTime.Equals(other.PhaseStartTime) &&
                   HasDeadline == other.HasDeadline && (!HasDeadline || PhaseEndTime.Equals(other.PhaseEndTime)) &&
                   RoundNumber == other.RoundNumber && TargetWins == other.TargetWins &&
                   RoundSeconds == other.RoundSeconds && HostClientId == other.HostClientId &&
                   RoundWinnerId == other.RoundWinnerId && TotalTickets == other.TotalTickets &&
                   WinningTicket == other.WinningTicket && LotteryWinnerId == other.LotteryWinnerId &&
                   LotterySkill == other.LotterySkill && MatchWinnerId == other.MatchWinnerId &&
                   LastEndReason == other.LastEndReason && Slot0.Equals(other.Slot0) &&
                   Slot1.Equals(other.Slot1) && Slot2.Equals(other.Slot2) && Slot3.Equals(other.Slot3);
        }

        public override bool Equals(object obj) => obj is MatchSnapshot other && Equals(other);

        public override int GetHashCode() => ((int)Phase * 397) ^ RoundNumber ^ Slot0.GetHashCode() ^ Slot1.GetHashCode();

        /// <summary>서버 전용: 엔진 상태를 스냅숏으로 옮긴다.</summary>
        internal static MatchSnapshot FromEngine(MatchEngine engine)
        {
            var snapshot = new MatchSnapshot
            {
                Phase = engine.Phase,
                PhaseStartTime = engine.PhaseStartTime,
                HasDeadline = !double.IsPositiveInfinity(engine.PhaseEndTime) && !double.IsNaN(engine.PhaseEndTime),
                RoundNumber = engine.RoundNumber,
                TargetWins = (byte)engine.Settings.TargetWins,
                RoundSeconds = (short)engine.Settings.RoundSeconds,
                HostClientId = engine.HostClientId,
                RoundWinnerId = engine.RoundWinnerId,
                TotalTickets = engine.TotalTickets,
                WinningTicket = engine.WinningTicket,
                LotteryWinnerId = engine.LotteryWinnerId,
                LotterySkill = engine.LotterySkill,
                MatchWinnerId = engine.MatchWinnerId,
                LastEndReason = engine.LastEndReason,
            };
            if (snapshot.HasDeadline)
            {
                snapshot.PhaseEndTime = engine.PhaseEndTime;
            }

            for (int i = 0; i < engine.Players.Count && i < SlotCount; i++)
            {
                PlayerRecord player = engine.Players[i];
                var slot = new PlayerSlot
                {
                    Occupied = true,
                    ClientId = player.ClientId,
                    Seat = (byte)player.Seat,
                    Ready = player.Ready,
                    Wins = (byte)Math.Min(byte.MaxValue, player.Wins),
                    RoundKimchi = (ushort)Math.Min(ushort.MaxValue, player.RoundKimchi),
                    Skill = player.Skill,
                    ActiveSkillUsed = player.ActiveSkillUsed,
                    TicketStart = player.TicketStart,
                    TicketCount = player.TicketCount,
                };
                slot.Name = ToFixedString(player.Name);
                snapshot.SetSlot(i, slot);
            }

            return snapshot;
        }

        /// <summary>문자열을 고정 길이 문자열로 바꾼다. 넘치면 글자 단위로 줄인다(예외를 내지 않는다).</summary>
        internal static FixedString64Bytes ToFixedString(string text)
        {
            var result = new FixedString64Bytes();
            if (!string.IsNullOrEmpty(text))
            {
                // 닉네임은 이미 48바이트 이내로 정리되어 있으므로 잘리는 일은 없지만, 넘치더라도 예외 없이 잘라 담는다.
                result.CopyFromTruncated(text);
            }

            return result;
        }
    }

    /// <summary>액티브 스킬 발동 신호. 모든 클라이언트에 전달된다(효과 연출/적용용).</summary>
    public readonly struct SkillActivation
    {
        public readonly ulong ClientId;
        public readonly SkillId Skill;

        /// <summary>발동자가 조준한 지점(월드 좌표). 경기장 범위로 보정되어 있다.</summary>
        public readonly UnityEngine.Vector3 AimPoint;

        public SkillActivation(ulong clientId, SkillId skill, UnityEngine.Vector3 aimPoint)
        {
            ClientId = clientId;
            Skill = skill;
            AimPoint = aimPoint;
        }
    }
}
