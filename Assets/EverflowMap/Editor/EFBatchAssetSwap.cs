// EVERFLOW - Map1123 그레이박스 일괄 에셋 교체
// 위치: Assets/EverflowMap/Editor/ 아래 (Editor 폴더 안)
// 메뉴: Tools > EVERFLOW > 1123 일괄 교체 - 미리보기   (바꾸지 않고 찾은 목록만 Console에 출력)
//       Tools > EVERFLOW > 1123 일괄 교체 - 실행       (한 번에 교체, Ctrl+Z 한 번으로 전부 되돌리기)
//
// 하는 일
//  1) 판정용 큐브의 렌더러 끄기 (콜라이더·스크립트는 그대로)
//  2) 팩 에셋으로 바꿀 큐브: 렌더러를 끄고 프리팹을 자식(Visual_…)으로 넣어 크기·위치 맞추기
//  - "EVERFLOW | Six Collectible Courses" 아래만 건드림 (예전 v25 그레이박스는 건드리지 않음)
//  - 다시 실행하면 이전 Visual_ 자식을 지우고 새로 넣음

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowMap.EditorTools
{
    public static class EFBatchAssetSwap
    {
        const string CourseRoot = "EVERFLOW | Six Collectible Courses";
        const string VisualPrefix = "Visual_";

        const string Foliage = "Assets/EmaceArt/Stylized Foliage Pack/Prefabs/";
        const string Fantasy = "Assets/FantasyEnvironments/Environments/Prefabs/";

        static readonly string[] Cliffs =
        {
            Foliage + "Clif/EA03_Environment_Clif_01c_PRE.prefab",
            Foliage + "Clif/EA03_Environment_Clif_01d_PRE.prefab",
            Foliage + "Clif/EA03_Environment_Clif_01e_PRE.prefab",
        };

        enum Fit { Uniform, Stretch }           // Uniform: 비율 유지, Stretch: 큐브 크기에 꼭 맞게 늘림(바위·절벽용)
        enum Align { Bottom, Center }

        sealed class Rule
        {
            public string name; public string[] prefabs; public Fit fit; public Align align;
            public Rule(string name, Fit fit, Align align, params string[] prefabs)
            { this.name = name; this.fit = fit; this.align = align; this.prefabs = prefabs; }
        }

        // ── 렌더러만 끌 오브젝트 (이름 그대로) ─────────────────────────────
        static readonly string[] HideOnly =
        {
            "Windmill corridor collision", "Windmill facade collision", "Windmill back collision",
            "Bridge planks collision",
            "Floating leaf 1", "Floating leaf 2", "Floating leaf 3",
            "Leaf ferry",
            "Physical door panel",
            "Rotate leaf bridge support",
            "Turbulent air face",
        };

        // ── 팩 에셋으로 바꿀 오브젝트 ─────────────────────────────────────
        static readonly Rule[] Swaps =
        {
            new Rule("Ice over the leak", Fit.Uniform, Align.Center,
                Foliage + "Rock/EA03_Environment_Rock_Ice_01a_PRE.prefab"),
            new Rule("Decorative seed pedestal", Fit.Uniform, Align.Bottom,
                Foliage + "Rock/EA03_Environment_Rock_Bowl_01c_PRE.prefab"),
            new Rule("Spout pier", Fit.Stretch, Align.Bottom, Cliffs),
            new Rule("D eastern masonry", Fit.Stretch, Align.Bottom, Cliffs),
            new Rule("D western masonry", Fit.Stretch, Align.Bottom, Cliffs),
            new Rule("D seed ledge", Fit.Stretch, Align.Bottom, Cliffs),
            new Rule("E middle ledge", Fit.Stretch, Align.Bottom, Fantasy + "Rock3.prefab", Fantasy + "Rock1.prefab"),
            new Rule("E return turn", Fit.Stretch, Align.Bottom, Fantasy + "Rock3.prefab", Fantasy + "Rock1.prefab"),
            new Rule("E return contour", Fit.Stretch, Align.Bottom, Fantasy + "Rock3.prefab", Fantasy + "Rock1.prefab"),
            new Rule("E bench join", Fit.Stretch, Align.Bottom, Fantasy + "Rock3.prefab", Fantasy + "Rock1.prefab"),
            new Rule("W left quay wall", Fit.Stretch, Align.Bottom, Cliffs),
            new Rule("W right quay wall", Fit.Stretch, Align.Bottom, Cliffs),
        };

        [MenuItem("Tools/EVERFLOW/1123 일괄 교체 - 미리보기")]
        static void Preview() => Run(apply: false);

        [MenuItem("Tools/EVERFLOW/1123 일괄 교체 - 실행")]
        static void Apply()
        {
            if (!EditorUtility.DisplayDialog("1123 일괄 교체",
                    "판정용 큐브 렌더러를 끄고, 바위·절벽·얼음 모델을 넣습니다.\n" +
                    "실행 후 Ctrl+Z 한 번으로 전부 되돌릴 수 있습니다.\n\n먼저 씬을 저장(또는 커밋)해 두는 걸 추천합니다.",
                    "실행", "취소")) return;
            Run(apply: true);
        }

        static void Run(bool apply)
        {
            var root = FindCourseRoot();
            if (root == null)
            {
                Debug.LogError($"[1123 일괄 교체] '{CourseRoot}' 오브젝트를 찾지 못했습니다. EVERFLOW_Map1123_puzzle 씬을 열어 주세요.");
                return;
            }

            var all = root.GetComponentsInChildren<Transform>(true)
                          .Where(t => !IsInsideVisual(t, root)).ToList();
            var log = new System.Text.StringBuilder();
            int hidden = 0, swapped = 0;

            Undo.SetCurrentGroupName("EVERFLOW 1123 일괄 교체");
            int group = Undo.GetCurrentGroup();

            log.AppendLine("■ 렌더러 끄기");
            foreach (var name in HideOnly)
            {
                var found = all.Where(t => t.name == name).ToList();
                log.AppendLine($"  {name}: {found.Count}개" + (found.Count == 0 ? "  ← 못 찾음" : ""));
                if (!apply) continue;
                foreach (var t in found)
                {
                    var mr = t.GetComponent<MeshRenderer>();
                    if (mr == null || !mr.enabled) continue;
                    Undo.RecordObject(mr, "Hide greybox");
                    mr.enabled = false;
                    hidden++;
                }
            }

            log.AppendLine("■ 팩 에셋 교체");
            foreach (var rule in Swaps)
            {
                var found = all.Where(t => t.name == rule.name).ToList();
                var prefabs = rule.prefabs.Select(LoadPrefab).Where(p => p != null).ToArray();
                string note = found.Count == 0 ? "  ← 오브젝트 못 찾음"
                            : prefabs.Length == 0 ? "  ← 프리팹 못 찾음 (경로 확인: " + rule.prefabs[0] + ")" : "";
                log.AppendLine($"  {rule.name}: {found.Count}개 → {string.Join(", ", prefabs.Select(p => p.name))}{note}");
                if (!apply || prefabs.Length == 0) continue;

                for (int i = 0; i < found.Count; i++)
                    if (SwapOne(found[i].gameObject, prefabs[i % prefabs.Length], rule.fit, rule.align))
                        swapped++;
            }

            Undo.CollapseUndoOperations(group);

            if (apply)
            {
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
                log.Insert(0, $"[1123 일괄 교체] 완료: 렌더러 끔 {hidden}개, 모델 넣음 {swapped}개 (Ctrl+Z로 되돌리기)\n");
            }
            else log.Insert(0, "[1123 일괄 교체] 미리보기 (아무것도 바꾸지 않음)\n");

            Debug.Log(log.ToString());
        }

        // 경로로 못 찾으면(폴더 이름 대소문자 차이 등) 파일 이름으로 다시 찾기
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

        static Transform FindCourseRoot()
        {
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    if (t.name == CourseRoot) return t;
            return null;
        }

        static bool SwapOne(GameObject target, GameObject prefab, Fit fit, Align align)
        {
            Transform parent = target.transform;
            var greyRenderer = target.GetComponent<MeshRenderer>();
            if (greyRenderer == null) return false;
            Bounds grey = greyRenderer.bounds;

            var old = new List<GameObject>();
            foreach (Transform child in parent)
                if (child.name.StartsWith(VisualPrefix)) old.Add(child.gameObject);
            foreach (var o in old) Undo.DestroyObjectImmediate(o);

            Undo.RecordObject(greyRenderer, "Hide greybox");
            greyRenderer.enabled = false;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, target.scene);
            Undo.RegisterCreatedObjectUndo(instance, "Create visual");
            instance.name = VisualPrefix + prefab.name;
            Undo.SetTransformParent(instance.transform, parent, "Parent visual");

            var t = instance.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;

            // 부모 큐브가 찌그러진 크기여도 모델 크기를 월드 기준으로 계산
            Vector3 ps = parent.lossyScale;
            Vector3 inv = new Vector3(SafeInv(ps.x), SafeInv(ps.y), SafeInv(ps.z));
            t.localScale = inv;

            if (!TryGetBounds(instance, out Bounds model)) return true;

            Vector3 world;
            if (fit == Fit.Stretch)
                world = new Vector3(Ratio(grey.size.x, model.size.x), Ratio(grey.size.y, model.size.y),
                                    Ratio(grey.size.z, model.size.z));
            else
            {
                float f = Mathf.Min(Ratio(grey.size.x, model.size.x), Ratio(grey.size.y, model.size.y),
                                    Ratio(grey.size.z, model.size.z));
                world = Vector3.one * f;
            }
            t.localScale = Vector3.Scale(inv, world);

            TryGetBounds(instance, out model);
            Vector3 targetPoint = new Vector3(grey.center.x, align == Align.Bottom ? grey.min.y : grey.center.y, grey.center.z);
            Vector3 modelPoint = new Vector3(model.center.x, align == Align.Bottom ? model.min.y : model.center.y, model.center.z);
            t.position += targetPoint - modelPoint;
            return true;
        }

        static bool IsInsideVisual(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
                if (t.name.StartsWith(VisualPrefix)) return true;
            return false;
        }

        static float SafeInv(float v) => Mathf.Abs(v) < 1e-5f ? 1f : 1f / v;
        static float Ratio(float want, float have) => have < 1e-4f ? 1f : want / have;

        static bool TryGetBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return found;
        }
    }
}
