using UnityEngine;

namespace KimchiBreaker.Presentation
{
    /// <summary>이 PC 의 조작 상태(메뉴 열림, 마우스 잠금 등). 네트워크와 무관하다.</summary>
    public static class LocalControl
    {
        /// <summary>일시정지/설정 메뉴가 열려 있는지. 열려 있으면 게임 조작을 받지 않는다.</summary>
        public static bool MenuOpen { get; set; }

        /// <summary>마우스 감도(픽셀당 도).</summary>
        public static float MouseSensitivity { get; set; } = 0.12f;

        /// <summary>게임 조작(시점 회전, 클릭)을 받아도 되는 상태인지.</summary>
        public static bool GameplayInputActive => !MenuOpen && Cursor.lockState == CursorLockMode.Locked && Application.isFocused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            MenuOpen = false;
        }
    }
}
