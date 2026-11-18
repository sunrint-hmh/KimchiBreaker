using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

public static class SetupCrosshair
{
    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "InGame") throw new System.InvalidOperationException("Expected InGame scene.");
        var hud = GameObject.Find("GameHUD");
        if (hud == null || hud.GetComponent<Canvas>() == null)
            throw new System.InvalidOperationException("GameHUD Canvas not found.");
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/CrosshairRing.png");
        if (sprite == null) throw new System.InvalidOperationException("Circle sprite not found.");
        // Preserve the user's latest scene state before this targeted edit.
        if (!EditorSceneManager.SaveScene(scene)) throw new System.InvalidOperationException("Scene save failed.");
        Directory.CreateDirectory("AgentScripts/Backups");
        string backup = "AgentScripts/Backups/InGame-before-crosshair-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity.bak";
        File.Copy(scene.path, backup, false);

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set up circular crosshair");
        var child = hud.transform.Find("Crosshair");
        GameObject go;
        if (child == null)
        {
            go = new GameObject("Crosshair", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            Undo.RegisterCreatedObjectUndo(go, "Create Crosshair");
            go.transform.SetParent(hud.transform, false);
        }
        else go = child.gameObject;
        var rect = go.GetComponent<RectTransform>();
        var image = go.GetComponent<UnityEngine.UI.Image>();
        if (image == null) image = Undo.AddComponent<UnityEngine.UI.Image>(go);
        Undo.RecordObject(rect, "Center crosshair");
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition3D = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        Undo.RecordObject(image, "Set circle sprite");
        image.sprite = sprite;
        image.color = Color.white;
        image.type = UnityEngine.UI.Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;
        var size = go.GetComponent<CrosshairSize>();
        if (size == null) size = Undo.AddComponent<CrosshairSize>(go);
        var serialized = new SerializedObject(size);
        serialized.FindProperty("baseDiameter").floatValue = 24f;
        serialized.FindProperty("skillMultiplier").floatValue = 1f;
        serialized.ApplyModifiedProperties();
        size.SetSkillMultiplier(1f);
        go.SetActive(true);
        EditorUtility.SetDirty(image);
        EditorUtility.SetDirty(size);
        Undo.CollapseUndoOperations(group);
        Canvas.ForceUpdateCanvases();
        if (!image.sprite || rect.rect.width <= 0 || rect.rect.height <= 0)
            throw new System.InvalidOperationException("Crosshair validation failed.");
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new System.InvalidOperationException("Updated scene save failed.");
        Selection.activeGameObject = go;
        return $"Crosshair saved: diameter={size.Diameter}, rect={rect.rect.size}, sprite={image.sprite.name}; backup={backup}";
    }
}
