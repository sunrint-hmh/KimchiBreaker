using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SetupRound
{
    public static string VerifySaved()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Expected stopped editor.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "InGame") throw new InvalidOperationException("Expected restored InGame scene: " + scene.name);
        var round = UnityEngine.Object.FindAnyObjectByType<RoundController>();
        if (round == null || new SerializedObject(round).FindProperty("durationSeconds").floatValue != 60f)
            throw new InvalidOperationException("Saved duration is not 60 seconds.");
        return "Editor stopped in InGame; saved duration 60 seconds.";
    }

    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "InGame") throw new InvalidOperationException("Open InGame first.");
        var hud = GameObject.Find("GameHUD");
        var camera = GameObject.Find("Main Camera");
        if (hud == null || hud.GetComponent<Canvas>() == null || camera == null)
            throw new InvalidOperationException("GameHUD or Main Camera missing.");
        if (TMPro.TMP_Settings.defaultFontAsset == null)
            throw new InvalidOperationException("TMP essential resources are needed before creating the timer.");
        EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("AgentScripts/Backups");
        File.Copy(scene.path, "AgentScripts/Backups/InGame-before-round-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity.bak");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add round timer and transition");
        var existing = hud.transform.Find("RoundTimer");
        var timer = existing == null ? new GameObject("RoundTimer", typeof(RectTransform)) : existing.gameObject;
        if (existing == null) { Undo.RegisterCreatedObjectUndo(timer, "Create timer"); timer.transform.SetParent(hud.transform, false); }
        var label = timer.GetComponent<TMPro.TextMeshProUGUI>();
        if (label == null) label = Undo.AddComponent<TMPro.TextMeshProUGUI>(timer);
        Undo.RecordObject(label, "Configure timer");
        label.font = TMPro.TMP_Settings.defaultFontAsset;
        label.fontSize = 40;
        label.fontStyle = TMPro.FontStyles.Bold;
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.97f, 0.88f);
        label.raycastTarget = false;
        label.text = "TIME  01:00";
        var rect = timer.GetComponent<RectTransform>();
        Undo.RecordObject(rect, "Position timer");
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -32f);
        rect.sizeDelta = new Vector2(340f, 64f);
        rect.localScale = Vector3.one;
        var manager = GameObject.Find("RoundManager");
        if (manager == null) { manager = new GameObject("RoundManager"); Undo.RegisterCreatedObjectUndo(manager, "Create RoundManager"); }
        var round = manager.GetComponent<RoundController>();
        if (round == null) round = Undo.AddComponent<RoundController>(manager);
        Undo.RecordObject(round, "Connect round");
        var serialized = new SerializedObject(round);
        serialized.FindProperty("durationSeconds").floatValue = 60f;
        serialized.FindProperty("drawScenePath").stringValue = "Assets/Scenes/Draw.unity";
        serialized.FindProperty("player").objectReferenceValue = camera.GetComponent<IngredientClicker>();
        serialized.FindProperty("cameraLook").objectReferenceValue = camera.GetComponent<SeatedCameraLook>();
        serialized.FindProperty("timerLabel").objectReferenceValue = label;
        var spawners = UnityEngine.Object.FindObjectsByType<IngredientSpawner>(FindObjectsSortMode.None);
        var array = serialized.FindProperty("spawners");
        array.arraySize = spawners.Length;
        for (int i = 0; i < spawners.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = spawners[i];
        serialized.ApplyModifiedProperties();
        Undo.CollapseUndoOperations(group);
        Canvas.ForceUpdateCanvases();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Save failed.");
        Selection.activeGameObject = manager;
        return "Saved 60-second timer and RoundManager; spawners=" + spawners.Length;
    }
}
