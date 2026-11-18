using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine.TextCore.LowLevel;

public static class SetupFirstSkillName
{
    private const string FontFolder = "Assets/UI/Fonts/NanumGothic/";

    public static string FromCurrentScene()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play before saving UI.");
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var draw = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Draw.unity");
        bool openedHere = !draw.isLoaded;
        if (openedHere) draw = EditorSceneManager.OpenScene("Assets/Scenes/Draw.unity", OpenSceneMode.Additive);
        try
        {
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(draw);
            return Main();
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
            if (openedHere) EditorSceneManager.CloseScene(draw, true);
        }
    }

    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play before saving UI.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Draw") throw new Exception("Expected Draw scene.");
        var labels = UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include)
            .Where(x => x.gameObject.scene == scene).ToArray();
        var source = labels.Single(x => x.name == "Jar1Count");
        if (source.GetComponentInParent<Canvas>() == null) throw new Exception("Source label has no Canvas.");
        var matches = labels.Where(x => x.name == "Jar1SkillName").ToArray();
        if (matches.Length > 1) throw new Exception("Duplicate skill names found.");
        AssetDatabase.Refresh();
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "NanumGothic-Regular.ttf");
        if (sourceFont == null) throw new Exception("Korean source font missing.");
        var fallback = FontAsset("NanumGothic Dynamic SDF", sourceFont, false);
        var font = FontAsset("SkillLabels SDF", sourceFont, true);
        if (font.fallbackFontAssetTable == null) font.fallbackFontAssetTable = new List<TMP_FontAsset>();
        if (!font.fallbackFontAssetTable.Contains(fallback)) font.fallbackFontAssetTable.Add(fallback);
        if (!font.HasCharacter('스') || !font.HasCharacter('킬')) throw new Exception("Korean title glyph validation failed.");
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();

        EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("AgentScripts/Backups");
        string backup = "AgentScripts/Backups/Draw-before-skill-name-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity.bak";
        File.Copy(scene.path, backup);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add first jar skill title");
        TextMeshProUGUI label;
        if (matches.Length == 1) label = matches[0];
        else
        {
            var clone = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent, false);
            Undo.RegisterCreatedObjectUndo(clone, "Create first skill title");
            clone.name = "Jar1SkillName";
            label = clone.GetComponent<TextMeshProUGUI>();
        }
        Undo.RecordObject(label, "Set first skill title");
        label.font = font;
        label.fontSharedMaterial = font.material;
        label.text = "스킬 1";
        label.fontSize = 36f;
        label.enableAutoSizing = false;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        var rect = label.rectTransform;
        Undo.RecordObject(rect, "Place first skill title");
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition3D = new Vector3(source.rectTransform.anchoredPosition.x, 220f, 0f);
        rect.sizeDelta = new Vector2(360f, 60f);
        label.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        label.ForceMeshUpdate();
        if (label.textInfo.characterCount != 4) throw new Exception("Unexpected title character count.");
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed.");
        Selection.activeGameObject = label.gameObject;
        return "Jar1SkillName saved above jar 1: 스킬 1, white/36pt, centered, raycast off; Korean glyphs verified; other labels preserved.";
    }

    private static TMP_FontAsset FontAsset(string name, Font source, bool isStatic)
    {
        string path = FontFolder + name + ".asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font != null) return font;
        font = TMP_FontAsset.CreateFontAsset(source, 42, 4, GlyphRenderMode.SDFAA, 512, 512,
            AtlasPopulationMode.Dynamic, !isStatic);
        if (font == null) throw new Exception("Font generation failed: " + name);
        font.name = name;
        if (!font.TryAddCharacters(isStatic ? "스킬 1234" : "한글", out string missing))
            throw new Exception("Missing glyphs: " + missing);
        font.faceInfo = WithScaleOne(font.faceInfo);
        if (isStatic) font.atlasPopulationMode = AtlasPopulationMode.Static;
        else
        {
            var serialized = new SerializedObject(font);
            serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.CreateAsset(font, path);
        foreach (var texture in font.atlasTextures)
            if (texture != null && !AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, font);
        if (font.material != null && !AssetDatabase.Contains(font.material)) AssetDatabase.AddObjectToAsset(font.material, font);
        EditorUtility.SetDirty(font);
        return font;
    }

    private static UnityEngine.TextCore.FaceInfo WithScaleOne(UnityEngine.TextCore.FaceInfo face)
    {
        face.scale = 1f;
        return face;
    }
}
