using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public static class VerifyOcclusion
{
    public static string Main()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var cameraObject = new GameObject("TestCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.transform.position = Vector3.one * 1000f;
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.aspect = 1;
            camera.pixelRect = new Rect(0, 0, 1000, 1000);
            var front = Target(scene, "Front", new Vector3(0, 0, 5));
            var rear = Target(scene, "Rear", new Vector3(0, 0, 7));
            var hits = new Dictionary<KimchiIngredient, Vector3>();
            Vector2 center = camera.WorldToScreenPoint(Vector3.one * 1000f + new Vector3(0, 0, 5));
            Physics.SyncTransforms();
            VisibleCircleTargets.Collect(camera, center, 25f, 20, ~0, 0.75f, hits);
            Check(hits.Count == 1 && hits.ContainsKey(front), "Fully hidden rear was hit.");
            Check(!VisibleCircleTargets.TryFindVisiblePoint(camera, rear, 20, ~0, .75f, out _),
                "Ancestor targeted fully hidden rear.");
            Check(VisibleCircleTargets.TryFindVisiblePoint(camera, front, 20, ~0, .75f, out _),
                "Ancestor failed to find exposed front.");
            rear.transform.position = Vector3.one * 1000f + new Vector3(0.15f, 0, 7);
            Physics.SyncTransforms();
            VisibleCircleTargets.Collect(camera, center, 25f, 20, ~0, 0.75f, hits);
            Check(hits.Count == 2, "Partially visible rear should also be hit.");
            Check(VisibleCircleTargets.TryFindVisiblePoint(camera, rear, 20, ~0, .75f, out _),
                "Ancestor missed exposed rear.");
            front.transform.position = Vector3.one * 1000f + new Vector3(-0.15f, 0, 5);
            Physics.SyncTransforms();
            VisibleCircleTargets.Collect(camera, center, 25f, 20, ~0, 0.75f, hits);
            Check(hits.Count == 2, "Side-by-side targets should both be hit once.");
            var wall = new GameObject("Wall", typeof(BoxCollider));
            SceneManager.MoveGameObjectToScene(wall, scene);
            wall.transform.position = Vector3.one * 1000f + new Vector3(0, 0, 3);
            wall.GetComponent<BoxCollider>().size = Vector3.one;
            Physics.SyncTransforms();
            VisibleCircleTargets.Collect(camera, center, 25f, 20, ~0, 0.75f, hits);
            Check(hits.Count == 0, "Wall should block all targets.");
            Check(!VisibleCircleTargets.TryFindVisiblePoint(camera, front, 20, ~0, .75f, out _) &&
                !VisibleCircleTargets.TryFindVisiblePoint(camera, rear, 20, ~0, .75f, out _),
                "Ancestor targeted through wall.");
            return "8 checks passed: aim circle and extra strike respect visibility and walls.";
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static KimchiIngredient Target(Scene scene, string name, Vector3 position)
    {
        var go = new GameObject(name, typeof(BoxCollider), typeof(Rigidbody));
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.position = Vector3.one * 1000f + position;
        go.GetComponent<BoxCollider>().size = Vector3.one * 0.2f;
        go.GetComponent<Rigidbody>().isKinematic = true;
        return go.AddComponent<KimchiIngredient>();
    }
    private static void Check(bool valid, string error) { if (!valid) throw new Exception(error); }
}
