using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace KimchiBreaker.Net
{
    /// <summary>
    /// 네트워크로 생성되는 프리팹 목록. Resources 폴더에서 불러와 NetworkManager 에 등록한다.
    /// 호스트와 클라이언트가 같은 목록을 같은 순서로 등록해야 한다(빌드가 같으면 자동으로 같다).
    /// </summary>
    public static class NetworkPrefabCatalog
    {
        public const string MatchStatePath = "KimchiBreaker/Net/MatchState";
        public const string PlayerAvatarPath = "KimchiBreaker/Net/PlayerAvatar";
        public const string PracticeIngredientPath = "KimchiBreaker/Net/PracticeIngredient";

        static readonly string[] s_Paths = { MatchStatePath, PlayerAvatarPath, PracticeIngredientPath };

        static readonly Dictionary<string, GameObject> s_Loaded = new Dictionary<string, GameObject>();

        public static GameObject MatchState => Get(MatchStatePath);

        public static GameObject PlayerAvatar => Get(PlayerAvatarPath);

        public static GameObject PracticeIngredient => Get(PracticeIngredientPath);

        /// <summary>모든 프리팹을 불러와 확인한다. 하나라도 없거나 잘못되면 false 와 함께 이유를 돌려준다.</summary>
        public static bool TryLoadAll(out string error)
        {
            foreach (string path in s_Paths)
            {
                GameObject prefab = Get(path);
                if (prefab == null)
                {
                    error = $"네트워크 프리팹을 찾을 수 없습니다: Resources/{path}.prefab";
                    return false;
                }

                if (!prefab.TryGetComponent(out NetworkObject _))
                {
                    error = $"네트워크 프리팹에 NetworkObject 가 없습니다: {path}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>NetworkManager 에 모든 프리팹을 등록한다. 이미 등록된 것은 건너뛴다.</summary>
        public static void RegisterAll(NetworkManager networkManager)
        {
            foreach (string path in s_Paths)
            {
                GameObject prefab = Get(path);
                if (prefab != null && !networkManager.NetworkConfig.Prefabs.Contains(prefab))
                {
                    networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
                }
            }
        }

        static GameObject Get(string path)
        {
            if (!s_Loaded.TryGetValue(path, out GameObject prefab) || prefab == null)
            {
                prefab = Resources.Load<GameObject>(path);
                s_Loaded[path] = prefab;
            }

            return prefab;
        }
    }
}
