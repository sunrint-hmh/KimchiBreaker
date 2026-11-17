using System;

namespace KimchiBreaker.Core
{
    /// <summary>클라이언트 요청 종류. 종류마다 허용 빈도가 다르다.</summary>
    public enum RequestKind
    {
        /// <summary>자세(위치/시선) 보고. 초당 약 30회.</summary>
        Pose = 0,

        /// <summary>재료 타격. 사람의 광클 + 자동 연타 스킬 수준까지 허용.</summary>
        Hit = 1,

        /// <summary>액티브 스킬 사용.</summary>
        Skill = 2,

        /// <summary>대기실 조작(준비, 설정 변경, 시작 등).</summary>
        Lobby = 3,
    }

    /// <summary>
    /// 클라이언트 한 명의 요청 빈도를 제한하고 위반 점수를 관리한다(서버 전용).
    /// 위반 점수가 기준을 넘으면 해당 클라이언트를 내보내야 한다.
    /// </summary>
    public sealed class RequestGuard
    {
        /// <summary>이 점수를 넘으면 내보낸다.</summary>
        public const double KickThreshold = 100.0;

        /// <summary>초당 줄어드는 위반 점수. 정상 플레이 중 가끔 생기는 오차는 자연히 사라진다.</summary>
        const double k_DecayPerSecond = 5.0;

        readonly TokenBucket[] m_Buckets =
        {
            new TokenBucket(capacity: 45, refillPerSecond: 40),  // Pose
            new TokenBucket(capacity: 30, refillPerSecond: 22),  // Hit
            new TokenBucket(capacity: 3, refillPerSecond: 0.5),  // Skill
            new TokenBucket(capacity: 10, refillPerSecond: 4),   // Lobby
        };

        double m_Strikes;
        double m_LastDecayTime;
        bool m_DecayStarted;

        public double Strikes => m_Strikes;

        public bool ShouldKick => m_Strikes >= KickThreshold;

        /// <summary>요청을 허용할지 판단한다. 빈도를 넘으면 false 와 함께 위반 점수가 오른다.</summary>
        public bool Allow(RequestKind kind, double now)
        {
            int index = (int)kind;
            if (index < 0 || index >= m_Buckets.Length)
            {
                ReportViolation(10.0, now);
                return false;
            }

            if (m_Buckets[index].TryConsume(now))
            {
                Decay(now);
                return true;
            }

            ReportViolation(kind == RequestKind.Pose ? 0.5 : 1.0, now);
            return false;
        }

        /// <summary>규칙에 어긋난 요청(범위 밖 값, 권한 없는 조작 등)을 기록한다.</summary>
        public void ReportViolation(double weight, double now)
        {
            Decay(now);
            if (weight > 0.0 && !double.IsNaN(weight))
            {
                m_Strikes = Math.Min(KickThreshold * 10.0, m_Strikes + weight);
            }
        }

        void Decay(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now))
            {
                return;
            }

            if (!m_DecayStarted)
            {
                m_DecayStarted = true;
                m_LastDecayTime = now;
                return;
            }

            if (now > m_LastDecayTime)
            {
                m_Strikes = Math.Max(0.0, m_Strikes - (now - m_LastDecayTime) * k_DecayPerSecond);
                m_LastDecayTime = now;
            }
        }
    }
}
