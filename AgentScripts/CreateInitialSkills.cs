using System;
using UnityEngine;
using UnityEditor;

public static class CreateInitialSkills
{
    public static string Main()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Skills")) AssetDatabase.CreateFolder("Assets", "Skills");
        string[] files = { "SpicyHands", "BigHands", "ChargedGloves", "AncestralTaste" };
        string[] names = { "매운 손맛", "큰손", "충전 장갑", "선조의 손맛" };
        int created = 0;
        for (int i = 0; i < files.Length; i++)
        {
            string path = "Assets/Skills/" + files[i] + ".asset";
            var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
            if (skill == null)
            {
                skill = ScriptableObject.CreateInstance<SkillDefinition>();
                var data = new SerializedObject(skill);
                data.FindProperty("kind").enumValueIndex = i;
                data.FindProperty("displayName").stringValue = names[i];
                data.FindProperty("winnerCount").intValue = 1;
                data.FindProperty("availableInDraw").boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(skill, path);
                created++;
            }
            if (skill.Kind != (SkillKind)i || string.IsNullOrWhiteSpace(skill.Description))
                throw new Exception("Invalid skill definition: " + path);
        }
        AssetDatabase.SaveAssets();
        return $"Four skill definitions ready ({created} newly created). Only assets were created; no scene components or UI references changed.";
    }
}
