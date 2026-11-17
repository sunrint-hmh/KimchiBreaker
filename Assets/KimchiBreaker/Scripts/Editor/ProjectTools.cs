using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KimchiBreaker.Net;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KimchiBreaker.EditorTools
{
    /// <summary>
    /// 에디터 메뉴: Kimchi Breaker/...
    /// - 프로젝트 점검: 네트워크 프리팹, 씬, 빌드 설정이 올바른지 확인한다.
    /// - Play 를 누르면 항상 Main 씬부터 시작하도록 설정한다(다른 씬을 열어 둔 상태에서도).
    /// - Windows 빌드.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectTools
    {
        public const string MainScenePath = "Assets/KimchiBreaker/Scenes/Main.unity";

        static readonly string[] s_NetworkPrefabPaths =
        {
            NetworkPrefabCatalog.MatchStatePath,
            NetworkPrefabCatalog.PlayerAvatarPath,
            NetworkPrefabCatalog.PracticeIngredientPath,
        };

        static ProjectTools()
        {
            EditorApplication.delayCall += () =>
            {
                ApplyPlayModeStartScene();
                if (!ValidateProject(out List<string> problems, logSuccess: false))
                {
                    Debug.LogError("[KimchiBreaker] 프로젝트 점검에서 문제가 발견되었습니다:\n- " + string.Join("\n- ", problems));
                }
            };
        }

        static void ApplyPlayModeStartScene()
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath);
            if (scene != null && EditorSceneManager.playModeStartScene != scene)
            {
                EditorSceneManager.playModeStartScene = scene;
            }
        }

        [MenuItem("Kimchi Breaker/프로젝트 점검", priority = 0)]
        static void ValidateMenu()
        {
            if (ValidateProject(out List<string> problems, logSuccess: true))
            {
                EditorUtility.DisplayDialog("Kimchi Breaker", "문제 없음. 네트워크 프리팹, 씬, 빌드 설정이 모두 정상입니다.", "확인");
            }
            else
            {
                EditorUtility.DisplayDialog("Kimchi Breaker", "문제가 있습니다:\n\n- " + string.Join("\n- ", problems), "확인");
            }
        }

        [MenuItem("Kimchi Breaker/Main 씬 열기", priority = 1)]
        static void OpenMainScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(MainScenePath);
            }
        }

        [MenuItem("Kimchi Breaker/Windows 빌드 (개발용)", priority = 20)]
        static void BuildWindowsDevelopment() => BuildWindows(development: true);

        [MenuItem("Kimchi Breaker/Windows 빌드 (배포용)", priority = 21)]
        static void BuildWindowsRelease() => BuildWindows(development: false);

        /// <summary>네트워크 프리팹/씬/빌드 설정을 점검한다. 문제 목록이 비어 있으면 true.</summary>
        public static bool ValidateProject(out List<string> problems, bool logSuccess)
        {
            problems = new List<string>();
            var hashes = new Dictionary<uint, string>();
            foreach (string resourcePath in s_NetworkPrefabPaths)
            {
                var prefab = Resources.Load<GameObject>(resourcePath);
                if (prefab == null)
                {
                    problems.Add($"Resources/{resourcePath}.prefab 이 없습니다.");
                    continue;
                }

                var networkObjects = prefab.GetComponentsInChildren<NetworkObject>(true);
                if (networkObjects.Length != 1 || networkObjects[0].gameObject != prefab)
                {
                    problems.Add($"{resourcePath}: 최상위에 NetworkObject 가 정확히 하나 있어야 합니다.");
                    continue;
                }

                if (prefab.GetComponentsInChildren<NetworkBehaviour>(true).Any(b => b == null))
                {
                    problems.Add($"{resourcePath}: 스크립트가 빠진 컴포넌트가 있습니다.");
                }

                var serialized = new SerializedObject(networkObjects[0]);
                SerializedProperty hashProperty = serialized.FindProperty("GlobalObjectIdHash");
                uint hash = hashProperty != null ? (uint)hashProperty.longValue : 0u;
                if (hash == 0)
                {
                    problems.Add($"{resourcePath}: GlobalObjectIdHash 가 0 입니다. 프리팹을 한 번 열었다 저장하세요.");
                }
                else if (hashes.TryGetValue(hash, out string other))
                {
                    problems.Add($"{resourcePath}: {other} 와 GlobalObjectIdHash 가 같습니다.");
                }
                else
                {
                    hashes[hash] = resourcePath;
                }
            }

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0 || scenes[0].path != MainScenePath || !scenes[0].enabled)
            {
                problems.Add($"빌드 설정의 첫 번째 씬이 {MainScenePath} 이어야 합니다.");
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) == null)
            {
                problems.Add($"{MainScenePath} 이 없습니다.");
            }

            if (problems.Count == 0 && logSuccess)
            {
                Debug.Log("[KimchiBreaker] 프로젝트 점검 통과");
            }

            return problems.Count == 0;
        }

        static void BuildWindows(bool development)
        {
            if (!ValidateProject(out List<string> problems, logSuccess: false))
            {
                EditorUtility.DisplayDialog("빌드 중단", "프로젝트 점검 실패:\n\n- " + string.Join("\n- ", problems), "확인");
                return;
            }

            string folder = Path.Combine("Builds", development ? "Windows-Dev" : "Windows");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScenePath },
                locationPathName = Path.Combine(folder, "KimchiBreaker.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                EditorUtility.RevealInFinder(options.locationPathName);
            }
            else
            {
                EditorUtility.DisplayDialog("빌드 실패", $"결과: {report.summary.result}\n콘솔 로그를 확인하세요.", "확인");
            }
        }
    }
}
