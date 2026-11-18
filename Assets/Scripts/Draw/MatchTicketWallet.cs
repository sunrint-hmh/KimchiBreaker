using System;
using UnityEngine;

/// <summary>Local player's spendable tickets, separate from completed-kimchi score.</summary>
public static class MatchTicketWallet
{
    public static int Balance { get; private set; }
    private static int lastClaimedRound;
    private static bool testSeeded;

    public static int OpenRound(int resultVersion, int newlyEarned)
    {
        if (resultVersion <= 0) throw new ArgumentOutOfRangeException(nameof(resultVersion));
        if (newlyEarned < 0) throw new ArgumentOutOfRangeException(nameof(newlyEarned));
        if (resultVersion > lastClaimedRound)
        {
            Balance = checked(Balance + newlyEarned);
            lastClaimedRound = resultVersion;
        }
        return Balance;
    }

    public static int OpenEditorTest(int startingTickets)
    {
        if (!testSeeded && lastClaimedRound == 0)
        {
            Balance = Mathf.Max(0, startingTickets);
            testSeeded = true;
        }
        return Balance;
    }

    public static bool TrySpendOne()
    {
        if (Balance <= 0) return false;
        Balance--;
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetForNewMatch()
    {
        Balance = 0;
        lastClaimedRound = 0;
        testSeeded = false;
    }
}
