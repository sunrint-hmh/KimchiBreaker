using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class FitCabbageCollider
{
    private const string PrefabPath = "Assets/Prefabs/NapaCabbage.prefab";
    private const string HullPath = "Assets/Prefabs/NapaCabbageCollision.asset";

    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play before fitting colliders.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "InGame") throw new Exception("Expected InGame scene.");
        EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("AgentScripts/Backups");
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        File.Copy(scene.path, "AgentScripts/Backups/InGame-before-collider-" + stamp + ".unity.bak");
        File.Copy(PrefabPath, "AgentScripts/Backups/NapaCabbage-before-collider-" + stamp + ".prefab.bak");
        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        Mesh hull = null;
        try
        {
            var vertices = new List<Vector3>();
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Matrix4x4 matrix = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (var p in filter.sharedMesh.vertices) vertices.Add(matrix.MultiplyPoint3x4(p));
            }
            if (vertices.Count < 4) throw new Exception("No cabbage geometry found.");
            // Keep extreme surface vertices in many directions. At most 102 vertices =>
            // at most 200 hull faces, below PhysX's convex-mesh limit of 255 faces.
            var support = new List<Vector3>();
            for (int i = 0; i < 102; i++)
            {
                Vector3 direction;
                if (i < 6) direction = (i / 2 == 0 ? Vector3.right : i / 2 == 1 ? Vector3.up : Vector3.forward) * (i % 2 == 0 ? 1 : -1);
                else
                {
                    float y = 1f - 2f * ((i - 6 + 0.5f) / 96f);
                    float radial = Mathf.Sqrt(1f - y * y);
                    float angle = (i - 6) * 2.39996323f;
                    direction = new Vector3(Mathf.Cos(angle) * radial, y, Mathf.Sin(angle) * radial);
                }
                Vector3 best = vertices[0];
                float dot = Vector3.Dot(best, direction);
                foreach (var p in vertices)
                    if (Vector3.Dot(p, direction) > dot) { best = p; dot = Vector3.Dot(p, direction); }
                if (!support.Contains(best)) support.Add(best);
            }
            var mesh = new Mesh { name = "NapaCabbageCollision" };
            mesh.SetVertices(support);
            // Convex cooking builds a closed hull from these referenced surface points.
            var triangles = new List<int>();
            for (int i = 1; i < support.Count - 1; i++) { triangles.Add(0); triangles.Add(i); triangles.Add(i + 1); }
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            Physics.BakeMesh(mesh.GetEntityId(), true);
            hull = AssetDatabase.LoadAssetAtPath<Mesh>(HullPath);
            if (hull == null) { AssetDatabase.CreateAsset(mesh, HullPath); hull = mesh; }
            else { EditorUtility.CopySerialized(mesh, hull); UnityEngine.Object.DestroyImmediate(mesh); }
            Configure(prefab, hull, false);
            PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }

        int count = 0;
        foreach (var ingredient in UnityEngine.Object.FindObjectsByType<KimchiIngredient>())
        {
            if (ingredient.gameObject.scene != scene) continue;
            Configure(ingredient.gameObject, hull, true);
            count++;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Physics.SyncTransforms();
        // Check actual cooked collider occupancy at an empty bounding-box corner.
        var cabbage = UnityEngine.Object.FindAnyObjectByType<KimchiIngredient>();
        var collider = cabbage.GetComponent<MeshCollider>();
        Bounds bounds = hull.bounds;
        Vector3 corner = cabbage.transform.TransformPoint(bounds.min + bounds.size * 0.015f);
        bool emptyCorner = Vector3.Distance(collider.ClosestPoint(corner), corner) > 0.005f;
        if (!emptyCorner) throw new Exception("Convex hull corner still unexpectedly occupied.");
        return $"Convex cabbage collider saved: {hull.vertexCount} support vertices; {count} scene ingredients updated; box removed; empty-corner check passed.";
    }

    private static void Configure(GameObject go, Mesh mesh, bool undo)
    {
        foreach (var box in go.GetComponents<BoxCollider>())
            if (undo) Undo.DestroyObjectImmediate(box); else UnityEngine.Object.DestroyImmediate(box);
        var collider = go.GetComponent<MeshCollider>();
        if (collider == null) collider = undo ? Undo.AddComponent<MeshCollider>(go) : go.AddComponent<MeshCollider>();
        if (undo) Undo.RecordObject(collider, "Fit cabbage physics collider");
        collider.sharedMesh = mesh;
        collider.convex = true;
        collider.contactOffset = 0.002f;
        collider.isTrigger = false;
        collider.enabled = true;
        EditorUtility.SetDirty(collider);
    }
}
