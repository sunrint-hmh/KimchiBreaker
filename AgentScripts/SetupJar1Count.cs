using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SetupJar1Count
{
    public static object Inspect()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        return new {
            scene = scene.path,
            labels = UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsInactive.Include)
                .Where(x => x.gameObject.scene == scene).Select(x => new {
                    name = x.name, parent = x.transform.parent.name, text = x.text,
                    position = x.rectTransform.anchoredPosition.ToString(), size = x.rectTransform.sizeDelta.ToString()
                }).ToArray(),
            managers = UnityEngine.Object.FindObjectsByType<DrawTicketAllocator>(FindObjectsInactive.Include)
                .Where(x => x.gameObject.scene == scene).Select(x => x.name).ToArray()
        };
    }

    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before changing the saved scene.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Draw") throw new InvalidOperationException("Expected Draw scene.");
        var managers = UnityEngine.Object.FindObjectsByType<DrawTicketAllocator>(FindObjectsInactive.Include)
            .Where(x => x.gameObject.scene == scene).ToArray();
        if (managers.Length != 1) throw new Exception("Expected one DrawTicketAllocator.");
        var labels = UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsInactive.Include)
            .Where(x => x.gameObject.scene == scene && string.Equals(x.name, "Jar1Count", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (labels.Length != 1) throw new Exception("Expected one existing Jar1Count text.");
        var label = labels[0];
        if (label.GetComponentInParent<Canvas>() == null) throw new Exception("Jar1Count has no Canvas.");
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene backup save failed.");
        Directory.CreateDirectory("AgentScripts/Backups");
        string backup = "AgentScripts/Backups/Draw-before-jar1-label-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity.bak";
        File.Copy(scene.path, backup);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configure Jar1Count and connect label");
        Undo.RecordObject(label.gameObject, "Normalize label name");
        label.gameObject.name = "Jar1Count";
        var rect = label.rectTransform;
        Undo.RecordObject(rect, "Position Jar1Count");
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition3D = new Vector3(-375f, -190f, 0f);
        rect.sizeDelta = new Vector2(200f, 60f);
        Undo.RecordObject(label, "Configure Jar1Count text");
        label.text = "0";
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.raycastTarget = false;
        Undo.RecordObject(managers[0], "Connect first allocation label");
        var serialized = new SerializedObject(managers[0]);
        var array = serialized.FindProperty("allocationLabels");
        // Preserve other jar label references if they have already been assigned.
        if (array.arraySize < 4) array.arraySize = 4;
        array.GetArrayElementAtIndex(0).objectReferenceValue = label;
        serialized.ApplyModifiedProperties();
        Canvas.ForceUpdateCanvases();
        // Fit the first jar in the actual Canvas resolution instead of assuming 1920x1080.
        var jarArray = serialized.FindProperty("jarColliders");
        var camera = serialized.FindProperty("drawCamera").objectReferenceValue as Camera;
        if (camera == null) camera = Camera.main;
        var jar = jarArray.arraySize > 0 ? jarArray.GetArrayElementAtIndex(0).objectReferenceValue as Collider : null;
        var parent = rect.parent as RectTransform;
        if (jar != null && camera != null && parent != null)
        {
            Bounds bounds = jar.bounds;
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity, minY = float.PositiveInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 viewport = camera.WorldToViewportPoint(corner);
                minX = Mathf.Min(minX, viewport.x); maxX = Mathf.Max(maxX, viewport.x);
                minY = Mathf.Min(minY, viewport.y);
            }
            rect.anchoredPosition = new Vector2(((minX + maxX) * .5f - .5f) * parent.rect.width,
                (minY - .5f) * parent.rect.height - rect.rect.height * .5f - 12f);
        }
        serialized.Update();
        if (array.GetArrayElementAtIndex(0).objectReferenceValue != label
            || rect.rect.width <= 0 || rect.rect.height <= 0 || label.text != "0")
            throw new Exception("Label verification failed.");
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed.");
        Selection.activeGameObject = label.gameObject;
        return "Jar1Count saved: center anchor, positioned below first jar at " + rect.anchoredPosition + ", size 200x60, centered text 0, raycast off; connected to Allocation Labels[0]. Backup: " + backup;
    }
}
