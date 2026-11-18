using System;
using System.Reflection;
using UnityEngine;

public static class VerifyRound
{
    public static string ArmExpiry()
    {
        var round = UnityEngine.Object.FindAnyObjectByType<RoundController>();
        if (round == null || !round.IsRunning || RoundSummary.HasResult)
            throw new Exception("Expected a fresh round with reset result.");
        typeof(RoundController).GetField("<RemainingSeconds>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(round, 0.2f);
        return "Runtime-only countdown shortened to 0.2 seconds; saved duration remains 60.";
    }

    public static string VerifyExpiry()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Draw"
            || !RoundSummary.HasResult || RoundSummary.LastRoundKimchiCount != 0)
            throw new Exception("Automatic timeout did not transfer the fresh round.");
        return "Automatic countdown expiry loaded Draw and recorded zero; no prior test score leaked.";
    }

    public static string Observe()
    {
        var round = UnityEngine.Object.FindFirstObjectByType<RoundController>();
        if (round == null || !round.IsRunning || round.RemainingSeconds <= 0 || round.RemainingSeconds > 60)
            throw new Exception("Round is not counting down.");
        var label = GameObject.Find("GameHUD/RoundTimer").GetComponent<TMPro.TMP_Text>();
        if (label.text != $"ROUND {round.CurrentRound}/{round.TotalRounds}  TIME {Mathf.CeilToInt(round.RemainingSeconds) / 60:00}:{Mathf.CeilToInt(round.RemainingSeconds) % 60:00}")
            throw new Exception("Timer text and remaining time disagree: " + label.text);
        return $"frame={Time.frameCount}; remaining={round.RemainingSeconds}; label={label.text}";
    }

    public static string Finish()
    {
        var round = UnityEngine.Object.FindFirstObjectByType<RoundController>();
        if (round == null || !round.IsRunning) throw new Exception("Expected active round.");
        var player = UnityEngine.Object.FindFirstObjectByType<IngredientClicker>();
        typeof(IngredientClicker).GetField("kimchiCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 7);
        round.EndRound();
        if (round.IsRunning || round.RemainingSeconds != 0 || player.enabled) throw new Exception("Clicking was not stopped.");
        foreach (var spawner in UnityEngine.Object.FindObjectsByType<IngredientSpawner>())
            if (spawner.enabled) throw new Exception("Spawner still enabled.");
        foreach (var ingredient in UnityEngine.Object.FindObjectsByType<KimchiIngredient>())
            if (ingredient.gameObject.activeInHierarchy) throw new Exception("Ingredient still active.");
        if (Cursor.lockState != CursorLockMode.None || !Cursor.visible) throw new Exception("Cursor not released.");
        if (!RoundSummary.HasResult || RoundSummary.LastRoundKimchiCount != 7) throw new Exception("Result not recorded.");
        round.EndRound(); // Duplicate finish must have no effect.
        return "Stopped input/spawning, cleared ingredients, released cursor, recorded 7, repeated end safely.";
    }

    public static string VerifyDraw()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Draw" || !RoundSummary.HasResult || RoundSummary.LastRoundKimchiCount != 7)
            throw new Exception("Scene transfer or result carry failed: " + scene.name);
        return "Draw scene loaded and score 7 survived the transition.";
    }
}
