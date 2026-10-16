using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using EverflowMVP;
using Ability = EverflowGraybox.Ability;

namespace EverflowMap.Editor
{
    public static class EFPresentationSetup
    {
        public const string MainPath = "Assets/logo/EVERFLOW_main.png";
        public const string ButtonPath = "Assets/logo/EVERFLOW_start_button.png";
        public const string Evidence = "docs/ui-progress";
        static Sprite Import(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing required image: " + path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048;
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size, Vector2 anchor)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        static Text Label(string name, Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var text = Rect(name, parent, position, size, new Vector2(.5f, 0)).gameObject.AddComponent<Text>();
            text.text = value; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize; text.color = Color.white; text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = text.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .85f);
            shadow.effectDistance = new Vector2(1, -1); return text;
        }
        // Measure alpha without modifying source images or enabling readable textures in builds.
        static Rect VisibleRect(string path)
        {
            var texture = new Texture2D(2, 2); texture.LoadImage(File.ReadAllBytes(path));
            var pixels = texture.GetPixels32(); int x0 = texture.width, y0 = texture.height, x1 = 0, y1 = 0;
            for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
                if (pixels[y * texture.width + x].a >= 80) { x0 = Mathf.Min(x0, x); y0 = Mathf.Min(y0, y); x1 = Mathf.Max(x1, x); y1 = Mathf.Max(y1, y); }
            var rect = new Rect((float)x0 / texture.width, (float)y0 / texture.height,
                (float)(x1 - x0 + 1) / texture.width, (float)(y1 - y0 + 1) / texture.height);
            UnityEngine.Object.DestroyImmediate(texture); return rect;
        }
        public static bool UpgradeMap(EFMapWorld world)
        {
            bool changed = false;
            for (int i = 0; i < 5; i++)
            {
                var marker = world.stages[i].arrival;
                if (marker.GetComponent<EFMapPassage>() != null) continue;
                var passage = marker.gameObject.AddComponent<EFMapPassage>(); passage.world = world; passage.stage = i; changed = true;
                if (i == 4)
                {
                    // Extend only beyond the door: preserve the existing near-side boundary.
                    marker.position += Vector3.right + Vector3.up * .5f;
                    world.stages[i].arrivalSize = new Vector3(4, 6, 6);
                }
            }
            if (world.GetComponentInChildren<EFMapAbilityHUD>(true) != null) return changed;
            var root = Rect("Ability HUD", world.transform, Vector2.zero, Vector2.zero, new Vector2(.5f, 0));
            var canvas = root.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = root.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var group = root.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = group.interactable = false;
            var hud = root.gameObject.AddComponent<EFMapAbilityHUD>(); hud.player = world.player; hud.visibility = group;
            var panel = Rect("Bottom Center (margin 24)", root, new Vector2(0, 24), new Vector2(600, 176), new Vector2(.5f, 0)); panel.pivot = new Vector2(.5f, 0);
            string[] names = { "BLOOM", "FREEZE", "PULSE" };
            string[] descriptions = { "식물을 성장시킵니다", "물을 얼리고 움직임을 멈춥니다", "에너지를 전달해 장치를 작동시킵니다" };
            hud.abilities = new EFAbilityView[3];
            var backing = Rect("Description backing", panel, new Vector2(0, 18), new Vector2(560, 36), new Vector2(.5f, 0)).gameObject.AddComponent<Image>();
            backing.color = new Color(.02f, .04f, .05f, .6f); backing.raycastTarget = false;
            for (int i = 0; i < 3; i++)
            {
                string path = "Assets/icon/" + names[i].ToLowerInvariant() + "_icon.png";
                var sprite = Import(path); var visible = VisibleRect(path);
                var slot = Rect(names[i], panel, new Vector2((i - 1) * 150, 0), new Vector2(140, 176), new Vector2(.5f, 0)); slot.pivot = new Vector2(.5f, 0);
                float imageSize = 76 / Mathf.Max(visible.width, visible.height);
                var iconRect = Rect("Icon", slot, new Vector2((.5f - visible.center.x) * imageSize, 120 + (.5f - visible.center.y) * imageSize), new Vector2(imageSize, imageSize), new Vector2(.5f, 0));
                var icon = iconRect.gameObject.AddComponent<Image>(); icon.sprite = sprite; icon.preserveAspect = true; icon.raycastTarget = false;
                var shortcut = Label("Shortcut", slot, (i + 1).ToString(), new Vector2(-43, 62), new Vector2(22, 28), 21);
                var abilityName = Label("Ability name", slot, names[i], new Vector2(15, 62), new Vector2(92, 28), 21);
                var mark = Rect("Selected underline", slot, new Vector2(0, 43), new Vector2(92, 2), new Vector2(.5f, 0)).gameObject.AddComponent<Image>();
                mark.color = Color.white; mark.raycastTarget = false;
                var description = Label(names[i] + " description", panel, descriptions[i], new Vector2(0, 18), new Vector2(540, 32), 22);
                hud.abilities[i] = new EFAbilityView { ability = (Ability)i, icon = icon, shortcut = shortcut, abilityName = abilityName, description = description, selectedMark = mark.gameObject };
            }
            foreach (var entry in hud.abilities)
            {
                bool selected = entry.ability == world.player.selected;
                entry.selectedMark.SetActive(selected); entry.description.gameObject.SetActive(selected);
                entry.icon.rectTransform.localScale = Vector3.one * (selected ? hud.selectedScale : 1);
                entry.icon.color = selected ? Color.white : new Color(.78f, .78f, .78f, 1);
            }
            return true;
        }
        [MenuItem("Tools/EVERFLOW/Presentation/Apply Title, HUD and Passage Markers")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var main = Import(MainPath); var buttonSprite = Import(ButtonPath);
            var previousPath = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(EverflowMVP.Editor.EFReleaseSetup.TitleScene);
            var title = UnityEngine.Object.FindFirstObjectByType<EFTitleScreen>();
            var scaler = title.GetComponent<CanvasScaler>(); scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var composition = title.transform.Find("Main Composition") as RectTransform;
            if (composition == null)
            {
                composition = Rect("Main Composition", title.transform, Vector2.zero, Vector2.zero, new Vector2(.5f, .5f));
                var fit = composition.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = main.rect.width / main.rect.height;
            }
            composition.SetSiblingIndex(1);
            var logo = title.transform.GetComponentsInChildren<Image>(true).Single(i => i.name == "EVERFLOW Logo");
            logo.transform.SetParent(composition, false);
            var logoRect = logo.rectTransform; logoRect.anchorMin = Vector2.zero; logoRect.anchorMax = Vector2.one;
            logoRect.offsetMin = logoRect.offsetMax = Vector2.zero; logo.sprite = main; logo.preserveAspect = true; logo.raycastTarget = false;
            var button = title.startButton; var buttonRect = (RectTransform)button.transform;
            buttonRect.SetParent(composition, false); buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(.244f, .44f);
            buttonRect.anchoredPosition = Vector2.zero; buttonRect.sizeDelta = new Vector2(450, 96);
            var target = button.GetComponent<Image>(); target.sprite = null; target.color = Color.clear; target.raycastTarget = true;
            var artwork = button.transform.Find("Button artwork") as RectTransform;
            if (artwork == null) artwork = Rect("Button artwork", button.transform, Vector2.zero, new Vector2(460, 460 * buttonSprite.rect.height / buttonSprite.rect.width), new Vector2(.5f, .5f));
            var image = artwork.GetComponent<Image>(); if (image == null) image = artwork.gameObject.AddComponent<Image>();
            image.sprite = buttonSprite; image.preserveAspect = true; image.raycastTarget = false; button.targetGraphic = image;
            var colors = button.colors; colors.normalColor = new Color(.94f, .94f, .94f); colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white; colors.pressedColor = new Color(.8f, .8f, .8f); colors.fadeDuration = .12f; button.colors = colors;
            if (title.startLabel != null) title.startLabel.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            scene = EditorSceneManager.OpenScene(EFMapAudit.ScenePath);
            var world = UnityEngine.Object.FindFirstObjectByType<EFMapWorld>();
            if (UpgradeMap(world)) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            AssetDatabase.SaveAssets(); EverflowMVP.Editor.EFReleaseSetup.ValidateScenes();
            Directory.CreateDirectory(Evidence);
            var origin = GameObject.Find("EF_Terrain").transform.position;
            File.WriteAllText(Evidence + "/layout.txt", string.Join("\n", world.stages.Take(5).Select((s, i) => $"P{i+1}: center world={s.arrival.position:F4}, size={s.arrivalSize}, terrain-relative={s.arrival.position - origin:F4}")));
            if (!string.IsNullOrEmpty(previousPath)) EditorSceneManager.OpenScene(previousPath);
            Debug.Log("PASS: presentation saved with referenced images and five existing exit regions; final puzzle untouched.");
        }
    }
}
