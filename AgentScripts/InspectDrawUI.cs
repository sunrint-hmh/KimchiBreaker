using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public static class InspectDrawUI
{
    public static object Main()
    {
        var previous = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Draw.unity");
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Draw.unity", OpenSceneMode.Additive);
        try
        {
            var allocator = Object.FindObjectsByType<DrawTicketAllocator>().Single(x => x.gameObject.scene == scene);
            var data = new SerializedObject(allocator);
            var timer = allocator.GetComponent<DrawAllocationCountdown>();
            return new {
                timerLabel = new SerializedObject(timer).FindProperty("timerLabel").objectReferenceValue == null ? "MISSING" : "assigned",
                remainingLabel = data.FindProperty("remainingLabel").objectReferenceValue == null ? "MISSING" : data.FindProperty("remainingLabel").objectReferenceValue.name,
                jars = Enumerable.Range(0,4).Select(i => new { index=i, collider=allocator.GetJarCollider(i).name,
                    enabled=allocator.GetJarCollider(i).enabled, label=allocator.GetAllocationLabel(i) == null ? "MISSING" : allocator.GetAllocationLabel(i).name }).ToArray(),
                graphics = Object.FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsInactive.Include).Where(x=>x.gameObject.scene == scene)
                    .Select(x=>new { name=x.name,raycast=x.raycastTarget }).ToArray(),
                camera=allocator.DrawCamera.name,
                cameraLook=allocator.DrawCamera.GetComponent<SeatedCameraLook>() != null,
                eventSystems=Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Count(x=>x.gameObject.scene==scene)
            };
        }
        finally { if (opened) { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene,true); } }
    }
}
