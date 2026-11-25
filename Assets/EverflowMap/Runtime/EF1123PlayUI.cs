using EverflowPuzzleLab;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EverflowMap
{
    [DisallowMultipleComponent, DefaultExecutionOrder(650)]
    public sealed class EF1123PlayUI : MonoBehaviour
    {
        public const string TitleScene = "Assets/Scenes/EVERFLOW_Title_lkh.unity";
        public EF1123World world;
        public EF1123Audio audio;
        public GameObject gameHUD, pauseMenu, ending, cinematicBars, controlsPanel;
        public Text starCount, objective, mission, action, aimCaption, endingStars, musicValue, effectsValue;
        public GameObject aimPanel, actionPanel;
        public Image aimAccent;
        public Text abilityHint;
        public Text[] abilityLabels;
        public RawImage[] abilityIcons;
        public Button resume, retry, pauseTitle, endingTitle, closeControls;
        public Slider music, effects;
        public Transform jumpHint;

        bool loadedTitle, previousPause, previousEnd, resumeAfterControls;
        static readonly Color AbilityInk = new(.94f, .97f, .95f);
        static readonly string[] WindDirectionNames = { "앞으로", "오른쪽으로", "뒤로", "왼쪽으로" };

        public bool HasControls => controlsPanel != null && closeControls != null;
        public bool ControlsOpen => HasControls && controlsPanel.activeSelf;

        void Start()
        {
            var font = LabSignFont.Get();
            if (font != null)
                foreach (var label in GetComponentsInChildren<Text>(true)) label.font = font;
            music.SetValueWithoutNotify(audio.MusicVolume);
            effects.SetValueWithoutNotify(audio.EffectsVolume);
            SetVolumeLabel(musicValue, audio.MusicVolume);
            SetVolumeLabel(effectsValue, audio.EffectsVolume);
            music.onValueChanged.AddListener(value =>
            {
                audio.SetMusicVolume(value);
                SetVolumeLabel(musicValue, value);
            });
            effects.onValueChanged.AddListener(value =>
            {
                audio.SetEffectsVolume(value);
                SetVolumeLabel(effectsValue, value);
            });
            resume.onClick.AddListener(() => { Confirm(); world.SetPaused(false); });
            retry.onClick.AddListener(() => { Confirm(); world.Retry(); });
            pauseTitle.onClick.AddListener(ReturnToTitle);
            endingTitle.onClick.AddListener(ReturnToTitle);
            if (HasControls) closeControls.onClick.AddListener(ToggleControls);
            foreach (var control in new Selectable[] {resume, retry, pauseTitle, endingTitle, music, effects})
            {
                var trigger = control.gameObject.AddComponent<EventTrigger>();
                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener(_ => audio.Play(EF1123Sound.MenuChoice, .38f));
                trigger.triggers.Add(entry);
            }
        }

        void Confirm() => audio.Play(EF1123Sound.Confirm, .72f);

        public void ToggleControls()
        {
            if (!HasControls || world.Finished) return;
            Confirm();
            if (ControlsOpen)
            {
                controlsPanel.SetActive(false);
                if (resumeAfterControls) world.SetPaused(false);
                return;
            }
            resumeAfterControls = !world.Paused;
            world.SetPaused(true);
            controlsPanel.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(closeControls.gameObject);
        }

        void ReturnToTitle()
        {
            if (loadedTitle) return;
            Confirm();
            if (!Application.CanStreamedLevelBeLoaded(TitleScene))
            {
                Debug.LogError("Register the LKH title scene before returning to title.");
                return;
            }
            loadedTitle = true;
            PlayerPrefs.Save();
            SceneManager.LoadSceneAsync(TitleScene, LoadSceneMode.Single);
        }

        static void SetVolumeLabel(Text label, float value) => label.text = Mathf.RoundToInt(value * 100) + "%";

        void LateUpdate()
        {
            bool paused = world.Paused, finished = world.Finished;
            bool showPause = paused && !finished && !ControlsOpen;
            gameHUD.SetActive(!paused && !finished);
            pauseMenu.SetActive(showPause);
            ending.SetActive(finished);
            cinematicBars.SetActive(world.Cinematic && !paused && !finished);
            if (showPause && !previousPause)
            {
                audio.Play(EF1123Sound.MenuChoice, .57f);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(resume.gameObject);
            }
            if (finished && !previousEnd)
            {
                endingStars.text = world.CollectibleName + "   " + world.Count + " / " + world.courses.Length;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(endingTitle.gameObject);
            }
            previousPause = showPause;
            previousEnd = finished;
            if (paused || finished) return;

            var interior = world.interior;
            int stage = interior != null ? world.courses[0].stage : 0;
            starCount.text = interior != null ? stage + " / " + world.courses[0].steps.Length : world.Count + " / " + world.courses.Length;
            if (mission != null)
                mission.text = interior != null ? interior.Mission(stage) : world.Count == world.courses.Length
                    ? "풍차 안의 순환 장치를 수리하세요."
                    : "조각 6개로 풍차를 열어 물길을 되살리세요.";
            for (int i = 0; i < abilityIcons.Length; i++)
            {
                bool selected = world.AbilityMode && (int)world.Selected == i;
                float size = selected ? 66 : 60;
                abilityLabels[i].color = selected ? EF1123Effects.Colors[i] : AbilityInk;
                if (abilityIcons[i].rectTransform.sizeDelta.x != size)
                    abilityIcons[i].rectTransform.sizeDelta = new Vector2(size, size);
            }
            aimPanel.SetActive(world.AbilityMode);
            actionPanel.SetActive(!world.AbilityMode);
            bool choosingWind = world.ChoosingWind;
            abilityHint.text = choosingWind ? "화살표 클릭 / WASD / 방향키  ·  Esc 취소" :
                world.AbilityMode ? AbilityDescription(world.Selected) : "1 / 2 / 3 · 능력 선택";
            if (world.AbilityMode)
            {
                aimAccent.color = EF1123Effects.Colors[(int)world.Selected];
                if (choosingWind)
                {
                    aimCaption.text = world.AimNotice ?? WindInstruction();
                    return;
                }
                var target = world.Hovered;
                aimCaption.text = world.AimNotice ?? (target == null ? (world.UsesModelAim ?
                    "빛나는 장치 모델을 클릭하세요   ·   Esc 취소" : "빛나는 능력 표식에 커서를 올리세요   ·   Esc 취소")
                    : target.title + "  ·  " + AimInstruction(target));
                return;
            }
            var (heading, instruction) = CurrentGuide();
            objective.text = heading;
            bool readyForInput = world.CanGrabFlower || world.CanLaunchJumpFlower;
            action.text = !readyForInput && Time.unscaledTime < world.MessageUntil && !string.IsNullOrEmpty(world.Message)
                ? world.Message : instruction;
        }

        static string AbilityDescription(LabAbility ability) => ability switch
        {
            LabAbility.Bloom => "식물을 성장시켜 길을 만듭니다",
            LabAbility.Freeze => "물을 얼리거나 움직이는 구조물을 고정합니다",
            _ => "FLOW · 같은 표식을 두 번 클릭"
        };

        string AimInstruction(EF1123Target target)
        {
            if (!world.CanCast(target, out string reason)) return reason;
            if (target.timedPlatform != null && !target.timedPlatform.IsAligned)
                return "잎이 빗나갔어요. Esc로 풀고 착지턱과 맞을 때 다시 조준하세요.";
            if (world.Selected != LabAbility.Flow) return "왼쪽 클릭";
            return world.PendingFlow == target ? "한 번 더 클릭해 바람 보내기" : "클릭해 바람의 대상 선택";
        }

        string WindInstruction()
        {
            int direction = world.HoveredWindDirection;
            if (direction >= 0)
            {
                var receiver = world.PreviewWindReceiver(direction);
                return WindDirectionNames[direction] + (receiver != null ?
                    "  ·  " + receiver.title + "에 닿아요" : "  ·  이 방향에 반응할 장치가 없어요");
            }
            return world.HasWindReceiver ? "금빛 화살표가 장치에 닿는 방향이에요." : world.WindSetupHint();
        }

        (string, string) CurrentGuide()
        {
            if (world.interior != null)
            {
                var course = world.courses[0];
                return (world.interior.Restored ? "물길 회복" : "풍차 내부", world.interior.Instruction(course.stage, course.steps[2].requiredMotion.AtOpenPose));
            }
            Vector3 position = world.player.transform.position;
            if (world.Count == world.courses.Length)
                return ("풍차 문 개방", "돌계단을 올라 풍차 안으로 들어가세요.");
            var tutorial = world.tutorials[0];
            if (world.TutorialStage == 0 && Vector3.Distance(position, tutorial.station.position) < 9)
                return ("멈춘 계곡", "WASD 이동 · 마우스 시점 · 1로 뿌리 성장");
            if (world.TutorialStage == 1 && position.y > 27 && position.z < 30)
                return ("중앙 갈림길로 내려가기", "뿌리 계단을 따라 내려가 코스 입구를 찾으세요.");
            if (jumpHint != null && !world.courses[0].collected && Vector3.Distance(position, jumpHint.position) < 4)
                return ("B 수조와 정원", "Space 점프 · 수위가 오르면 잎 발판을 밟아 건너세요.");
            if (world.courses[5].stage == 1 && Vector3.Distance(position, world.jumpPad.position) < 3)
                return ("서쪽 채광실", world.CanLaunchJumpFlower ?
                    "도약 준비 완료  ·  Space로 창턱까지 뛰세요." : "꽃 중앙으로 이동하면 Space로 도약할 수 있어요.");

            EF1123Course nearest = null;
            float best = 11;
            foreach (var course in world.courses)
            {
                if (course.collected) continue;
                var step = course.stage < course.steps.Length ? course.steps[course.stage] : course.steps[^1];
                float distance = Vector3.Distance(position, step.station.position + Vector3.up);
                if (distance < best) { best = distance; nearest = course; }
            }
            if (nearest == null)
                return ("계곡의 숨결 모으기", "표지판을 따라 원하는 코스 입구로 가세요.   ·   R 체크포인트");
            if (nearest.stage == nearest.steps.Length)
                return (nearest.title, nearest.steps[^1].solution);
            var active = nearest.steps[nearest.stage];
            if (active.grabFirst)
            {
                if (world.HoldingFlower) return (nearest.title, "꽃씨를 잡았어요  ·  3으로 섬 쪽에 바람을 보내세요.");
                return (nearest.title, world.CanGrabFlower ?
                    "잡기 준비 완료  ·  E로 꽃씨 손잡이를 잡으세요." : "발사턱에서 꽃씨 가까이로 이동하세요.");
            }
            if (active.timedPlatform != null && best <= active.stationRadius)
                return (nearest.title, active.timedPlatform.IsAligned ?
                    "잎이 착지턱과 맞았어요  ·  2로 얼리세요." : "잎이 착지턱과 나란해질 때까지 기다리세요.");
            if (active.requiredMotion != null && !active.requiredMotion.AtOpenPose)
                return (nearest.title, "씨앗이 흙받이에 안착하면 BLOOM을 사용할 수 있어요.");
            return (nearest.title, $"{(int)active.ability + 1}  {active.ability.ToString().ToUpperInvariant()}   ·   {active.title}");
        }
    }
}
