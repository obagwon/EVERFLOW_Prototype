using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EverflowMap
{
    [DisallowMultipleComponent]
    public sealed class EFWindmillInterior : MonoBehaviour
    {
        public EF1123World world;
        public EF1123Target ventTarget, seedTarget;
        public Transform vent, seed, arrivalView, canopyView;
        public GameObject ventFrost;
        public Light ventLight;
        public EF1123Motion[] leaves, flowering;
        public GameObject[] leafSurfaces;
        public Transform[] seedRoute;
        public ParticleSystem crownWind;
        [Min(1)] public float freezeSeconds = 5f;
        const float TravelSeconds = 2.35f, ShowcaseSeconds = 3.9f, SwingPeriod = 5.2f;
        const float OpenAngle = 90f, OpenThreshold = 45f;
        Quaternion shut;
        float swingClock, frozen, harvestTime;
        bool transporting, finished;
        EF1123World exterior;
        GameObject[] exteriorRoots;

        public bool Restored { get; private set; }
        public bool VentHeld => frozen > 0;
        public bool VentOpen => OpenAngle * SwingAmount >= OpenThreshold;
        float SwingAmount => .5f - .5f * Mathf.Cos(swingClock * (2f * Mathf.PI / SwingPeriod));

        internal static bool Enter(EF1123World outside, string path)
        {
            if (!Application.CanStreamedLevelBeLoaded(path))
            {
                Debug.LogError($"Windmill garden scene is not in the active build profile: {path}", outside);
                return false;
            }
            Scene exteriorScene = outside.gameObject.scene;
            SceneManager.sceneLoaded += Connect;
            var load = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            if (load != null) return true;
            SceneManager.sceneLoaded -= Connect;
            return false;

            void Connect(Scene indoorScene, LoadSceneMode mode)
            {
                if (indoorScene.path != path) return;
                SceneManager.sceneLoaded -= Connect;
                EFWindmillInterior garden = null;
                foreach (var root in indoorScene.GetRootGameObjects())
                {
                    garden = root.GetComponentInChildren<EFWindmillInterior>();
                    if (garden != null) break;
                }
                if (garden == null)
                {
                    Debug.LogError("Windmill garden scene is missing its puzzle controller.", outside);
                    SceneManager.UnloadSceneAsync(indoorScene);
                    outside.CancelWindmillEntry();
                    return;
                }
                garden.exterior = outside;
                garden.exteriorRoots = Array.FindAll(exteriorScene.GetRootGameObjects(), root => root.activeSelf);
                foreach (var root in garden.exteriorRoots) root.SetActive(false);
                SceneManager.SetActiveScene(indoorScene);
            }
        }

        void Awake()
        {
            shut = vent.localRotation;
            ventFrost.SetActive(false);
            foreach (var leaf in leafSurfaces) leaf.SetActive(false);
        }

        internal bool IsVentAvailable(int stage) => stage >= 1 && stage < 3;

        internal void ShowArrival()
        {
            if (arrivalView != null) world.effects?.ShowCutaway(arrivalView, 2.35f);
        }

        internal string CastBlock(EF1123Target target)
        {
            if (target == ventTarget)
                return VentHeld ? "환기판이 고정되어 있어요. 씨앗 쪽으로 바람을 보내세요." :
                    !VentOpen ? "Esc로 나온 뒤 환기판이 반쯤 열리면 2를 누르세요." : null;
            return target == seedTarget && !VentHeld ? "환기판이 열린 순간을 FREEZE로 고정하세요." : null;
        }

        internal bool Apply(EF1123Target target)
        {
            var course = world.courses[0];
            if (target == ventTarget)
            {
                if (VentHeld) return true;
                swingClock = SwingPeriod * .5f;
                vent.localRotation = shut * Quaternion.Euler(0f, OpenAngle, 0f);
                frozen = freezeSeconds;
                ventFrost.SetActive(true);
                course.stage = 2;
                world.Report(target.solution);
                world.effects?.Impact(target);
                return true;
            }
            if (target != seedTarget) return false;
            if (!VentHeld || transporting || Restored) return true;
            course.stage = 3;
            transporting = true;
            harvestTime = 0f;
            world.Report(target.solution);
            world.effects?.Impact(target);
            world.effects?.ShowCutaway(canopyView, TravelSeconds + ShowcaseSeconds);
            return true;
        }

        internal void Tick(float dt)
        {
            if (finished) return;

            if (world.courses[0].stage > 0 && !leafSurfaces[0].activeSelf &&
                Array.TrueForAll(leaves, leaf => leaf.AtOpenPose))
                foreach (var leaf in leafSurfaces) leaf.SetActive(true);

            if (transporting)
            {
                harvestTime += dt;
                if (!Restored)
                {
                    MoveSeed(Mathf.Clamp01(harvestTime / TravelSeconds), dt);
                    if (harvestTime >= TravelSeconds)
                    {
                        Restored = true;
                        seed.gameObject.SetActive(false);
                        ventFrost.SetActive(false);
                        foreach (var motion in flowering) motion.Open();
                        crownWind.Play();
                        world.Report("수관이 피어났습니다. 바람이 풍차 밖으로 이어집니다.");
                    }
                }
                if (harvestTime >= TravelSeconds + ShowcaseSeconds) Complete();
                return;
            }

            if (frozen > 0)
            {
                frozen -= dt;
                if (frozen <= 0)
                {
                    ventFrost.SetActive(false);
                    world.courses[0].stage = 1;
                    world.Report("고정이 풀렸습니다. 이 발코니에서 다시 얼릴 수 있어요.");
                }
            }
            else swingClock = (swingClock + dt) % SwingPeriod;

            vent.localRotation = shut * Quaternion.Euler(0f, OpenAngle * SwingAmount, 0f);
            ventLight.color = VentHeld ? new Color(.29f,.77f,1f) :
                VentOpen ? new Color(1f,.72f,.26f) : new Color(.66f,.44f,.24f);
            ventLight.intensity = VentHeld ? 2.2f : VentOpen ? 1.45f : .30f;
        }

        void MoveSeed(float progress, float delta)
        {
            float along = progress * (seedRoute.Length - 1);
            int segment = Mathf.Min((int)along, seedRoute.Length - 2);
            Vector3 a = seedRoute[segment].position, b = seedRoute[segment + 1].position;
            seed.position = Vector3.Lerp(a, b, along - segment);
            seed.Rotate(0f, 82f * delta, 29f * delta, Space.Self);
        }

        void Complete()
        {
            finished = true;
            if (exterior == null)
            {
                world.EndWindmillGarden();
                return;
            }
            Scene interiorScene = gameObject.scene;
            SceneManager.SetActiveScene(exterior.gameObject.scene);
            foreach (var root in interiorScene.GetRootGameObjects()) root.SetActive(false);
            foreach (var root in exteriorRoots)
                if (root != null) root.SetActive(true);
            exterior.PlayWindmillEpilogue();
            SceneManager.UnloadSceneAsync(interiorScene);
        }

        internal string Mission(int stage) => stage switch
        {
            0 => "마른 잎을 펼쳐 수직 정원 위로 올라가세요.",
            1 => "중층 환기판을 열린 각도에 고정하세요.",
            2 => "씨앗을 공기 통로로 보내세요.",
            _ => Restored ? "풍차가 다시 숨을 쉽니다." : "씨앗이 지붕으로 올라가는 중입니다."
        };

        internal string Instruction(int stage) => stage switch
        {
            0 => "1 BLOOM · 뿌리 옆에 접힌 잎을 클릭하세요.",
            1 => "환기판이 반쯤 열리면 2 · 경첩을 클릭하세요.",
            2 => "3 FLOW · 발코니에서 씨앗 쪽 주황색 화살표를 고르세요.",
            _ => Restored ? "지붕 환기구가 열리고 바람이 퍼집니다." : "씨앗이 수관을 향해 날아갑니다."
        };
    }
}
