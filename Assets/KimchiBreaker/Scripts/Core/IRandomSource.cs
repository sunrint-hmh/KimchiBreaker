using System;
using System.Security.Cryptography;

namespace KimchiBreaker.Core
{
    /// <summary>난수 공급원. 테스트에서는 가짜 구현으로 바꿔 끼운다.</summary>
    public interface IRandomSource
    {
        /// <summary>0 이상 <paramref name="exclusiveMax"/> 미만의 균등 분포 정수.</summary>
        int NextInt(int exclusiveMax);
    }

    /// <summary>
    /// 암호학적으로 안전한 난수. 추첨 결과를 예측하거나 조작할 수 없게 한다.
    /// 거절 표본추출(rejection sampling)로 나머지 연산 편향을 없앤다.
    /// </summary>
    public sealed class SecureRandomSource : IRandomSource, IDisposable
    {
        readonly RandomNumberGenerator m_Generator = RandomNumberGenerator.Create();
        readonly byte[] m_Buffer = new byte[4];
        readonly object m_Lock = new object();

        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax), "1 이상이어야 합니다.");
            }

            if (exclusiveMax == 1)
            {
                return 0;
            }

            uint range = (uint)exclusiveMax;
            // 2^32 를 range 로 나눈 나머지만큼의 앞 구간을 버리면 남는 구간이 range 의 배수가 된다.
            uint threshold = (uint)((0x100000000UL - range) % range);
            lock (m_Lock)
            {
                while (true)
                {
                    m_Generator.GetBytes(m_Buffer);
                    uint value = BitConverter.ToUInt32(m_Buffer, 0);
                    if (value >= threshold)
                    {
                        return (int)(value % range);
                    }
                }
            }
        }

        public void Dispose() => m_Generator.Dispose();
    }
}
