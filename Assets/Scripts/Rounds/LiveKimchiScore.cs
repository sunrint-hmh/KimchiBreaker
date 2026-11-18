using System;

/// <summary>Recorded rounds plus the active round, counted exactly once.</summary>
public readonly struct LiveKimchiScore
{
    public int RoundCount { get; }
    public long TotalCount { get; }
    private LiveKimchiScore(int roundCount, long totalCount)
    { RoundCount = roundCount; TotalCount = totalCount; }

    public static LiveKimchiScore Read(MatchProgress match, int playerId, int currentRoundCount)
    {
        if (currentRoundCount < 0) throw new ArgumentOutOfRangeException(nameof(currentRoundCount));
        long recorded = match == null ? 0 : match.GetTotal(playerId);
        bool active = match != null && match.Phase == MatchPhase.Playing;
        return new LiveKimchiScore(currentRoundCount, checked(recorded + (active ? currentRoundCount : 0)));
    }
}
