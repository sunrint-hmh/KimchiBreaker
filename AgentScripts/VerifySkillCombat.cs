using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VerifySkillCombat
{
    public static object Main()
    {
        var players = (IDictionary)typeof(MatchSkillInventory).GetField("Players",
            BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        int id = int.MinValue;
        while (players.Contains(id)) id++;
        int checks = 0;
        Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); checks++; };
        try
        {
            var state = new SkillCombatState();
            state.Configure(Array.Empty<MatchSkillInventory.OwnedSkill>(), 0);
            check(state.BeginClick(1, 4, out bool charged) == 1 && !charged, "Baseline damage");
            check(Mathf.Approximately(state.AimMultiplier, 1) && !state.RecordSuccessfulClick(), "Baseline aim/counter");
            foreach (string name in new[] { "SpicyHands", "BigHands", "ChargedGloves", "AncestralTaste" })
            {
                var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/Skills/" + name + ".asset");
                check(skill != null, "Missing skill asset: " + name);
                MatchSkillInventory.Grant(id, skill);
            }
            state.Configure(MatchSkillInventory.GetSkills(id), 0);
            check(Mathf.Approximately(state.AimMultiplier, 1.2f), "Big hands diameter");
            check(state.BeginClick(1, .5f, out charged) == 2 && !charged, "Flat damage bonus");
            check(state.BeginClick(1, 1.5f, out charged) == 4 && charged, "Charged damage stacks with flat bonus");
            check(state.BeginClick(1, 1.6f, out charged) == 2 && !charged, "Charge consumed");
            int rebuiltDamage = state.BeginClick(1, 2.6f, out charged);
            check(rebuiltDamage == 4 && charged, "Charge rebuilt: damage=" + rebuiltDamage + ", charged=" + charged + ", delay=" + state.ChargeDelay);
            check(state.BeginClick(1, 2.7f, out charged) == 2 && !charged, "Miss also consumes charge");
            for (int i = 0; i < 4; i++) check(!state.RecordSuccessfulClick(), "Ancestor fired before fifth click");
            check(state.AncestorReadyOnNextHit && state.RecordSuccessfulClick(), "Fifth successful click");
            check(state.RecordSuccessfulClick() && state.SuccessfulClicks == 5, "Hold when extra target absent");
            check(state.GetAncestorDamage(1) == 2, "Extra strike must not reuse charged multiplier");
            state.ConsumeAncestorStrike();
            check(state.SuccessfulClicks == 0 && !state.AncestorReadyOnNextHit, "Consumed extra strike");
            state.Configure(Array.Empty<MatchSkillInventory.OwnedSkill>(), 9);
            check(state.DamageBonus == 0 && Mathf.Approximately(state.AimMultiplier, 1) &&
                !state.HasCharge && !state.HasAncestor, "Reset profile");
            return new { checksPassed = checks, sceneModified = false, playerInventoryRestored = true };
        }
        finally { players.Remove(id); }
    }
}
