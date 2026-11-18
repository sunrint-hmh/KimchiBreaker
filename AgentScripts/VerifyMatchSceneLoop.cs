using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Temporary Play-only fixture. Never saves a scene or changes serialized assets.
public static class VerifyMatchSceneLoop
{
    private static int finishedRound, drawnRound, draws;
    private static double deadline;
    private static readonly HashSet<int> countdownRounds = new HashSet<int>();
    private static readonly string report = "AgentScripts/RoundCountdown-review.json";
    public static string Arm()
    {
        if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "InGame" ||
            MatchSession.Progress.CurrentRound != 1)
            throw new Exception("Expected our fresh InGame Play session.");
        finishedRound = drawnRound = draws = 0;
        countdownRounds.Clear();
        deadline = EditorApplication.timeSinceStartup + 60;
        if (File.Exists(report)) File.Delete(report);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        return "Temporary six-round scene loop armed; no scene saves.";
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) { Finish(false, "Play stopped before completion."); return; }
            if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Scene loop timed out."); return; }
            string scene = SceneManager.GetActiveScene().name;
            int round = MatchSession.Progress.CurrentRound;
            if (scene == "InGame")
            {
                var controller = UnityEngine.Object.FindAnyObjectByType<RoundController>();
                if (controller != null && controller.IsCountingDown)
                {
                    if (controller.IsRunning || Mathf.Abs(controller.RemainingSeconds - 60f) > .001f)
                        throw new Exception("Round time consumed during preparation.");
                    var clicker = UnityEngine.Object.FindAnyObjectByType<IngredientClicker>();
                    if (clicker != null && clicker.enabled) throw new Exception("Attacking enabled during preparation.");
                    foreach (var spawner in UnityEngine.Object.FindObjectsByType<IngredientSpawner>())
                        if (spawner.enabled) throw new Exception("Spawning enabled during preparation.");
                    if (countdownRounds.Add(round))
                    {
                        int version = RoundSummary.ResultVersion;
                        controller.EndRound();
                        if (!controller.IsCountingDown || RoundSummary.ResultVersion != version)
                            throw new Exception("Preparation incorrectly finished a round.");
                    }
                    return;
                }
                if (controller == null || !controller.IsRunning || round <= finishedRound) return;
                var player = UnityEngine.Object.FindAnyObjectByType<IngredientClicker>();
                if (round > 1)
                {
                    var effects = player.GetComponent<PlayerSkillEffects>();
                    if (effects == null || effects.State.DamageBonus != round - 1)
                        throw new Exception("Awarded skill did not carry into round " + round);
                    if (MatchTicketWallet.Balance != (round - 1) * 2)
                        throw new Exception("Unspent ticket carry mismatch.");
                }
                typeof(IngredientClicker).GetField("kimchiCount", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(player, 3);
                finishedRound = round;
                controller.EndRound();
            }
            else if (scene == "Draw")
            {
                if (drawnRound >= round) return;
                var allocator = UnityEngine.Object.FindAnyObjectByType<DrawTicketAllocator>();
                if (allocator == null || !allocator.IsReady) return;
                var resolver = allocator.GetComponent<DrawRaffleResolver>();
                if (resolver == null) throw new Exception("DrawRaffleResolver missing.");
                if (!allocator.TryDeposit(0)) throw new Exception("Test deposit rejected.");
                typeof(DrawRaffleResolver).GetField("revealInterval", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(resolver, 0f);
                var transition = allocator.GetComponent<DrawRoundTransition>();
                if (transition == null) transition = allocator.gameObject.AddComponent<DrawRoundTransition>();
                typeof(DrawRoundTransition).GetField("resultHoldSeconds", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(transition, .1f);
                drawnRound = round; draws++;
                allocator.CloseAllocation();
            }
            else if (scene == "Result")
            {
                bool ok = round == 6 && draws == 5 && MatchSession.Progress.Phase == MatchPhase.Finished &&
                    MatchSession.Progress.GetTotal(0) == 18 && MatchTicketWallet.Balance == 10 &&
                    MatchSkillInventory.GetLevel(0, SkillKind.SpicyHands) == 5 && countdownRounds.Count >= 5;
                Finish(ok, ok ? "6 rounds, 5 draws, skill effects and tickets carried; total 18 despite 5 spent tickets."
                    : "Final state did not match expected result.");
            }
        }
        catch (Exception ex) { Finish(false, ex.ToString()); }
    }

    private static void Finish(bool passed, string detail)
    {
        EditorApplication.update -= Tick;
        File.WriteAllText(report, JsonUtility.ToJson(new Review { passed = passed, detail = detail,
            completedRounds = finishedRound, draws = draws, countdownRoundsChecked = countdownRounds.Count, scenesSaved = false }, true));
    }
    [Serializable] private sealed class Review
    {
        public bool passed, scenesSaved;
        public int completedRounds, draws, countdownRoundsChecked;
        public string detail;
    }
}
