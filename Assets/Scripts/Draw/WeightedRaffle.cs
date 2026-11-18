using System;
using System.Collections.Generic;

public readonly struct RaffleEntry
{
    public int PlayerId { get; }
    public int Tickets { get; }
    public RaffleEntry(int playerId, int tickets) { PlayerId = playerId; Tickets = tickets; }
}

/// <summary>Weighted drawing without replacement by player, not by individual ticket.</summary>
public static class WeightedRaffle
{
    private struct Candidate { public int PlayerId; public long Weight; }

    public static int[] Draw(IReadOnlyList<RaffleEntry> entries, int winnerCount, Random random)
    {
        if (entries == null) throw new ArgumentNullException(nameof(entries));
        if (random == null) throw new ArgumentNullException(nameof(random));
        if (winnerCount < 0) throw new ArgumentOutOfRangeException(nameof(winnerCount));
        var weights = new Dictionary<int, long>();
        foreach (var entry in entries)
        {
            if (entry.Tickets < 0) throw new ArgumentOutOfRangeException(nameof(entries), "Ticket counts cannot be negative.");
            if (entry.Tickets == 0) continue;
            weights.TryGetValue(entry.PlayerId, out long previous);
            weights[entry.PlayerId] = checked(previous + entry.Tickets);
        }
        var pool = new List<Candidate>(weights.Count);
        long total = 0;
        foreach (var item in weights)
        {
            total = checked(total + item.Value);
            pool.Add(new Candidate { PlayerId = item.Key, Weight = item.Value });
        }
        int count = Math.Min(winnerCount, pool.Count);
        var winners = new int[count];
        var bytes = new byte[8];
        for (int draw = 0; draw < count; draw++)
        {
            long ticket = NextBelow(random, total, bytes);
            int selected = 0;
            for (; selected < pool.Count - 1; selected++)
            {
                if (ticket < pool[selected].Weight) break;
                ticket -= pool[selected].Weight;
            }
            winners[draw] = pool[selected].PlayerId;
            total -= pool[selected].Weight;
            // Remove every ticket belonging to this player before selecting the next winner.
            pool[selected] = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
        }
        return winners;
    }

    private static long NextBelow(Random random, long exclusiveMaximum, byte[] bytes)
    {
        ulong bound = (ulong)exclusiveMaximum;
        ulong threshold = unchecked(0UL - bound) % bound;
        ulong value;
        do { random.NextBytes(bytes); value = BitConverter.ToUInt64(bytes, 0); } while (value < threshold);
        return (long)(value % bound);
    }
}
