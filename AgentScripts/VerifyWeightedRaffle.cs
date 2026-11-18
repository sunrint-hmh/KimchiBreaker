using System;
using System.Linq;

public static class VerifyWeightedRaffle
{
    public static object Main()
    {
        var random = new Random(404);
        var entries = new[] { new RaffleEntry(0,2), new RaffleEntry(1,1), new RaffleEntry(2,1) };
        var counts = new int[3];
        for(int i=0;i<40000;i++) counts[WeightedRaffle.Draw(entries,1,random)[0]]++;
        if(Math.Abs(counts[0]/40000.0-.5)>.015 || Math.Abs(counts[1]/40000.0-.25)>.015
            || Math.Abs(counts[2]/40000.0-.25)>.015) throw new Exception("Weighted distribution failed.");
        for(int i=0;i<1000;i++)
        {
            var winners=WeightedRaffle.Draw(entries,2,random);
            if(winners.Length!=2 || winners.Distinct().Count()!=2) throw new Exception("Duplicate winner.");
        }
        var duplicated = new[] { new RaffleEntry(7,100), new RaffleEntry(7,1), new RaffleEntry(8,1), new RaffleEntry(9,0) };
        var all = WeightedRaffle.Draw(duplicated,5,random);
        if(all.Length!=2 || !all.Contains(7) || !all.Contains(8)) throw new Exception("Duplicate aggregation or zero-ticket exclusion failed.");
        if(WeightedRaffle.Draw(Array.Empty<RaffleEntry>(),1,random).Length!=0) throw new Exception("Empty pool failed.");
        if(WeightedRaffle.Draw(entries,0,random).Length!=0) throw new Exception("Zero winner quota failed.");
        if(WeightedRaffle.Draw(new[]{new RaffleEntry(1,int.MaxValue),new RaffleEntry(2,int.MaxValue)},2,random).Distinct().Count()!=2)
            throw new Exception("Large weights failed.");
        bool rejected=false;
        try { WeightedRaffle.Draw(new[]{new RaffleEntry(1,-1)},1,random); } catch(ArgumentOutOfRangeException) { rejected=true; }
        if(!rejected) throw new Exception("Negative votes accepted.");
        return new { singleWinnerDraws=40000, observed=counts, expected="50% / 25% / 25%", distinctWinnerChecks=1000,
            edgeCases="duplicate players, no votes, zero quota, excess quota, large weights and negative votes checked" };
    }
}
