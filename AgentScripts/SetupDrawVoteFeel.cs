using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SetupDrawVoteFeel
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play before setup.");
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var draw = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Draw.unity");
        bool openedHere = !draw.isLoaded;
        if (openedHere) draw = EditorSceneManager.OpenScene("Assets/Scenes/Draw.unity", OpenSceneMode.Additive);
        try
        {
            var allocator = UnityEngine.Object.FindObjectsByType<DrawTicketAllocator>().Single(x => x.gameObject.scene == draw);
            var data = new SerializedObject(allocator);
            var slots = data.FindProperty("jarColliders");
            var hovers = new JarRaffleHover[4];
            for (int i = 0; i < 4; i++)
            {
                var collider = slots.GetArrayElementAtIndex(i).objectReferenceValue as Collider;
                hovers[i] = collider == null ? null : collider.GetComponentInParent<JarRaffleHover>();
                if (hovers[i] == null) throw new Exception("Missing hover on jar " + i);
            }
            if (allocator.DrawCamera == null) throw new Exception("Draw camera missing.");
            EditorSceneManager.SaveScene(draw);
            File.Copy(draw.path, "AgentScripts/Backups/Draw-before-vote-feel-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity.bak");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add independent vote-driven jar feedback");
            var remainingText = UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsInactive.Include)
                .SingleOrDefault(x => x.gameObject.scene == draw && x.name == "RemainingKimchi");
            if (remainingText != null)
            {
                Undo.RecordObject(allocator, "Connect remaining kimchi counter");
                data.FindProperty("remainingLabel").objectReferenceValue = remainingText;
                data.ApplyModifiedProperties();
            }
            foreach (var hover in hovers)
            {
                Undo.RecordObject(hover, "Randomize jar motion");
                var settings = new SerializedObject(hover);
                settings.FindProperty("randomizeMotion").boolValue = true;
                settings.FindProperty("votesForStrongEffect").floatValue = 10f;
                settings.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(hover);
            }
            const string materialPath = "Assets/UI/DrawVoteSpark.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetColor("_BaseColor", Color.white); // Preserve the explicitly colored red/lime particles.
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            var feedback = allocator.GetComponent<DrawVoteFeedback>();
            if (feedback == null) feedback = Undo.AddComponent<DrawVoteFeedback>(allocator.gameObject);
            Undo.RecordObject(feedback, "Assign vote feedback material");
            var serialized = new SerializedObject(feedback);
            serialized.FindProperty("particleMaterial").objectReferenceValue = material;
            serialized.ApplyModifiedProperties();
            var audio = allocator.GetComponent<AudioSource>();
            Undo.RecordObject(audio, "Configure UI vote audio");
            audio.playOnAwake = false;
            audio.spatialBlend = 0;
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(draw);
            EditorSceneManager.SaveScene(draw);
            return "Independent jar motion, vote-driven punch/urgency/camera/counter/sparks/audio wired. Pipeline="
                + UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name;
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
            if (openedHere) EditorSceneManager.CloseScene(draw, true);
        }
    }

    public static object Probe()
    {
        var allocator = UnityEngine.Object.FindAnyObjectByType<DrawTicketAllocator>();
        var timer = UnityEngine.Object.FindAnyObjectByType<DrawAllocationCountdown>();
        var feedback = UnityEngine.Object.FindAnyObjectByType<DrawVoteFeedback>();
        if (allocator == null || !allocator.IsReady || feedback == null) throw new Exception("Draw feedback is not running.");
        // Runtime-only extension gives verification time; the saved allocation window stays 10s.
        typeof(DrawAllocationCountdown).GetField("<RemainingSeconds>k__BackingField", Private).SetValue(timer, 60f);
        var hovers = Enumerable.Range(0, 4).Select(i => allocator.GetJarCollider(i).GetComponentInParent<JarRaffleHover>()).ToArray();
        var phases = hovers.Select(x => (float)typeof(JarRaffleHover).GetField("phase", Private).GetValue(x)).ToArray();
        if (phases.Distinct().Count() != 4) throw new Exception("Motion phases are not independent.");
        float fov = allocator.DrawCamera.fieldOfView;
        int pools = UnityEngine.Object.FindObjectsByType<ParticleSystem>().Length;
        for (int i = 0; i < 8; i++) if (!allocator.TryDeposit(0)) throw new Exception("Vote deposit failed.");
        if (!allocator.TryDeposit(1) || !allocator.TryDeposit(2)) throw new Exception("Secondary vote failed.");
        if (!(hovers[0].VoteStrength > hovers[1].VoteStrength && hovers[1].VoteStrength > hovers[3].VoteStrength))
            throw new Exception("More votes did not increase jar intensity.");
        var particles = feedback.GetComponentInChildren<ParticleSystem>();
        if (particles.particleCount == 0) throw new Exception("Vote particles were not emitted.");
        if (pools != UnityEngine.Object.FindObjectsByType<ParticleSystem>().Length) throw new Exception("Particle pool leaked.");
        if (!feedback.GetComponent<AudioSource>().isPlaying) throw new Exception("Vote audio did not play.");
        var remainingText = new SerializedObject(allocator).FindProperty("remainingLabel").objectReferenceValue as TMPro.TMP_Text;
        if (remainingText == null || remainingText.text.Split(':').Last().Trim() != allocator.RemainingTickets.ToString())
            throw new Exception("Remaining kimchi label does not match the balance.");
        DrawVoteFeedback.SfxVolume = 0f;
        feedback.SendMessage("Update");
        if (feedback.GetComponent<AudioSource>().volume != 0) throw new Exception("SFX gain did not mute the source.");
        DrawVoteFeedback.SfxVolume = 1f;
        feedback.SendMessage("Update");
        // Preview one busy frame, then pause so an on-demand capture keeps the feedback visible.
        foreach (var hover in hovers)
        {
            typeof(JarRaffleHover).GetField("pulseAge", Private).SetValue(hover, .07f);
            hover.SendMessage("LateUpdate");
        }
        var ages = (float[])typeof(DrawVoteFeedback).GetField("labelPulseAges", Private).GetValue(feedback);
        ages[0] = .07f;
        feedback.SendMessage("LateUpdate");
        particles.Simulate(.07f, true, false, false);
        particles.Pause();
        if (Mathf.Abs(allocator.DrawCamera.fieldOfView - fov) < .1f) throw new Exception("Camera zoom kick missing.");
        EditorApplication.isPaused = true;
        return new { checks = "independent phases, stronger voted jars, one particle pool, playing audio, SFX gain, camera kick",
            phases, votes = allocator.GetAllocationsSnapshot(), particleCount = particles.particleCount,
            fovBefore = fov, fovDuringImpact = allocator.DrawCamera.fieldOfView };
    }

    public static string Resume()
    {
        var feedback = UnityEngine.Object.FindAnyObjectByType<DrawVoteFeedback>();
        feedback.GetComponentInChildren<ParticleSystem>().Play();
        EditorApplication.isPaused = false;
        return "Feedback test resumed.";
    }

    public static string VerifyRecovery()
    {
        var feedback = UnityEngine.Object.FindAnyObjectByType<DrawVoteFeedback>();
        var allocator = feedback.GetComponent<DrawTicketAllocator>();
        var camera = allocator.DrawCamera;
        var baseline = (float)typeof(DrawVoteFeedback).GetField("cameraFov", Private).GetValue(feedback);
        if (Mathf.Abs(camera.fieldOfView-baseline) > .001f) throw new Exception("Camera FOV did not recover.");
        for (int i = 0; i < 4; i++)
        {
            var hover = allocator.GetJarCollider(i).GetComponentInParent<JarRaffleHover>();
            var scale = (Vector3)typeof(JarRaffleHover).GetField("restScale", Private).GetValue(hover);
            if (Vector3.Distance(scale, hover.transform.localScale) > .001f) throw new Exception("Jar scale did not recover.");
            var label = allocator.GetAllocationLabel(i);
            if (label != null && Vector3.Distance(label.transform.localScale, Vector3.one) > .001f) throw new Exception("Counter pop did not recover.");
        }
        return "Camera zoom and all jar/counter scales returned to baseline after impact.";
    }

    public static string FinishWindow()
    {
        var timer = UnityEngine.Object.FindAnyObjectByType<DrawAllocationCountdown>();
        typeof(DrawAllocationCountdown).GetField("<RemainingSeconds>k__BackingField", Private).SetValue(timer, .01f);
        return "Runtime-only test window shortened for deadline verification.";
    }

    public static object VerifySaved()
    {
        if (EditorApplication.isPlaying) throw new Exception("Expected stopped editor for final saved-state check.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var allocator = UnityEngine.Object.FindObjectsByType<DrawTicketAllocator>().Single(x => x.gameObject.scene == scene);
        var data = new SerializedObject(allocator);
        if (data.FindProperty("remainingLabel").objectReferenceValue == null) throw new Exception("Remaining label unassigned.");
        var feedback = allocator.GetComponent<DrawVoteFeedback>();
        if (feedback == null || !feedback.enabled || new SerializedObject(feedback).FindProperty("particleMaterial").objectReferenceValue == null)
            throw new Exception("Feedback or retained particle material missing.");
        var timer = allocator.GetComponent<DrawAllocationCountdown>();
        if (new SerializedObject(timer).FindProperty("durationSeconds").floatValue != 10f) throw new Exception("Saved timer changed.");
        for (int i = 0; i < 4; i++)
        {
            var hover = allocator.GetJarCollider(i).GetComponentInParent<JarRaffleHover>();
            if (hover == null || !new SerializedObject(hover).FindProperty("randomizeMotion").boolValue)
                throw new Exception("Independent hover missing.");
        }
        return new { savedSeconds = 10, independentJars = 4, feedbackEnabled = true, remainingLabelConnected = true,
            sceneDirty = scene.isDirty, playMode = "stopped" };
    }
}
