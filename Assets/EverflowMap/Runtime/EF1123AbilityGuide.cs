using UnityEngine;
using UnityEngine.UI;

namespace EverflowMap
{
    internal sealed class EF1123AbilityGuide
    {
        static readonly string[] Titles = { "BLOOM · 성장", "FREEZE · 동결", "FLOW · 바람" };
        static readonly string[] Descriptions =
        {
            "마른 식물을 자라게 해 길을 만듭니다.",
            "물을 얼리거나 움직이는 구조물을 고정합니다.",
            "바람으로 장치를 작동시키거나 물체를 움직입니다."
        };
        static readonly Color Ink = new(.93f, .96f, .90f);
        readonly EF1123PlayUI ui;
        RectTransform overlay;
        RawImage icon;
        Text title, description, instruction;
        readonly Image[] tabs = new Image[Titles.Length];
        Button previous;
        Text nextLabel;
        int page;
        bool hasOpened;

        public EF1123AbilityGuide(EF1123PlayUI ui) => this.ui = ui;

        public bool IsOpen => overlay != null && overlay.gameObject.activeSelf;

        public void Tick()
        {
            if (IsOpen)
            {
                if (EFMapInput.KeyDown(KeyCode.Escape)) Close();
                else if (EFMapInput.KeyDown(KeyCode.LeftArrow)) ShowPage(page - 1);
                else if (EFMapInput.KeyDown(KeyCode.RightArrow)) ShowPage(page + 1);
                else if (EFMapInput.KeyDown(KeyCode.Tab)) ShowPage((page + 1) % Titles.Length);
                else if (EFMapInput.KeyDown(KeyCode.Return)) Next();
                return;
            }
            if (hasOpened || !ui.world.CanOpenStartingAbilityGuide) return;
            hasOpened = true;
            Build();
            ShowPage(0);
            overlay.SetAsLastSibling();
            ui.world.SetAbilityGuideOpen(true);
        }

        void ShowPage(int index)
        {
            page = Mathf.Clamp(index, 0, Titles.Length - 1);
            icon.texture = ui.abilityIcons[page].texture;
            icon.uvRect = ui.abilityIcons[page].uvRect;
            title.text = Titles[page];
            title.color = EF1123Effects.Colors[page];
            description.text = Descriptions[page];
            instruction.text = page switch
            {
                0 => "1 → 마른 덩굴 클릭",
                1 => "2 → 빛나는 대상 클릭",
                _ when ui.world.UsesWindCompass => "3 → 대상 쪽 바람 방향 선택",
                _ when ui.world.directModelCasting => "3 → 작동시킬 대상 클릭",
                _ => "3 → 대상 표식을 두 번 클릭"
            };
            for (int i = 0; i < tabs.Length; i++)
                tabs[i].color = i == page ? new Color(.25f, .47f, .38f) : new Color(.11f, .24f, .20f);
            previous.interactable = page > 0;
            nextLabel.text = page == Titles.Length - 1 ? "플레이 시작" : "다음 →";
        }

        void Next()
        {
            if (page == Titles.Length - 1) Close();
            else ShowPage(page + 1);
        }

        void Close()
        {
            if (!IsOpen) return;
            overlay.gameObject.SetActive(false);
            ui.world.SetAbilityGuideOpen(false);
        }

        void Build()
        {
            overlay = Rect(ui.gameHUD.transform.parent, "Ability guide", Vector2.zero, Vector2.zero);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            overlay.gameObject.AddComponent<Image>().color = new Color(.045f, .095f, .083f, .45f);

            var card = Rect(overlay, "Card", Vector2.zero, new Vector2(550, 390));
            card.gameObject.AddComponent<Image>().color = new Color(.065f, .165f, .14f, .98f);
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                var tab = MakeButton(card, Titles[i], new Vector2((i - 1) * 151, 151),
                    new Vector2(140, 40), () => ShowPage(index));
                tabs[i] = (Image)tab.targetGraphic;
                Label(tab.transform, "Tab label", 16, 0, true).text = Titles[i];
            }
            icon = Rect(card, "Ability icon", new Vector2(0, 75), new Vector2(72, 72))
                .gameObject.AddComponent<RawImage>();
            icon.raycastTarget = false;
            title = Label(card, "Title", 30, 12, true);
            description = Label(card, "Description", 21, -32);
            instruction = Label(card, "Input", 22, -72, true);

            previous = MakeButton(card, "Previous", new Vector2(-150, -139), new Vector2(132, 45),
                () => ShowPage(page - 1));
            Label(previous.transform, "Previous label", 18, 0).text = "← 이전";
            var next = MakeButton(card, "Continue", new Vector2(150, -139), new Vector2(132, 45), Next);
            nextLabel = Label(next.transform, "Continue label", 18, 0);
            Label(card, "Keyboard shortcuts", 13, -175).text = "Tab · 탭 이동     ← → · 이전 / 다음     Esc · 건너뛰기";
        }

        static Button MakeButton(Transform parent, string name, Vector2 position, Vector2 size,
            UnityEngine.Events.UnityAction action)
        {
            var node = Rect(parent, name, position, size).gameObject;
            var background = node.AddComponent<Image>();
            background.color = new Color(.24f, .43f, .35f);
            var button = node.AddComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);
            return button;
        }

        Text Label(Transform parent, string name, int size, float y, bool bold = false)
        {
            var rect = Rect(parent, name, new Vector2(0, y), new Vector2(510, 41));
            var label = rect.gameObject.AddComponent<Text>();
            label.font = ui.starCount.font;
            label.fontSize = size;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Ink;
            label.raycastTarget = false;
            return label;
        }

        static RectTransform Rect(Transform parent, string name, Vector2 offset, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
