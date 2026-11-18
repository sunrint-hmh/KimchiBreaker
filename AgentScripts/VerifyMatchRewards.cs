using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;

public static class VerifyMatchRewards
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Run isolated reward checks outside Play.");
        const BindingFlags Fields = BindingFlags.Static | BindingFlags.NonPublic;
        var fields = typeof(MatchTicketWallet).GetFields(Fields).Where(x=>!x.IsLiteral && !x.IsInitOnly).ToArray();
        var original = fields.Select(x=>x.GetValue(null)).ToArray();
        var players=(IDictionary)typeof(MatchSkillInventory).GetField("Players",Fields).GetValue(null);
        int playerId=int.MinValue;
        while(players.Contains(playerId)) playerId++;
        try
        {
            MatchTicketWallet.ResetForNewMatch();
            if(MatchTicketWallet.OpenRound(1,4)!=4) throw new Exception("Round reward failed.");
            MatchTicketWallet.TrySpendOne(); MatchTicketWallet.TrySpendOne();
            if(MatchTicketWallet.OpenRound(1,4)!=2) throw new Exception("Duplicate reward was claimed.");
            if(MatchTicketWallet.OpenRound(2,3)!=5) throw new Exception("Unused ticket carry failed.");
            for(int i=0;i<5;i++) if(!MatchTicketWallet.TrySpendOne()) throw new Exception("Valid spend rejected.");
            if(MatchTicketWallet.TrySpendOne() || MatchTicketWallet.Balance!=0) throw new Exception("Negative balance allowed.");
            var first=AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/Skills/SpicyHands.asset");
            var second=AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/Skills/BigHands.asset");
            if(first==null || second==null) throw new Exception("Skill definitions missing.");
            if(MatchSkillInventory.Grant(playerId,first)!=1 || MatchSkillInventory.Grant(playerId,first)!=2)
                throw new Exception("Skill acquisition/level record failed.");
            MatchSkillInventory.Grant(playerId,second);
            if(MatchSkillInventory.GetSkills(playerId).Length!=2 || MatchSkillInventory.GetLevel(playerId,first.Kind)!=2)
                throw new Exception("Distinct skill ownership failed.");
            return new { ticketCarry=true, duplicateRewardPrevented=true, negativeBalancePrevented=true,
                skillOwnershipAndLevels=true, editorStateRestored=true };
        }
        finally
        {
            for(int i=0;i<fields.Length;i++) fields[i].SetValue(null,original[i]);
            players.Remove(playerId);
        }
    }
}
