using System.Collections;
using KimchiBreaker.Net;
using KimchiBreaker.Presentation;
using UnityEngine;

namespace KimchiBreaker.App
{
    /// <summary>
    /// 게임 시작점. Main 씬에 하나만 둔다.
    /// 로딩 화면을 띄운 상태에서 경기장, 카메라, 네트워크 모듈, 화면을 차례로 준비한 뒤 메인 메뉴로 넘어간다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        static bool s_Booted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Booted = false;
        }

        void Awake()
        {
            if (s_Booted)
            {
                // 씬을 다시 불러와도 시스템이 두 벌 생기지 않게 한다.
                Destroy(gameObject);
                return;
            }

            s_Booted = true;
            Application.runInBackground = true; // 창이 비활성이어도 네트워크가 멈추지 않게
            QualitySettings.vSyncCount = 1;
        }

        IEnumerator Start()
        {
            var uiObject = new GameObject("GameUI");
            DontDestroyOnLoad(uiObject);
            var ui = uiObject.AddComponent<GameUI>();
            ui.Loading = true;
            ui.LoadingMessage = "경기장 준비 중...";
            ui.LoadingProgress = 0.1f;
            yield return null;

            GameObject arena = ArenaBuilder.Build();
            DontDestroyOnLoad(arena);
            CameraDirector cameraDirector = CameraDirector.Create();
            DontDestroyOnLoad(cameraDirector.gameObject);
            ui.LoadingMessage = "네트워크 모듈 준비 중...";
            ui.LoadingProgress = 0.5f;
            yield return null;

            var sessionObject = new GameObject("NetworkSession");
            var session = sessionObject.AddComponent<NetworkSession>();
            ui.LoadingProgress = 0.9f;
            yield return null;

            ui.Initialize(session, cameraDirector);
            ui.LoadingMessage = "완료";
            ui.LoadingProgress = 1f;
            yield return new WaitForSecondsRealtime(0.3f);
            ui.Loading = false;
        }
    }
}
