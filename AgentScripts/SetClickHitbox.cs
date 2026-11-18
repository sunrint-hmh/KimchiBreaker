using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetClickHitbox
{
    public static string Main()
    {
        // Test the reduced hit region independently of physical simulation.
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var cameraObject = new GameObject("HitboxTestCamera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, preview);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.aspect = 1f;
            camera.pixelRect = new Rect(0, 0, 1000, 1000);
            var target = new GameObject("HitboxTestBox");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(target, preview);
            var box = target.AddComponent<BoxCollider>();
            box.size = Vector3.one * 0.5f;
            target.transform.position = new Vector3(0.33f, 0, 5);
            Vector2 center = camera.WorldToScreenPoint(new Vector3(0, 0, 5));
            if (!ScreenCircleHitTest.Intersects(camera, box, center, 10f, 1f)) throw new Exception("Baseline edge test failed.");
            if (ScreenCircleHitTest.Intersects(camera, box, center, 10f, 0.75f)) throw new Exception("Reduced edge still hits.");
            target.transform.position = new Vector3(0, 0, 5);
            if (!ScreenCircleHitTest.Intersects(camera, box, center, 10f, 0.75f)) throw new Exception("Center target missed.");
            target.transform.position = new Vector3(0, 0, 7);
            if (!ScreenCircleHitTest.Intersects(camera, box, center, 10f, 0.75f)) throw new Exception("Overlapping rear target missed.");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }

        var player = UnityEngine.Object.FindAnyObjectByType<IngredientClicker>();
        if (player == null) throw new Exception("IngredientClicker missing from current scene.");
        Undo.RecordObject(player, "Reduce click hitbox");
        var serialized = new SerializedObject(player);
        serialized.FindProperty("hitboxScale").floatValue = 0.75f;
        serialized.ApplyModifiedProperties();
        if (!EditorApplication.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
        }
        return "Click hitbox set to 75%; four geometry checks passed; physics colliders unchanged. Playing=" + EditorApplication.isPlaying;
    }
}
