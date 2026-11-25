// EVERFLOW - Map1123 에셋 교체 (Swamp 팩 + FantasyEnvironments)
// 위치: Assets/EverflowMap/Editor/ 아래 (Editor 폴더 안)
// ※ 이 파일이 EFSwampAssetSwap.cs를 대신합니다. 예전 파일은 지워 주세요.
// 메뉴: Tools > EVERFLOW > 에셋 교체 - 미리보기 / 실행 / 전부 되돌리기
//
// 대상: 2026-11-24 썸네일 비교로 고른 14종 (데크·다리·문·벽·기둥·난간·받침대·선반·수조 벽·얼음·석판)
// 제외: 보상실(없애기로 함), 전망대 탑(직접 모델링), 채광실(건물 하나로 통합 예정), 계단(Step)
//
// 동작
//  - 그레이박스 MeshRenderer만 끄고(콜라이더·EF1123Motion 그대로) 모델을 자식 "Asset_…"로 넣음
//  - 움직이는 다리·문은 모델이 자식이라 연출을 그대로 따라감
//  - 모델과 큐브의 긴 방향이 다르면 90도 돌려서 맞춤
//  - "EVERFLOW | Six Collectible Courses" 아래만 건드림

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowMap.EditorTools
{
    public static class EFAssetSwap
    {
        const string CourseRoot = "EVERFLOW | Six Collectible Courses";
        const string Prefix = "Asset_";
        static readonly string[] OldPrefixes = { "Asset_", "Swamp_", "Visual_" };
        const string P = "Assets/EmacEArt/SwampArch_2025/Prefabs/";
        const string Floor1 = P + "Bld_Floor/EA_Bld_Wood_Floor_01a_PRE.prefab";
        const string Floor2 = P + "Bld_Floor/EA_Bld_Wood_Floor_02a_PRE.prefab";
        const string Bridge = P + "Bridge/EA_Bld_Wood_MarshBridge_Module_01a_PRE.prefab";
        const string Fence = P + "Fence/EA_Bld_Wood_Fence_01a_PRE.prefab";
        const string Pin = P + "Pin/EA_Wood_Pin_01a_PRE.prefab";
        const string FlatRock = P + "FlatGround/EA_Env_Rock_FlatGround_1a_PRE.prefab";

        const string FE = "Assets/FantasyEnvironments/Environments/";
        const string WoodWall = FE + "Town/Prefabs/WoodenWall_1.prefab";
        const string WoodGate = FE + "Town/Prefabs/WoodenWall_Gate.prefab";
        const string StoneWall = FE + "Town/Prefabs/Wall_part2.prefab";
        const string Stump = FE + "Ambient-Occlusion-Trees/Prefabs/Pine_stump.prefab";
        const string Rock1 = FE + "Prefabs/Rock1.prefab";

        const string Foliage = "Assets/EmaceArt/Stylized Foliage Pack/Prefabs/";
        const string Ice = Foliage + "Rock/EA03_Environment_Rock_Ice_01a_PRE.prefab";

        // Deck:    가로·세로는 큐브에 꼭 맞추고, 두께는 비율 유지. 윗면을 큐브 윗면에 맞춤 (밟는 높이 그대로)
        // Stretch: 큐브 크기에 꼭 맞게 늘림 (벽·문·난간·기둥·석판)
        // Uniform: 비율 유지, 큐브 안에 들어가게 (그루터기·얼음)
        enum Fit { Deck, Stretch, Uniform }

        sealed class Rule
        {
            public readonly string name; public readonly string[] prefabs; public readonly Fit fit;
            public Rule(string name, Fit fit, params string[] prefabs) { this.name = name; this.fit = fit; this.prefabs = prefabs; }
        }

        // 2026-11-24 썸네일 보고 고른 결과
        static readonly Rule[] Rules =
        {
            // 1. 나무 데크·부두: Wood_Floor 01a / 02a 섞어서
            new Rule("B shore dock", Fit.Deck, Floor1),
            new Rule("B exit turning deck", Fit.Deck, Floor2),
            new Rule("B return toward C", Fit.Deck, Floor1),
            new Rule("C departure dock", Fit.Deck, Floor2),
            new Rule("C lake landing", Fit.Deck, Floor1),
            new Rule("C wide landing", Fit.Deck, Floor2),
            new Rule("C glider launch deck", Fit.Deck, Floor1),
            new Rule("C windbreak access", Fit.Deck, Floor2),
            new Rule("W operator deck", Fit.Deck, Floor1),
            // 2. 다리: MarshBridge_Module_01a
            new Rule("C restored return bridge", Fit.Deck, Bridge),
            new Rule("Bridge plank model", Fit.Deck, Bridge),
            new Rule("Imported dock planks", Fit.Deck, Bridge),
            new Rule("Imported lake landing", Fit.Deck, Bridge),
            // 3. 문: WoodenWall_Gate (보상실 문은 보상실 정리 후 결정)
            new Rule("PH_D_Gate - working gate", Fit.Stretch, WoodGate),
            new Rule("C one-way cliff gate", Fit.Stretch, WoodGate),
            // 4. 석벽: WoodenWall_1 (채광실은 건물 하나로 따로 작업)
            new Rule("D eastern masonry", Fit.Stretch, WoodWall),
            new Rule("D western masonry", Fit.Stretch, WoodWall),
            new Rule("W left quay wall", Fit.Stretch, WoodWall),
            new Rule("W right quay wall", Fit.Stretch, WoodWall),
            // 5. 기둥: 나무 말뚝
            new Rule("Spout pier", Fit.Stretch, Pin),
            new Rule("Service windmill bearing", Fit.Stretch, Pin),
            // 6. 난간
            new Rule("C cliff-edge rail", Fit.Stretch, Fence),
            // 7. 씨앗 받침대
            new Rule("Decorative seed pedestal", Fit.Uniform, Stump),
            // 8. 바위 선반·발판: Rock_FlatGround_1a
            new Rule("Shared switchback rest", Fit.Deck, FlatRock),
            new Rule("Upper access", Fit.Deck, FlatRock),
            new Rule("E middle ledge", Fit.Deck, FlatRock),
            new Rule("E return turn", Fit.Deck, FlatRock),
            new Rule("E return contour", Fit.Deck, FlatRock),
            new Rule("E bench join", Fit.Deck, FlatRock),
            new Rule("D seed ledge", Fit.Deck, FlatRock),
            new Rule("Flower pad foundation", Fit.Deck, FlatRock),
            new Rule("G entry landing", Fit.Deck, FlatRock),
            new Rule("G windmill threshold", Fit.Deck, FlatRock),
            // 11. B 수조 벽 (예전 v25 그룹 안)
            new Rule("B_Wall_N", Fit.Stretch, StoneWall),
            new Rule("B_Wall_S_Dam", Fit.Stretch, StoneWall),
            new Rule("B_Wall_E1", Fit.Stretch, StoneWall),
            new Rule("B_Wall_E2", Fit.Stretch, StoneWall),
            new Rule("B_Wall_E_Outlet", Fit.Stretch, StoneWall),
            new Rule("B_Wall_W", Fit.Stretch, StoneWall),
            // 12. 누수구 얼음
            new Rule("Ice over the leak", Fit.Uniform, Ice),
            // 13. E 석판: Rock1 납작하게
            new Rule("PH_E_Slab - growing stone", Fit.Stretch, Rock1),
        };

        [MenuItem("Tools/EVERFLOW/에셋 교체 - 미리보기")]
        static void Preview() => Run(apply: false);

        [MenuItem("Tools/EVERFLOW/에셋 교체 - 실행")]
        static void Apply()
        {
            if (!EditorUtility.DisplayDialog("에셋 교체",
                    "썸네일로 고른 에셋으로 " + Rules.Length + "곳을 바꿉니다.\n" +
                    "Ctrl+Z 한 번, 또는 '에셋 교체 - 전부 되돌리기'로 원래대로 돌아갑니다.\n\n먼저 씬을 저장해 두세요.",
                    "실행", "취소")) return;
            Run(apply: true);
        }

        [MenuItem("Tools/EVERFLOW/에셋 교체 - 전부 되돌리기")]
        static void Revert()
        {
            var root = FindCourseRoot();
            if (root == null) { Debug.LogError("[에셋 교체] 코스 루트를 찾지 못했습니다."); return; }

            Undo.SetCurrentGroupName("에셋 교체 되돌리기");
            int group = Undo.GetCurrentGroup();
            int removed = 0, shown = 0;

            var visuals = root.GetComponentsInChildren<Transform>(true).Where(t => HasOldPrefix(t.name)).ToList();
            var oldRoot = FindRoot("EVERFLOW_Greybox_v25");
            if (oldRoot != null) visuals.AddRange(oldRoot.GetComponentsInChildren<Transform>(true).Where(t => HasOldPrefix(t.name)));
            foreach (var v in visuals)
            {
                if (v == null) continue;
                var owner = v.parent;
                Undo.DestroyObjectImmediate(v.gameObject);
                removed++;
                var mr = owner != null ? owner.GetComponent<MeshRenderer>() : null;
                if (mr != null && !mr.enabled) { Undo.RecordObject(mr, "Show greybox"); mr.enabled = true; shown++; }
            }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log($"[에셋 교체] 되돌리기 완료: Swamp 모델 {removed}개 삭제, 원래 모습 {shown}개 다시 보임");
        }

        static void Run(bool apply)
        {
            var root = FindCourseRoot();
            if (root == null)
            {
                Debug.LogError($"[에셋 교체] '{CourseRoot}'를 찾지 못했습니다. EVERFLOW_Map1123_puzzle 씬을 열어 주세요.");
                return;
            }

            var all = root.GetComponentsInChildren<Transform>(true).Where(t => !IsInsideVisual(t, root)).ToList();
            var old = FindRoot("EVERFLOW_Greybox_v25");
            if (old != null) all.AddRange(old.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("B_Wall_")));
            var log = new System.Text.StringBuilder();
            int swapped = 0;

            Undo.SetCurrentGroupName("에셋 교체");
            int group = Undo.GetCurrentGroup();

            foreach (var rule in Rules)
            {
                var found = all.Where(t => t.name == rule.name).ToList();
                var prefabs = rule.prefabs.Select(LoadPrefab).ToArray();
                var prefab = prefabs.FirstOrDefault(x => x != null);
                string note = found.Count == 0 ? "  ← 오브젝트 못 찾음"
                            : prefab == null ? "  ← 프리팹 못 찾음 (팩 임포트 확인)" : "";
                log.AppendLine($"  {rule.name}: {found.Count}개 → {string.Join(", ", rule.prefabs.Select(System.IO.Path.GetFileNameWithoutExtension))}{note}");
                if (!apply || prefab == null) continue;
                foreach (var t in found)
                    if (SwapOne(t.gameObject, prefab, rule.fit)) swapped++;
            }

            Undo.CollapseUndoOperations(group);

            if (apply)
            {
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
                log.Insert(0, $"[에셋 교체] 완료: 모델 {swapped}개 넣음 (Ctrl+Z 또는 '전부 되돌리기')\n");
            }
            else log.Insert(0, "[에셋 교체] 미리보기 (아무것도 바꾸지 않음)\n");
            Debug.Log(log.ToString());
        }

        static bool SwapOne(GameObject target, GameObject prefab, Fit fit)
        {
            Transform parent = target.transform;
            var greyRenderer = target.GetComponent<MeshRenderer>();
            var greyFilter = target.GetComponent<MeshFilter>();
            if (greyRenderer == null || greyFilter == null || greyFilter.sharedMesh == null) return false;

            // 그레이박스 크기는 부모(자기 자신) 로컬 기준으로 계산 → 돌아가 있는 큐브도 정확히 맞음
            Bounds grey = greyFilter.sharedMesh.bounds;
            Vector3 ps = parent.lossyScale;
            Vector3 aps = new Vector3(Mathf.Abs(ps.x), Mathf.Abs(ps.y), Mathf.Abs(ps.z));

            foreach (var old in parent.Cast<Transform>().Where(c => HasOldPrefix(c.name)).Select(c => c.gameObject).ToList())
                Undo.DestroyObjectImmediate(old);

            Undo.RecordObject(greyRenderer, "Hide greybox");
            greyRenderer.enabled = false;

            // 부모 아래에 바로 생성 (생성 뒤 부모를 옮기면 위치·크기 변경이 사라지는 문제 방지)
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(instance, "Create asset visual");
            instance.name = Prefix + prefab.name;

            var t = instance.transform;
            Undo.RecordObject(t, "Fit asset visual");
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;

            // 부모가 찌그러진 크기여도 모델은 원래 비율(월드 배율 1)로 시작
            t.localScale = ScaleFor(Vector3.one, ps, false);
            if (!LocalBounds(instance, parent, out Bounds model))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                return true;
            }

            // 긴 방향이 다르면 Y축 90도 회전 (90도 단위라 찌그러진 부모 아래에서도 비틀림 없음)
            bool rotated = false;
            if (fit != Fit.Uniform && (grey.size.x * aps.x >= grey.size.z * aps.z) != (model.size.x * aps.x >= model.size.z * aps.z))
            {
                rotated = true;
                t.localRotation = Quaternion.Euler(0, 90, 0);
                t.localScale = ScaleFor(Vector3.one, ps, true);
                LocalBounds(instance, parent, out model);
            }

            Vector3 greyW = Vector3.Scale(grey.size, aps), modelW = Vector3.Scale(model.size, aps);
            float rx = Ratio(greyW.x, modelW.x), ry = Ratio(greyW.y, modelW.y), rz = Ratio(greyW.z, modelW.z);
            Vector3 r = fit == Fit.Stretch ? new Vector3(rx, ry, rz)
                      : fit == Fit.Deck ? new Vector3(rx, (rx + rz) * 0.5f, rz)
                      : Vector3.one * Mathf.Min(rx, ry, rz);
            t.localScale = ScaleFor(r, ps, rotated);

            LocalBounds(instance, parent, out model);
            float targetY = fit == Fit.Deck ? grey.max.y : grey.min.y;
            float modelY = fit == Fit.Deck ? model.max.y : model.min.y;
            t.localPosition += new Vector3(grey.center.x - model.center.x, targetY - modelY, grey.center.z - model.center.z);
            // 프리팹 인스턴스의 변경값을 씬에 확실히 기록 (안 하면 원래 값으로 되돌아감)
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            return true;
        }

        // 모델의 메시 경계를 frame(그레이박스) 로컬 좌표로 계산
        static bool LocalBounds(GameObject root, Transform frame, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            Matrix4x4 toFrame = frame.worldToLocalMatrix;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                Matrix4x4 m = toFrame * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(c);
                    if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return found;
        }

        static GameObject LoadPrefab(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null) return go;
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            foreach (var guid in AssetDatabase.FindAssets(file + " t:Prefab"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == file)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
            return null;
        }

        static Transform FindCourseRoot() => FindRoot(CourseRoot);

        static Transform FindRoot(string rootName)
        {
            foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    if (t.name == rootName) return t;
            return null;
        }

        static bool IsInsideVisual(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
                if (HasOldPrefix(t.name)) return true;
            return false;
        }

        // 부모 축 기준 배율 r → 자식 localScale (90도 회전 시 X/Z가 서로 바뀜)
        static Vector3 ScaleFor(Vector3 r, Vector3 ps, bool rotated) => rotated
            ? new Vector3(r.z * SafeInv(ps.z), r.y * SafeInv(ps.y), r.x * SafeInv(ps.x))
            : new Vector3(r.x * SafeInv(ps.x), r.y * SafeInv(ps.y), r.z * SafeInv(ps.z));

        static bool HasOldPrefix(string n) => OldPrefixes.Any(p => n.StartsWith(p));

        static float SafeInv(float v) => Mathf.Abs(v) < 1e-5f ? 1f : 1f / v;
        static float Ratio(float want, float have) => have < 1e-4f ? 1f : want / have;
    }
}
