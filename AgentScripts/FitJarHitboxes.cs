using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class FitJarHitboxes
{
    private const string PrefabPath = "Assets/Prefabs/SkillJar.prefab";

    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play before changing saved hitboxes.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Draw") throw new Exception("Expected Draw scene.");
        var manager = UnityEngine.Object.FindObjectsByType<DrawTicketAllocator>()
            .Single(x => x.gameObject.scene == scene);
        var original = new SerializedObject(manager);
        var array = original.FindProperty("jarColliders");
        if (array.arraySize != 4) throw new Exception("Expected four jar references.");
        var roots = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            var collider = array.GetArrayElementAtIndex(i).objectReferenceValue as Collider;
            if (collider == null) throw new Exception("Missing jar collider " + i);
            roots[i] = PrefabUtility.GetOutermostPrefabInstanceRoot(collider.gameObject) ?? collider.gameObject;
            Validate(roots[i]);
        }
        if (roots.Distinct().Count() != 4) throw new Exception("Duplicate jar roots.");
        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Validate(prefab);
            EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory("AgentScripts/Backups");
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(scene.path, "AgentScripts/Backups/Draw-before-jar-hitboxes-" + stamp + ".unity.bak");
            File.Copy(PrefabPath, "AgentScripts/Backups/SkillJar-before-hitboxes-" + stamp + ".prefab.bak");
            Configure(prefab, false);
            PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Fit jar selection to mesh surfaces");
        Undo.RecordObject(manager, "Reconnect jar hitboxes");
        var updated = new SerializedObject(manager);
        var slots = updated.FindProperty("jarColliders");
        var colliders = new MeshCollider[4];
        for (int i = 0; i < 4; i++)
        {
            colliders[i] = Configure(roots[i], true);
            slots.GetArrayElementAtIndex(i).objectReferenceValue = colliders[i];
        }
        updated.ApplyModifiedProperties();
        Physics.SyncTransforms();
        var camera = updated.FindProperty("drawCamera").objectReferenceValue as Camera;
        if (camera == null) camera = Camera.main;
        if (camera == null) throw new Exception("Draw camera missing.");
        int checks = 0;
        for (int i = 0; i < 4; i++)
        {
            var collider = colliders[i];
            var filter = collider.GetComponent<MeshFilter>();
            var bounds = filter.sharedMesh.bounds;
            Vector3 center = filter.transform.TransformPoint(bounds.center);
            if (!collider.Raycast(new Ray(camera.transform.position, (center-camera.transform.position).normalized), out _, 100f))
                throw new Exception("Jar body click test failed: " + roots[i].name);
            checks++;
            // Ray through the empty upper corner of the old box must miss the curved jar.
            Vector3 corner = bounds.center + new Vector3(bounds.extents.x * .97f, bounds.extents.y * .97f, -bounds.extents.z - 1f);
            var ray = new Ray(filter.transform.TransformPoint(corner), filter.transform.TransformDirection(Vector3.forward));
            if (collider.Raycast(ray, out _, 100f)) throw new Exception("Empty corner still hits: " + roots[i].name);
            checks++;
            if (roots[i].GetComponentsInChildren<BoxCollider>(true).Length != 0)
                throw new Exception("Old box collider remains.");
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed.");
        AssetDatabase.SaveAssets();
        return $"Four jars and prefab now use exact static mesh surfaces; all four allocator slots reconnected; {checks} body/empty-corner ray checks passed; Draw saved.";
    }

    private static void Validate(GameObject root)
    {
        var filters = root.GetComponentsInChildren<MeshFilter>(true).Where(x => x.sharedMesh != null).ToArray();
        if (filters.Length != 1) throw new Exception("Expected a single jar mesh: " + root.name);
        if (root.GetComponentsInChildren<Rigidbody>(true).Any(x => !x.isKinematic))
            throw new Exception("Jar must remain static for an exact non-convex surface collider.");
    }

    private static MeshCollider Configure(GameObject root, bool undo)
    {
        foreach (var box in root.GetComponentsInChildren<BoxCollider>(true))
            if (undo) Undo.DestroyObjectImmediate(box); else UnityEngine.Object.DestroyImmediate(box);
        var filter = root.GetComponentsInChildren<MeshFilter>(true).Single(x => x.sharedMesh != null);
        var collider = filter.GetComponent<MeshCollider>();
        if (collider == null) collider = undo ? Undo.AddComponent<MeshCollider>(filter.gameObject) : filter.gameObject.AddComponent<MeshCollider>();
        if (undo) Undo.RecordObject(collider, "Set exact jar surface");
        collider.sharedMesh = filter.sharedMesh;
        collider.convex = false;
        collider.isTrigger = false;
        collider.enabled = true;
        EditorUtility.SetDirty(collider);
        if (undo) PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
        return collider;
    }
}
