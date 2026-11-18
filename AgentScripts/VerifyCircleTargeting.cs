using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class VerifyCircleTargeting
{
    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode before setup.");
        var preview = EditorSceneManager.NewPreviewScene();
        int passed = 0;
        try
        {
            var cameraObject = new GameObject("CircleTestCamera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, preview);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.aspect = 1f;
            camera.pixelRect = new Rect(0, 0, 1000, 1000);
            var boxObject = new GameObject("CircleTestBox");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(boxObject, preview);
            var box = boxObject.AddComponent<BoxCollider>();
            box.size = Vector3.one * 0.5f;
            Vector2 center = (Vector2)camera.WorldToScreenPoint(new Vector3(0, 0, 5));
            boxObject.transform.position = new Vector3(0, 0, 5);
            Check(ScreenCircleHitTest.Intersects(camera, box, center, 10), "center target", ref passed);
            boxObject.transform.position = new Vector3(0, 0, 7);
            Check(ScreenCircleHitTest.Intersects(camera, box, center, 10), "overlapped rear target", ref passed);
            boxObject.transform.position = new Vector3(.3f, 0, 5);
            Check(ScreenCircleHitTest.Intersects(camera, box, center, 10), "edge overlap without center ray hit", ref passed);
            boxObject.transform.position = new Vector3(1, 0, 5);
            Check(!ScreenCircleHitTest.Intersects(camera, box, center, 10), "outside circle", ref passed);
            Check(ScreenCircleHitTest.Intersects(camera, box, center, 100), "larger skill area", ref passed);
            boxObject.transform.position = new Vector3(0, 0, -2);
            Check(!ScreenCircleHitTest.Intersects(camera, box, center, 100), "behind camera", ref passed);
            boxObject.transform.position = new Vector3(0, 0, 5);
            box.size = new Vector3(2, .2f, .5f);
            boxObject.transform.rotation = Quaternion.Euler(0, 0, 45);
            Check(!ScreenCircleHitTest.Intersects(camera, box, center + new Vector2(60, -60), 2),
                "rotated box empty AABB corner", ref passed);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }

        var mainCamera = GameObject.Find("Main Camera");
        var crosshair = GameObject.Find("GameHUD/Crosshair");
        if (mainCamera == null || crosshair == null) throw new System.InvalidOperationException("Camera or Crosshair missing.");
        var clicker = mainCamera.GetComponent<IngredientClicker>();
        if (clicker == null) throw new System.InvalidOperationException("IngredientClicker missing.");
        Undo.RecordObject(clicker, "Connect circular hit area");
        var serialized = new SerializedObject(clicker);
        serialized.FindProperty("aimCircle").objectReferenceValue = crosshair.GetComponent<RectTransform>();
        serialized.FindProperty("innerRadiusFraction").floatValue = 25f / 64f;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(clicker);
        var scene = mainCamera.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new System.InvalidOperationException("Scene save failed.");
        return $"{passed} geometry checks passed; circle connected to IngredientClicker and scene saved.";
    }

    private static void Check(bool value, string name, ref int passed)
    {
        if (!value) throw new System.InvalidOperationException("Failed: " + name);
        passed++;
    }
}
