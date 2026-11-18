using UnityEngine;

/// <summary>Local prototype result carried from InGame to Draw; not the match's spent-ticket balance.</summary>
public static class RoundSummary
{
    public static int LastRoundKimchiCount { get; private set; }
    public static bool HasResult { get; private set; }
    public static int ResultVersion { get; private set; }

    public static void Record(int kimchiCount)
    {
        LastRoundKimchiCount = Mathf.Max(0, kimchiCount);
        HasResult = true;
        ResultVersion++;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetForNewMatch()
    {
        LastRoundKimchiCount = 0;
        HasResult = false;
        ResultVersion = 0;
    }
}
