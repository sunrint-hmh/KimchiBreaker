using System;

namespace KimchiBreaker.Core
{
    /// <summary>방장이 대기실에서 정하는 매치 설정.</summary>
    public readonly struct MatchSettings : IEquatable<MatchSettings>
    {
        /// <summary>매치 승리에 필요한 라운드 승수.</summary>
        public readonly int TargetWins;

        /// <summary>한 라운드 길이(초).</summary>
        public readonly int RoundSeconds;

        public MatchSettings(int targetWins, int roundSeconds)
        {
            TargetWins = targetWins;
            RoundSeconds = roundSeconds;
        }

        public static MatchSettings Default => new MatchSettings(GameRules.DefaultTargetWins, GameRules.DefaultRoundSeconds);

        /// <summary>값이 허용 범위 안인지 여부.</summary>
        public bool IsValid =>
            TargetWins >= GameRules.MinTargetWins && TargetWins <= GameRules.MaxTargetWins &&
            RoundSeconds >= GameRules.MinRoundSeconds && RoundSeconds <= GameRules.MaxRoundSeconds;

        /// <summary>허용 범위로 보정한 복사본.</summary>
        public MatchSettings Clamped()
        {
            return new MatchSettings(
                Clamp(TargetWins, GameRules.MinTargetWins, GameRules.MaxTargetWins),
                Clamp(RoundSeconds, GameRules.MinRoundSeconds, GameRules.MaxRoundSeconds));
        }

        static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);

        public bool Equals(MatchSettings other) => TargetWins == other.TargetWins && RoundSeconds == other.RoundSeconds;

        public override bool Equals(object obj) => obj is MatchSettings other && Equals(other);

        public override int GetHashCode() => unchecked((TargetWins * 397) ^ RoundSeconds);

        public static bool operator ==(MatchSettings a, MatchSettings b) => a.Equals(b);

        public static bool operator !=(MatchSettings a, MatchSettings b) => !a.Equals(b);
    }

    /// <summary>각 단계의 길이(초). 연출 길이에 맞춰 조정한다.</summary>
    public sealed class MatchTimings
    {
        /// <summary>라운드 시작 연출(탑뷰 → 1인칭, 카운트다운).</summary>
        public double RoundIntroSeconds = 4.0;

        /// <summary>정산(번호표 장독대 투입 연출).</summary>
        public double SettlementSeconds = 4.0;

        /// <summary>김치전사 추첨 연출.</summary>
        public double LotterySeconds = 7.0;

        /// <summary>라운드 결과 창 표시.</summary>
        public double ResultSeconds = 6.0;

        /// <summary>피날레 최소 유지 시간. 이 시간이 지나야 방장이 대기실로 돌아갈 수 있다.</summary>
        public double FinaleMinSeconds = 5.0;

        /// <summary>값이 잘못됐으면(음수, NaN 등) 안전한 값으로 고친 복사본을 돌려준다.</summary>
        public MatchTimings Sanitized()
        {
            return new MatchTimings
            {
                RoundIntroSeconds = Fix(RoundIntroSeconds, 4.0),
                SettlementSeconds = Fix(SettlementSeconds, 4.0),
                LotterySeconds = Fix(LotterySeconds, 7.0),
                ResultSeconds = Fix(ResultSeconds, 6.0),
                FinaleMinSeconds = Fix(FinaleMinSeconds, 5.0),
            };
        }

        static double Fix(double value, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
            {
                return fallback;
            }

            return value > 600.0 ? 600.0 : value;
        }
    }
}
