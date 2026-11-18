using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class MoveJarRandomizers
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play before moving components.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Draw") throw new Exception("Expected Draw scene.");
        var manager = UnityEngine.Object.FindObjectsByType<DrawTicketAllocator>().Single(x => x.gameObject.scene == scene);
        var data = new SerializedObject(manager);
        var slots = data.FindProperty("jarColliders");
        var labels = data.FindProperty("allocationLabels");
        if (slots.arraySize != 4 || labels.arraySize < 4) throw new Exception("Expected four jar and label references.");
        var jars = new GameObject[4];
        var counts = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            var collider = slots.GetArrayElementAtIndex(i).objectReferenceValue as Collider;
            var label = labels.GetArrayElementAtIndex(i).objectReferenceValue as TMPro.TMP_Text;
            if (collider == null || label == null) throw new Exception("Missing jar or label reference.");
            jars[i] = PrefabUtility.GetOutermostPrefabInstanceRoot(collider.gameObject) ?? collider.gameObject;
            counts[i] = label.gameObject;
            if (jars[i].GetComponentsInChildren<MeshRenderer>(true).Length == 0) throw new Exception("Jar has no renderers.");
        }
        if (jars.Distinct().Count() != 4) throw new Exception("Duplicate jar roots.");
        EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("AgentScripts/Backups");
        string backup = "AgentScripts/Backups/Draw-before-moving-randomizers-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity.bak";
        File.Copy(scene.path, backup);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Move texture randomizers from labels to jars");
        int removed = 0;
        var transforms = new Vector4[4];
        for (int i = 0; i < 4; i++)
        {
            var misplaced = counts[i].GetComponent<JarTextureRandomizer>();
            var target = jars[i].GetComponent<JarTextureRandomizer>();
            if (target == null) target = Undo.AddComponent<JarTextureRandomizer>(jars[i]);
            Undo.RecordObject(target, "Enable jar texture variation");
            if (misplaced != null)
            {
                var from = new SerializedObject(misplaced);
                var to = new SerializedObject(target);
                to.FindProperty("minimumPatternScale").floatValue = from.FindProperty("minimumPatternScale").floatValue;
                to.FindProperty("maximumPatternScale").floatValue = from.FindProperty("maximumPatternScale").floatValue;
                to.FindProperty("allowHorizontalMirror").boolValue = from.FindProperty("allowHorizontalMirror").boolValue;
                to.ApplyModifiedProperties();
                Undo.DestroyObjectImmediate(misplaced);
                removed++;
            }
            target.enabled = true;
            target.RandomizeTexture();
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            var renderer = jars[i].GetComponentInChildren<MeshRenderer>();
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, 0);
            transforms[i] = block.GetVector(Shader.PropertyToID("_BaseMap_ST"));
            if (transforms[i] == Vector4.zero || counts[i].GetComponent<JarTextureRandomizer>() != null)
                throw new Exception("Randomizer move verification failed.");
        }
        if (transforms.Distinct().Count() != 4) throw new Exception("Expected different UV transforms on all four jars.");
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed.");
        Selection.objects = jars;
        return new { removedFromLabels = removed, jars = jars.Select(x => x.name).ToArray(), uniquePatterns = 4,
            sceneSaved = true, backup };
    }
}
