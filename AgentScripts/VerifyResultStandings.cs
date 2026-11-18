using System;
using System.Collections.Generic;

public static class VerifyResultStandings
{
    public static object Main()
    {
        int checks = 0;
        Action<bool,string> check = (ok, message) => { if (!ok) throw new Exception(message); checks++; };
        check(MatchResultStandings.Build(null).Length == 0, "Null result");
        var match = new MatchProgress(1);
        check(MatchResultStandings.Build(match).Length == 0, "Unstarted result");
        match.BeginRound();
        check(MatchResultStandings.Build(match).Length == 0, "Unfinished result");
        match.CompleteRound(new Dictionary<int,int> { {3,4}, {2,20}, {0,20}, {1,9} });
        var rows = MatchResultStandings.Build(match);
        check(rows.Length == 4, "All participants included");
        check(rows[0].PlayerId == 0 && rows[1].PlayerId == 2 && rows[2].PlayerId == 1 && rows[3].PlayerId == 3, "Score order and stable ties");
        check(rows[0].Rank == 1 && rows[1].Rank == 1 && rows[2].Rank == 3 && rows[3].Rank == 4, "Competition ranks");
        check(rows[0].IsWinner && rows[1].IsWinner && !rows[2].IsWinner, "Joint winners");
        check(rows[0].KimchiCount == 20 && rows[3].KimchiCount == 4, "Actual totals");
        var next = MatchResultStandings.Build(match);
        check(next != rows && match.GetTotal(0) == 20, "No mutation of source");
        var zero = new MatchProgress(1); zero.BeginRound();
        zero.CompleteRound(new Dictionary<int,int>{{0,0},{1,0}});
        var zeroRows = MatchResultStandings.Build(zero);
        check(zeroRows.Length == 2 && zeroRows[0].IsWinner && zeroRows[1].IsWinner, "Zero tie");
        var large = new MatchProgress(2);
        large.BeginRound(); large.CompleteRound(new Dictionary<int,int>{{0,int.MaxValue}});
        large.CompleteDraw(); large.BeginRound(); large.CompleteRound(new Dictionary<int,int>{{0,int.MaxValue}});
        check(MatchResultStandings.Build(large)[0].KimchiCount == 2L * int.MaxValue, "Long total");
        return new { checksPassed = checks, scenesModified = false };
    }
}
