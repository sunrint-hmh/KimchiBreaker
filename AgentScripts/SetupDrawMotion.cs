using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SetupDrawMotion
{
    private const string DrawPath = "Assets/Scenes/Draw.unity";
    private const string PreviewStatePath = "AgentScripts/DrawMotionPreviewState.json";
    [Serializable] private class PreviewState { public string previousStartScene; }

    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play before setup.");
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var draw = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(DrawPath);
        bool openedHere = !draw.isLoaded;
        if (openedHere) draw = EditorSceneManager.OpenScene(DrawPath, OpenSceneMode.Additive);
        try
        {
            var manager = UnityEngine.Object.FindObjectsByType<DrawTicketAllocator>().Single(x => x.gameObject.scene == draw);
            var data = new SerializedObject(manager);
            var slots = data.FindProperty("jarColliders");
            if (slots.arraySize != 4) throw new Exception("Expected four jars.");
            var jars = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                var collider = slots.GetArrayElementAtIndex(i).objectReferenceValue as Collider;
                if (collider == null) throw new Exception("Missing jar collider.");
                jars[i] = PrefabUtility.GetOutermostPrefabInstanceRoot(collider.gameObject) ?? collider.gameObject;
                if (jars[i].GetComponentsInChildren<MeshRenderer>(true).Length == 0) throw new Exception("Jar renderer missing.");
            }
            if (jars.Distinct().Count() != 4) throw new Exception("Duplicate jars.");
            EditorSceneManager.SaveScene(draw);
            Directory.CreateDirectory("AgentScripts/Backups");
            File.Copy(DrawPath, "AgentScripts/Backups/Draw-before-countdown-motion-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity.bak");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Set up draw countdown and floating jars");
            var countdown = manager.GetComponent<DrawAllocationCountdown>();
            if (countdown == null) countdown = Undo.AddComponent<DrawAllocationCountdown>(manager.gameObject);
            Undo.RecordObject(countdown, "Configure 10-second allocation");
            countdown.enabled = true;
            var timerData = new SerializedObject(countdown);
            timerData.FindProperty("durationSeconds").floatValue = 10f;
            timerData.ApplyModifiedProperties();
            foreach (var jar in jars)
            {
                var hover = jar.GetComponent<JarRaffleHover>();
                if (hover == null) hover = Undo.AddComponent<JarRaffleHover>(jar);
                Undo.RecordObject(hover, "Configure 1-second jar motion");
                hover.enabled = true;
                var settings = new SerializedObject(hover);
                settings.FindProperty("cycleSeconds").floatValue = 1f;
                settings.FindProperty("liftHeight").floatValue = .09f;
                settings.FindProperty("rockDegrees").floatValue = 2f;
                settings.FindProperty("sidewaysDistance").floatValue = .01f;
                settings.FindProperty("phaseOffset").floatValue = 0f;
                settings.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(hover);
            }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(draw);
            if (!EditorSceneManager.SaveScene(draw)) throw new Exception("Draw save failed.");
            return "Draw saved: 10-second countdown on manager; one-second hover on four actual jars.";
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
            if (openedHere) EditorSceneManager.CloseScene(draw, true);
        }
    }

    public static string PreparePreview()
    {
        if (EditorApplication.isPlaying) throw new Exception("Expected stopped editor.");
        var previous = EditorSceneManager.playModeStartScene;
        File.WriteAllText(PreviewStatePath, JsonUtility.ToJson(new PreviewState {
            previousStartScene = previous == null ? "" : AssetDatabase.GetAssetPath(previous) }));
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(DrawPath);
        return "Temporary Play start scene set to Draw; previous value recorded.";
    }

    public static string RestorePreview()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop test before restoring editor setting.");
        var previous = JsonUtility.FromJson<PreviewState>(File.ReadAllText(PreviewStatePath));
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous.previousStartScene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous.previousStartScene);
        return "Previous Play start scene restored.";
    }

    public static object Observe()
    {
        var timer = UnityEngine.Object.FindAnyObjectByType<DrawAllocationCountdown>();
        var allocator = UnityEngine.Object.FindAnyObjectByType<DrawTicketAllocator>();
        var hovers = UnityEngine.Object.FindObjectsByType<JarRaffleHover>();
        if (timer == null || allocator == null || !allocator.IsReady || hovers.Length != 4) throw new Exception("Draw test setup missing.");
        return new { frame = Time.frameCount, remaining = timer.RemainingSeconds, locked = allocator.IsAllocationClosed,
            positions = hovers.Select(x => x.transform.localPosition.ToString("F4")).ToArray() };
    }

    public static string VerifyClosed()
    {
        var timer = UnityEngine.Object.FindAnyObjectByType<DrawAllocationCountdown>();
        var allocator = UnityEngine.Object.FindAnyObjectByType<DrawTicketAllocator>();
        if (timer == null || allocator == null || !allocator.IsAllocationClosed || timer.IsRunning || timer.RemainingSeconds != 0f)
            throw new Exception("Countdown has not automatically locked allocation.");
        int remaining = allocator.RemainingTickets;
        int[] counts = allocator.GetAllocationsSnapshot();
        for (int i = 0; i < 4; i++)
            if (allocator.TryDeposit(i)) throw new Exception("Locked allocation accepted an edit.");
        if (remaining != allocator.RemainingTickets || !counts.SequenceEqual(allocator.GetAllocationsSnapshot())) throw new Exception("Locked balances changed.");
        return "Automatic 10-second expiry verified; all four jars reject deposits after closure; balances unchanged.";
    }
}
