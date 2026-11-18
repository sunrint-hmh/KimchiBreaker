using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>In-memory current match. Network authority and persistent saves are separate.</summary>
public static class MatchSession
{
    public static MatchProgress Progress { get; private set; } = new MatchProgress(6);

    // Lobby/start-game code can call this before loading InGame for a fresh match.
    public static void StartNewMatch()
    {
        Progress = new MatchProgress(6);
        RoundSummary.ResetForNewMatch();
        MatchTicketWallet.ResetForNewMatch();
        MatchSkillInventory.ResetForNewMatch();
    }

    public static void BeginRound()
    {
        if (Progress.Phase == MatchPhase.NotStarted || Progress.Phase == MatchPhase.Finished)
            StartNewMatch();
        if (Progress.Phase == MatchPhase.Playing) return;
        Progress.BeginRound();
    }

    public static void CompleteLocalRound(int playerId, int kimchiCount)
    {
        if (kimchiCount < 0) throw new ArgumentOutOfRangeException(nameof(kimchiCount));
        Progress.CompleteRound(new Dictionary<int, int> { { playerId, kimchiCount } });
        RoundSummary.Record(kimchiCount);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime() => Progress = new MatchProgress(6);
}
