using System;
using UnityEngine;
using UnityEngine.UI;
using Ability = EverflowGraybox.Ability;

namespace EverflowMap
{
    [Serializable]
    public sealed class EFAbilityView
    {
        public Ability ability;
        public Image icon;
        public Text shortcut, abilityName, description;
        public GameObject selectedMark;
    }

    [DisallowMultipleComponent]
    public sealed class EFMapAbilityHUD : MonoBehaviour
    {
        public EFMapPlayer player;
        public CanvasGroup visibility;
        public EFAbilityView[] abilities;
        [Range(1, 1.2f)] public float selectedScale = 1.1f;
        [Range(.4f, 1)] public float unselectedBrightness = .78f;
        Font osFont;

        void Awake()
        {
            // This project's release target is Windows. A serialized font can override
            // the installed Korean font without changing the HUD or player code.
            osFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR" }, 24);
            var defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (osFont != null)
                foreach (var view in abilities)
                    foreach (var text in new[] { view.shortcut, view.abilityName, view.description })
                        if (text.font == null || text.font == defaultFont) text.font = osFont;
            visibility.blocksRaycasts = visibility.interactable = false;
        }
        void OnEnable()
        {
            player.AbilityChanged += Refresh;
            Refresh(player.selected);
        }
        void OnDisable() { if (player != null) player.AbilityChanged -= Refresh; }
        void LateUpdate() { visibility.alpha = player.world.GameInputActive ? 1 : 0; }
        void Refresh(Ability selected)
        {
            foreach (var view in abilities)
            {
                bool active = view.ability == selected;
                view.icon.rectTransform.localScale = Vector3.one * (active ? selectedScale : 1);
                float brightness = active ? 1 : unselectedBrightness;
                view.icon.color = new Color(brightness, brightness, brightness, 1);
                view.selectedMark.SetActive(active);
                view.description.gameObject.SetActive(active);
            }
        }
        void OnDestroy() { if (osFont != null) Destroy(osFont); }
    }
}
