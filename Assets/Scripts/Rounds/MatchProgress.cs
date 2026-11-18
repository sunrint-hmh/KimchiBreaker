using System;
using System.Collections.Generic;
using System.Linq;

public enum MatchPhase { NotStarted, Playing, Drawing, ReadyForNextRound, Finished }

/// <summary>Match rules independent of scenes, ticket spending and skill ownership.</summary>
public sealed class MatchProgress
{
    private readonly Dictionary<int, long> totals = new Dictionary<int, long>();
    public int TotalRounds { get; }
    public int CurrentRound { get; private set; }
    public MatchPhase Phase { get; private set; }

    public MatchProgress(int totalRounds = 6)
    {
        if (totalRounds < 1) throw new ArgumentOutOfRangeException(nameof(totalRounds));
        TotalRounds = totalRounds;
    }

    public void BeginRound()
    {
        if (Phase != MatchPhase.NotStarted && Phase != MatchPhase.ReadyForNextRound)
            throw new InvalidOperationException("The next round is not ready.");
        CurrentRound++;
        Phase = MatchPhase.Playing;
    }

    public void CompleteRound(IReadOnlyDictionary<int, int> earnedKimchi)
    {
        if (Phase != MatchPhase.Playing) throw new InvalidOperationException("No active round to finish.");
        if (earnedKimchi == null || earnedKimchi.Count == 0)
            throw new ArgumentException("Provide at least one player's round score.");
        foreach (var score in earnedKimchi)
            if (score.Value < 0) throw new ArgumentOutOfRangeException(nameof(earnedKimchi));
        foreach (var score in earnedKimchi)
            totals[score.Key] = checked(GetTotal(score.Key) + score.Value);
        Phase = CurrentRound >= TotalRounds ? MatchPhase.Finished : MatchPhase.Drawing;
    }

    public bool CompleteDraw()
    {
        if (Phase != MatchPhase.Drawing) return false;
        Phase = MatchPhase.ReadyForNextRound;
        return true;
    }

    public long GetTotal(int playerId) => totals.TryGetValue(playerId, out long value) ? value : 0;
    public Dictionary<int, long> GetScoresSnapshot() => new Dictionary<int, long>(totals);

    public int[] GetWinners()
    {
        if (Phase != MatchPhase.Finished || totals.Count == 0) return Array.Empty<int>();
        long best = totals.Values.Max();
        return totals.Where(x => x.Value == best).Select(x => x.Key).OrderBy(x => x).ToArray();
    }
}
