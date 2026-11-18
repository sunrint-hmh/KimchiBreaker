using UnityEngine;
using UnityEngine.SceneManagement;
public static class InspectMatchScenePaths
{
    public static object Main() => new {
        playing = Application.isPlaying,
        fullPath = Application.CanStreamedLevelBeLoaded("Assets/Scenes/InGame.unity"),
        noExtension = Application.CanStreamedLevelBeLoaded("Assets/Scenes/InGame"),
        name = Application.CanStreamedLevelBeLoaded("InGame"),
        index = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/InGame.unity"),
        global::UnityEditor.EditorBuildSettings.scenes
    };
}
