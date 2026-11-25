using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EverflowMVP
{
    public sealed class EFTitleScreen : MonoBehaviour
    {
        public const string GameplayScene = "Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity";
        public Button startButton;
        public Text startLabel; // Retained inactive for existing scene/editor references; artwork includes text.
        public Text versionLabel;
        [SerializeField] string gameplaySceneOverride;
        [SerializeField] EverflowMap.EF1123Audio menuAudio;
        bool loading;
        void Awake()
        {
            // The gameplay world takes over cursor ownership when the title unloads.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (versionLabel != null) versionLabel.text = "v" + Application.version;
        }
        public void StartGame()
        {
            if (loading) return;
            string destination = string.IsNullOrEmpty(gameplaySceneOverride) ? GameplayScene : gameplaySceneOverride;
            if (!Application.CanStreamedLevelBeLoaded(destination))
            {
                Debug.LogError("Gameplay scene is missing from the build: " + destination);
                return;
            }
            loading = true;
            startButton.interactable = false;
            menuAudio?.Play(EverflowMap.EF1123Sound.Confirm, .85f);
            try
            {
                if (SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single) == null)
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
