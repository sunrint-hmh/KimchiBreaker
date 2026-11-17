using System;
using System.Collections.Generic;
using KimchiBreaker.Core;

namespace KimchiBreaker.Tests
{
    /// <summary>미리 정해 둔 값을 순서대로 돌려주는 난수 공급원.</summary>
    public sealed class ScriptedRandom : IRandomSource
    {
        readonly Queue<int> m_Values = new Queue<int>();

        public int Calls { get; private set; }

        public ScriptedRandom(params int[] values)
        {
            foreach (int value in values)
            {
                m_Values.Enqueue(value);
            }
        }

        public void Enqueue(int value) => m_Values.Enqueue(value);

        public int NextInt(int exclusiveMax)
        {
            Calls++;
            int value = m_Values.Count > 0 ? m_Values.Dequeue() : 0;
            return Math.Max(0, Math.Min(exclusiveMax - 1, value));
        }
    }

    /// <summary>시드가 고정된 난수 공급원(재현 가능한 무작위 테스트용).</summary>
    public sealed class SeededRandom : IRandomSource
    {
        readonly Random m_Random;

        public SeededRandom(int seed)
        {
            m_Random = new Random(seed);
        }

        public int NextInt(int exclusiveMax) => m_Random.Next(exclusiveMax);
    }
}
