using System;

namespace KimchiBreaker.Core
{
    /// <summary>
    /// 토큰 버킷 방식 요청 빈도 제한. 클라이언트가 보내는 요청(타격, 설정 변경 등)을
    /// 서버에서 걸러 도배/매크로/서비스 거부 공격을 막는다.
    /// </summary>
    public sealed class TokenBucket
    {
        readonly double m_Capacity;
        readonly double m_RefillPerSecond;
        double m_Tokens;
        double m_LastTime;
        bool m_Started;

        /// <param name="capacity">한꺼번에 허용되는 최대 요청 수.</param>
        /// <param name="refillPerSecond">초당 회복되는 요청 수.</param>
        public TokenBucket(double capacity, double refillPerSecond)
        {
            if (!(capacity > 0.0) || double.IsInfinity(capacity))
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            if (!(refillPerSecond >= 0.0) || double.IsInfinity(refillPerSecond))
            {
                throw new ArgumentOutOfRangeException(nameof(refillPerSecond));
            }

            m_Capacity = capacity;
            m_RefillPerSecond = refillPerSecond;
            m_Tokens = capacity;
        }

        /// <summary>요청 1회를 소비한다. 허용되면 true.</summary>
        public bool TryConsume(double now) => TryConsume(now, 1.0);

        public bool TryConsume(double now, double cost)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || !(cost > 0.0))
            {
                return false;
            }

            if (!m_Started)
            {
                m_Started = true;
                m_LastTime = now;
            }
            else if (now > m_LastTime)
            {
                m_Tokens = Math.Min(m_Capacity, m_Tokens + (now - m_LastTime) * m_RefillPerSecond);
                m_LastTime = now;
            }

            if (m_Tokens >= cost)
            {
                m_Tokens -= cost;
                return true;
            }

            return false;
        }

        /// <summary>버킷을 가득 찬 상태로 되돌린다.</summary>
        public void Reset()
        {
            m_Tokens = m_Capacity;
            m_Started = false;
        }
    }
}
