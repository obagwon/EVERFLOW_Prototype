using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EverflowMVP
{
    public sealed class EFTitleScreen : MonoBehaviour
    {
        public const string GameplayScene = "Assets/Scenes/Everflow_Map.unity";
        public Button startButton;
        public Text startLabel; // Retained inactive for existing scene/editor references; artwork includes text.
        public Text versionLabel;
        bool loading;
        void Awake()
        {
            // EFMapWorld takes over cursor ownership after the title is unloaded.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (versionLabel != null) versionLabel.text = "v" + Application.version;
        }
        public void StartGame()
        {
            if (loading) return;
            if (!Application.CanStreamedLevelBeLoaded(GameplayScene))
            {
                Debug.LogError("Gameplay scene is missing from the build: " + GameplayScene);
                return;
            }
            loading = true;
            startButton.interactable = false;
            try
            {
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
    }
}
