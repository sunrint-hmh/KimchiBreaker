using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class VerifyMatchFlow
{
    public static object Main()
    {
        int checks = 0;
        Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); checks++; };
        var match = new MatchProgress(6);
        check(match.CurrentRound == 0 && match.Phase == MatchPhase.NotStarted, "Fresh match");
        check(!match.CompleteDraw() && match.GetWinners().Length == 0, "No premature transition/winner");
        for (int round = 1; round <= 6; round++)
        {
            match.BeginRound();
            check(match.CurrentRound == round && match.Phase == MatchPhase.Playing, "Round index");
            bool blocked = false;
            try { match.BeginRound(); } catch (InvalidOperationException) { blocked = true; }
            check(blocked && match.CurrentRound == round, "Duplicate round start");
            match.CompleteRound(new Dictionary<int, int> { { 0, round }, { 1, round } });
            check(match.GetTotal(0) == round * (round + 1) / 2 && match.GetTotal(1) == match.GetTotal(0), "Cumulative score");
            blocked = false;
            try { match.CompleteRound(new Dictionary<int, int> { { 0, 999 } }); }
            catch (InvalidOperationException) { blocked = true; }
            check(blocked, "Duplicate score award");
            if (round < 6)
            {
                check(match.Phase == MatchPhase.Drawing && match.GetWinners().Length == 0, "Draw before next round");
                check(match.CompleteDraw() && !match.CompleteDraw(), "Complete draw once");
            }
        }
        check(match.Phase == MatchPhase.Finished && match.CurrentRound == 6, "Six round finish");
        var winners = match.GetWinners();
        check(winners.Length == 2 && winners[0] == 0 && winners[1] == 1, "Joint victory");
        var scores = match.GetScoresSnapshot(); scores[0] = -100;
        check(match.GetTotal(0) == 21, "Score snapshot isolated");
        bool seventhBlocked = false;
        try { match.BeginRound(); } catch (InvalidOperationException) { seventhBlocked = true; }
        check(seventhBlocked && !match.CompleteDraw(), "No seventh round or final raffle");
        var soloWinner = new MatchProgress(1);
        soloWinner.BeginRound();
        bool negativeBlocked = false;
        try { soloWinner.CompleteRound(new Dictionary<int, int> { { 0, 5 }, { 1, -1 } }); }
        catch (ArgumentOutOfRangeException) { negativeBlocked = true; }
        check(negativeBlocked && soloWinner.GetTotal(0) == 0 && soloWinner.Phase == MatchPhase.Playing, "Invalid scores atomic");
        soloWinner.CompleteRound(new Dictionary<int, int> { { 0, 9 }, { 1, 4 } });
        check(soloWinner.GetWinners().Length == 1 && soloWinner.GetWinners()[0] == 0, "Highest total wins");
        var zeroScores = new MatchProgress(1); zeroScores.BeginRound();
        zeroScores.CompleteRound(new Dictionary<int, int> { { 0, 0 }, { 1, 0 } });
        check(zeroScores.GetWinners().Length == 2, "Zero score tie");
        foreach (string scene in new[] { "Assets/Scenes/InGame.unity", "Assets/Scenes/Draw.unity", "Assets/Scenes/Result.unity" })
            check(UnityEditor.EditorBuildSettings.scenes.Any(x => x.enabled && x.path == scene),
                "Scene disabled/missing: " + scene);
        return new { checksPassed = checks, rounds = 6, jointVictory = true, scenesModified = false };
    }
}
