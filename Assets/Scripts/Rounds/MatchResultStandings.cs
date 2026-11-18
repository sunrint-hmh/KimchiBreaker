using System;
using System.Linq;

/// <summary>Final competition ranks: equal totals share a rank, e.g. 1, 1, 3.</summary>
public static class MatchResultStandings
{
    public sealed class Row
    {
        public int PlayerId { get; }
        public long KimchiCount { get; }
        public int Rank { get; }
        public bool IsWinner => Rank == 1;
        public Row(int playerId, long count, int rank)
        { PlayerId = playerId; KimchiCount = count; Rank = rank; }
    }

    public static Row[] Build(MatchProgress match)
    {
        if (match == null || match.Phase != MatchPhase.Finished) return Array.Empty<Row>();
        var scores = match.GetScoresSnapshot().OrderByDescending(x => x.Value).ThenBy(x => x.Key).ToArray();
        var result = new Row[scores.Length];
        int rank = 0;
        for (int i = 0; i < scores.Length; i++)
        {
            if (i == 0 || scores[i].Value != scores[i - 1].Value) rank = i + 1;
            result[i] = new Row(scores[i].Key, scores[i].Value, rank);
        }
        return result;
    }
}
