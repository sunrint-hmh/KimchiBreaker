using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Current-match skills; survives scene changes and resets when a new match starts.</summary>
public static class MatchSkillInventory
{
    public sealed class OwnedSkill
    {
        public SkillDefinition Definition { get; }
        public int Level { get; private set; }
        internal OwnedSkill(SkillDefinition definition) { Definition = definition; Level = 1; }
        internal void Upgrade() => Level = checked(Level + 1);
    }

    private static readonly Dictionary<int, Dictionary<SkillKind, OwnedSkill>> Players =
        new Dictionary<int, Dictionary<SkillKind, OwnedSkill>>();
    public static event Action<int> Changed;

    public static int Grant(int playerId, SkillDefinition definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (!Players.TryGetValue(playerId, out var skills))
        {
            skills = new Dictionary<SkillKind, OwnedSkill>();
            Players.Add(playerId, skills);
        }
        if (skills.TryGetValue(definition.Kind, out var owned)) owned.Upgrade();
        else { owned = new OwnedSkill(definition); skills.Add(definition.Kind, owned); }
        Changed?.Invoke(playerId);
        return owned.Level;
    }

    public static int GetLevel(int playerId, SkillKind kind) =>
        Players.TryGetValue(playerId, out var skills) && skills.TryGetValue(kind, out var owned) ? owned.Level : 0;

    public static OwnedSkill[] GetSkills(int playerId)
    {
        if (!Players.TryGetValue(playerId, out var skills)) return Array.Empty<OwnedSkill>();
        var result = new OwnedSkill[skills.Count];
        skills.Values.CopyTo(result, 0);
        return result;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetForNewMatch()
    {
        var ids = new int[Players.Count];
        Players.Keys.CopyTo(ids, 0);
        Players.Clear();
        foreach (int id in ids) Changed?.Invoke(id);
    }
}
