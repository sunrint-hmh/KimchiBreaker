using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SetupOtherJarCounts
{
    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play before scene editing.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Draw") throw new Exception("Expected Draw scene.");
        var managers = UnityEngine.Object.FindObjectsByType<DrawTicketAllocator>(FindObjectsInactive.Include)
            .Where(x => x.gameObject.scene == scene).ToArray();
        if (managers.Length != 1) throw new Exception("Expected one DrawManager.");
        var texts = UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsInactive.Include)
            .Where(x => x.gameObject.scene == scene).ToArray();
        var source = texts.Single(x => x.name == "Jar1Count");
        var serialized = new SerializedObject(managers[0]);
        var jars = serialized.FindProperty("jarColliders");
        if (jars.arraySize != 4) throw new Exception("Expected four jars.");
        var camera = serialized.FindProperty("drawCamera").objectReferenceValue as Camera;
        if (camera == null) camera = Camera.main;
        if (camera == null) throw new Exception("Draw camera is missing.");
        for (int i = 1; i < 4; i++)
            if (jars.GetArrayElementAtIndex(i).objectReferenceValue == null) throw new Exception("Jar reference missing.");
        EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("AgentScripts/Backups");
        string backup = "AgentScripts/Backups/Draw-before-other-labels-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity.bak";
        File.Copy(scene.path, backup);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add remaining jar count labels");
        Undo.RecordObject(managers[0], "Connect jar labels");
        var labels = serialized.FindProperty("allocationLabels");
        if (labels.arraySize < 4) labels.arraySize = 4;
        Canvas.ForceUpdateCanvases();
        for (int i = 1; i < 4; i++)
        {
            string name = "Jar" + (i + 1) + "Count";
            var found = texts.Where(x => string.Equals(x.name, name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (found.Length > 1) throw new Exception("Duplicate label: " + name);
            TMPro.TextMeshProUGUI label;
            if (found.Length == 1) label = found[0];
            else
            {
                var clone = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent, false);
                Undo.RegisterCreatedObjectUndo(clone, "Create " + name);
                label = clone.GetComponent<TMPro.TextMeshProUGUI>();
            }
            Undo.RecordObject(label.gameObject, "Name jar count");
            label.gameObject.name = name;
            Undo.RecordObject(label, "Set count text");
            label.text = "0";
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.raycastTarget = false;
            var rect = label.rectTransform;
            Undo.RecordObject(rect, "Place jar count");
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = source.rectTransform.sizeDelta;
            var parent = rect.parent as RectTransform;
            var jar = jars.GetArrayElementAtIndex(i).objectReferenceValue as Collider;
            Bounds bounds = jar.bounds;
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (cornerIndex & 1) == 0 ? -1 : 1, (cornerIndex & 2) == 0 ? -1 : 1, (cornerIndex & 4) == 0 ? -1 : 1));
                Vector3 viewport = camera.WorldToViewportPoint(corner);
                minX = Mathf.Min(minX, viewport.x); maxX = Mathf.Max(maxX, viewport.x);
            }
            rect.anchoredPosition3D = new Vector3(((minX + maxX) * .5f - .5f) * parent.rect.width,
                source.rectTransform.anchoredPosition.y, 0f);
            labels.GetArrayElementAtIndex(i).objectReferenceValue = label;
        }
        serialized.ApplyModifiedProperties();
        serialized.Update();
        for (int i = 0; i < 4; i++)
        {
            var label = labels.GetArrayElementAtIndex(i).objectReferenceValue as TMPro.TMP_Text;
            if (label == null || label.name != "Jar" + (i + 1) + "Count") throw new Exception("Label connection verification failed.");
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed.");
        Canvas.ForceUpdateCanvases();
        return "Jar2Count, Jar3Count, Jar4Count created/positioned and connected to slots 1,2,3; all four label connections verified; scene saved. Backup: " + backup;
    }
}
