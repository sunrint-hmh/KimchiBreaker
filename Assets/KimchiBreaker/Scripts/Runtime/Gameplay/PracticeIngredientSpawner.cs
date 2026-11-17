using System;
using System.Collections.Generic;
using KimchiBreaker.Core;
using KimchiBreaker.Net;
using Unity.Netcode;
using UnityEngine;
using Random = System.Random;

namespace KimchiBreaker.Gameplay
{
    /// <summary>
    /// [임시, 서버 전용] 라운드 중에 연습 재료를 포대 위로 떨어뜨린다.
    /// 남현 님의 "가상 AI 재료 스포너"가 준비되면 대체한다.
    /// </summary>
    public sealed class PracticeIngredientSpawner
    {
        const int k_MaxAlive = 12;
        const double k_MinInterval = 1.0;
        const double k_MaxInterval = 2.0;

        readonly NetworkManager m_NetworkManager;
        readonly Random m_Random = new Random();
        readonly List<NetworkObject> m_Alive = new List<NetworkObject>();
        double m_NextSpawnTime = double.NaN;

        public PracticeIngredientSpawner(NetworkManager networkManager)
        {
            m_NetworkManager = networkManager ? networkManager : throw new ArgumentNullException(nameof(networkManager));
        }

        public void ServerUpdate(MatchPhase phase, double now)
        {
            if (phase != MatchPhase.RoundPlaying || !m_NetworkManager.IsServer)
            {
                m_NextSpawnTime = double.NaN;
                return;
            }

            m_Alive.RemoveAll(o => o == null || !o.IsSpawned);
            if (double.IsNaN(m_NextSpawnTime))
            {
                m_NextSpawnTime = now + 0.5;
            }

            if (now < m_NextSpawnTime || m_Alive.Count >= k_MaxAlive)
            {
                return;
            }

            m_NextSpawnTime = now + k_MinInterval + m_Random.NextDouble() * (k_MaxInterval - k_MinInterval);
            Spawn();
        }

        void Spawn()
        {
            GameObject prefab = NetworkPrefabCatalog.PracticeIngredient;
            if (prefab == null)
            {
                return;
            }

            Vector3 position = GameArena.RandomPointOnTarp(m_Random, 3.5f);
            GameObject instance = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
            if (instance.TryGetComponent(out PracticeIngredient ingredient))
            {
                ingredient.ServerConfigure(m_Random.Next(10) == 0 ? 3 : 1); // 10% 확률로 3개짜리
            }

            var networkObject = instance.GetComponent<NetworkObject>();
            try
            {
                networkObject.Spawn(true);
                m_Alive.Add(networkObject);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                UnityEngine.Object.Destroy(instance);
            }
        }

        /// <summary>남아 있는 재료를 모두 없앤다(라운드 종료, 대기실 복귀 시).</summary>
        public void DespawnAll()
        {
            for (int i = 0; i < m_Alive.Count; i++)
            {
                NetworkObject networkObject = m_Alive[i];
                if (networkObject != null && networkObject.IsSpawned)
                {
                    networkObject.Despawn(true);
                }
            }

            m_Alive.Clear();
            m_NextSpawnTime = double.NaN;
        }
    }
}
