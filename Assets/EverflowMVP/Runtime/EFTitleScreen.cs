using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EverflowMVP
{
    public sealed class EFTitleScreen : MonoBehaviour
    {
        public const string GameplayScene = "Assets/EverflowMVP/Scenes/Everflow_6Puzzles.unity";
        public Button startButton;
        public Text startLabel;
        public Text versionLabel;
        bool loading;
        Font koreanFont;

        void Awake()
        {
            // Gameplay already uses a visible, unlocked pointer for aiming and RMB orbit.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            versionLabel.text = "v" + Application.version;
            koreanFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 32);
            if (koreanFont != null) startLabel.font = koreanFont;
        }

        public void StartGame()
        {
            if (loading) return;
            if (!Application.CanStreamedLevelBeLoaded(GameplayScene))
            {
                Debug.LogError("Gameplay scene is missing from the build: " + GameplayScene);
                startLabel.text = "씬 설정 확인 필요";
                return;
            }
            loading = true;
            startButton.interactable = false;
            try
            {
                // Single unloads this scene's Canvas, EventSystem and camera together.
                if (SceneManager.LoadSceneAsync(GameplayScene, LoadSceneMode.Single) == null)
                    throw new InvalidOperationException("Scene loading did not start.");
            }
            catch (Exception exception)
            {
                loading = false;
                startButton.interactable = true;
                Debug.LogException(exception);
            }
        }

        void OnDestroy()
        {
            if (koreanFont != null) Destroy(koreanFont);
        }
    }
}
