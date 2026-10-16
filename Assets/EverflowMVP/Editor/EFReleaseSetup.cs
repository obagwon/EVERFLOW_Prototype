using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EverflowMVP.Editor
{
    public static class EFReleaseSetup
    {
        public const string TitleScene = "Assets/EverflowMVP/Scenes/Everflow_Title.unity";
        const string LogoPath = "Assets/logo/EVERFLOW_main.png";
        const string IconPath = "Assets/logo/EVERFLOW_icon.png";

        [MenuItem("Tools/EVERFLOW/Release/1. Set Up Title and Windows Settings")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EFTitleScreen.GameplayScene) == null)
                throw new InvalidOperationException("The configured gameplay scene is missing.");
            ImportTexture(LogoPath, true);
            ImportTexture(IconPath, false);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            foreach (var target in new[] { NamedBuildTarget.Unknown, NamedBuildTarget.Standalone })
            {
                int count = PlayerSettings.GetIconSizes(target, IconKind.Any).Length;
                if (count > 0) PlayerSettings.SetIcons(target, Enumerable.Repeat(icon, count).ToArray(), IconKind.Any);
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScene) == null) CreateTitle();

            var scenes = CreateReleaseScenes(EditorBuildSettings.globalScenes);
            EditorBuildSettings.globalScenes = scenes;
            var profile = BuildProfile.GetActiveBuildProfile();
            if (profile != null && profile.overrideGlobalScenes)
            {
                profile.scenes = scenes;
                EditorUtility.SetDirty(profile);
            }
            AssetDatabase.SaveAssets();
            ValidateScenes();
            Debug.Log("EVERFLOW setup complete. Existing title/gameplay scene contents preserved. Version: " + PlayerSettings.bundleVersion);
        }

        // Reapplying Release must preserve utility scenes without enabling them in the player.
        internal static EditorBuildSettingsScene[] CreateReleaseScenes(EditorBuildSettingsScene[] existing)
        {
            return new[] { new EditorBuildSettingsScene(TitleScene, true),
                new EditorBuildSettingsScene(EFTitleScreen.GameplayScene, true) }
                .Concat(existing.Where(s => s.path != TitleScene && s.path != EFTitleScreen.GameplayScene)
                    .Select(s => new EditorBuildSettingsScene(s.path, false))).ToArray();
        }

        internal static void ValidateBuildScenes(EditorBuildSettingsScene[] scenes)
        {
            var enabled = scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Require(enabled.Length >= 2 && enabled[0] == TitleScene && enabled[1] == EFTitleScreen.GameplayScene &&
                enabled.Count(p => p == TitleScene) == 1 && enabled.Count(p => p == EFTitleScreen.GameplayScene) == 1,
                "The title and exhibition map must be the first two enabled scenes, without duplicates. Run Tools > EVERFLOW > Release > 1. Set Up Title and Windows Settings.");
        }

        static void Require(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException("EVERFLOW validation: " + message);
        }

        internal static void ValidateMapReferences(EverflowMap.EFMapWorld[] worlds, EverflowMap.EFMapPlayer[] players)
        {
            Require(worlds.Length == 1, "gameplay scene must contain exactly one exhibition map world; the legacy MVP world is not a release substitute.");
            var world = worlds[0];
            Require(world.stages != null && world.stages.Length == 6, "six-stage exhibition map");
            Require(players.Length == 1 && world.player == players[0] && players[0].world == world &&
                players[0].view != null && players[0].follow != null, "map player/camera references");
            Require(world.stages.All(s => s != null && s.spawn != null && s.arrival != null &&
                s.nodes != null && s.nodes.Length > 0 && s.nodes.All(n => n != null && n.world == world) &&
                s.requirements != null && s.requirements.Length > 0 && s.requirements.All(n => n != null) &&
                s.restoration != null && s.restoration.All(a => a != null && a.clip != null)),
                "map puzzle/restoration references (empty restoration lists are allowed)");
            Require(world.gate4 != null && world.sideWater != null, "map water gate references");
            Require(world.wheel != null && world.endingCamera != null && world.endingCamera != world.player.view &&
                world.finalFlow != null && world.finalFlow.Length > 0 && world.finalFlow.All(f => f != null),
                "map ending camera/wheel/water-flow references");
        }

        static void ImportTexture(string path, bool sprite)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing image: " + path);
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (sprite) importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static Text Label(string name, Transform parent, string text, int size)
        {
            var label = Rect(name, parent, Vector2.zero, Vector2.one).gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text; label.fontSize = size; label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white; label.raycastTarget = false;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 12; label.resizeTextMaxSize = size;
            return label;
        }

        static void CreateTitle()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var camera = new GameObject("Title Camera", typeof(Camera)).GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.025f, .105f, .115f);
                camera.cullingMask = 0;
                var root = new GameObject("EVERFLOW Title", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(EFTitleScreen));
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                var background = Rect("Background", root.transform, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
                background.color = camera.backgroundColor; background.raycastTarget = false;
                var logo = Rect("EVERFLOW Logo", root.transform, new Vector2(.12f, .43f), new Vector2(.88f, .86f))
                    .gameObject.AddComponent<Image>();
                logo.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
                logo.preserveAspect = true; logo.raycastTarget = false;
                var buttonRect = Rect("Start Game", root.transform, new Vector2(.30f, .24f), new Vector2(.70f, .35f));
                var buttonImage = buttonRect.gameObject.AddComponent<Image>();
                buttonImage.color = new Color(.08f, .35f, .36f);
                var button = buttonRect.gameObject.AddComponent<Button>(); button.targetGraphic = buttonImage;
                var title = root.GetComponent<EFTitleScreen>();
                title.startButton = button; title.startLabel = Label("Label", buttonRect, "게임 시작", 32);
                var versionRect = Rect("Version", root.transform, new Vector2(.60f, .025f), new Vector2(.975f, .075f));
                title.versionLabel = Label("App Version", versionRect, "", 18);
                title.versionLabel.alignment = TextAnchor.MiddleRight;
                title.versionLabel.color = new Color(.65f, .78f, .78f);
                UnityEventTools.AddPersistentListener(button.onClick, title.StartGame);
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                events.GetComponent<EventSystem>().firstSelectedGameObject = button.gameObject;
                if (!EditorSceneManager.SaveScene(scene, TitleScene)) throw new InvalidOperationException("Cannot save title scene.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [MenuItem("Tools/EVERFLOW/Release/2. Open Title Scene")]
        public static void OpenTitle()
        {
            Configure();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(TitleScene);
        }

        [MenuItem("Tools/EVERFLOW/Release/Validate Title and Gameplay References")]
        public static void ValidateScenes()
        {
            foreach (string path in new[] { TitleScene, EFTitleScreen.GameplayScene })
            {
                Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null, "missing release scene: " + path);
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var roots = scene.GetRootGameObjects();
                    T[] Find<T>() where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
                    Require(Find<Camera>().Count(c => c.enabled && c.gameObject.activeInHierarchy) == 1,
                        path + " must contain one active camera.");
                    if (path == TitleScene)
                    {
                        var titles = Find<EFTitleScreen>();
                        Require(titles.Length == 1, "title controller count");
                        var title = titles[0];
                        Require(title.startButton != null && title.startLabel != null && title.versionLabel != null,
                            "title UI references missing; existing scene was preserved, repair its references.");
                        Require(title.startButton.onClick.GetPersistentEventCount() == 1 &&
                            title.startButton.onClick.GetPersistentTarget(0) == title &&
                            title.startButton.onClick.GetPersistentMethodName(0) == nameof(EFTitleScreen.StartGame), "start button event");
                        Require(Find<Image>().Any(i => i.sprite != null && AssetDatabase.GetAssetPath(i.sprite) == LogoPath && i.preserveAspect), "logo reference/aspect");
                        Require(Find<EventSystem>().Length == 1 && Find<InputSystemUIInputModule>().Length == 1, "UI input system count");
                        Require(Find<EFPlayer>().Length == 0 && Find<EFWorld>().Length == 0, "gameplay objects in title");
                    }
                    else
                    {
                        ValidateMapReferences(Find<EverflowMap.EFMapWorld>(), Find<EverflowMap.EFMapPlayer>());
                        Require(Find<EventSystem>().Length <= 1, "duplicate gameplay EventSystems");
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            Debug.Log("PASS: title button/logo/input and configured gameplay scene references. Playthrough still required.");
        }
    }
}
