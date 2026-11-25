#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EverflowMap.EditorTools
{
    // 잎·연잎·낙엽·씨앗 자동 배치 (B, C, D, E 코스)
    // 메뉴: Tools > EVERFLOW > 잎·씨앗 에셋 배치 (B C D E)
    // 퍼즐 장치는 건드리지 않고, 보이는 그레이박스 자리에 새 모델을 '자식'으로 맞춰 넣고 옛 렌더러만 꺼요.
    // (장치가 커지거나 움직이면 새 모델도 같이 움직임) Ctrl+Z 한 번이면 전부 되돌아가요.
    public static class EFPlaceLeafAssets
    {
        const string Tag = "Leaf_Asset_";

        [MenuItem("Tools/EVERFLOW/잎·씨앗 에셋 배치 (B C D E)")]
        public static void Run()
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("잎·씨앗 배치");
            var log = new List<string>();
            try
            {
                // B: 떠 있는 연잎 3장 (발판)
                // 물이 차기 전엔 마른 호수 바닥에 붙은 칙칙한 색 → 물과 함께 떠오르며 생기 있는 색
                foreach (var t in All("Floating leaf 1", "Floating leaf 2", "Floating leaf 3"))
                    Tint(Fit(t, "EF_Lotus", log, topAlign: true, hideAlso: Near(t, "Leaf mesh")), t.parent,
                         new Color(0.52f, 0.5f, 0.36f), 2.2f);

                // B: 잎 물받이 (BLOOM 으로 커짐 → 시든 색에서 초록)
                foreach (var t in All("Catchment leaf"))
                    Tint(Fit(t, "EF_Leaf", log), t.parent);

                // C: 잎 나룻배 (줄기 달린 연잎)
                foreach (var t in All("Ferry leaf model"))
                    Fit(t, "EF_LotusStem", log, topAlign: true, sizeFromTopDisc: true, hideAlso: t.parent);

                // C: 바람막이 잎 (세운 잎, BLOOM)
                foreach (var t in All("Vertical windbreak leaf"))
                    Tint(Fit(t, "EF_Leaf", log, tipUp: true, flat: false), t.parent);

                // D: 잎 계단 장식 잎 4장 + 디딤판 18칸 (퍼즐이 켜서 보여 줌 → 켜지는 순간 초록으로)
                foreach (var t in All("Leaf on stair"))
                    Tint(Fit(t, "EF_Leaf", log), null);
                var treads = FindScene("Stem and leaf treads");
                if (treads != null)
                    foreach (var st in treads.GetComponentsInChildren<Transform>(true).Where(x => x.name.StartsWith("Step")))
                        Tint(Fit(st, "EF_Leaf", log, quiet: true, topAlign: true), null);

                // D: 꽃가루 → 마른 낙엽 더미 (그룹 안에 넣어 FLOW 때 같이 사라짐)
                var pollen = FindScene("D dancing pollen");
                if (pollen != null) Pile(pollen, log);

                // D: 씨앗 2개
                foreach (var t in All("Seed from the pedestal", "도착한 씨앗"))
                    Fit(t, "EF_Seed", log, selfVisual: true, flat: false);

                // E: 회전 잎 → 큰 연잎 (줄기 = 경첩)
                var hinge = FindScene("Leaf hinge");
                if (hinge != null) BigLotus(hinge, log);
            }
            catch (Exception e) { Debug.LogException(e); log.Add("오류: " + e.Message); }

            Undo.CollapseUndoOperations(group);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            var msg = string.Join("\n", log);
            Debug.Log("[잎·씨앗 배치]\n" + msg);
            EditorUtility.DisplayDialog("잎·씨앗 에셋 배치", msg + "\n\n마음에 안 들면 Ctrl+Z 한 번으로 전부 되돌려져요.", "확인");
        }

        // ------------------------------------------------------------------ 맞춰 넣기
        // old(보이는 그레이박스) 의 범위에 model 을 맞춰서 old 의 부모 아래 자식으로 넣는다.
        // selfVisual: old 자체가 장치(움직임)라 old 의 자식으로 넣고 스케일을 상쇄
        static GameObject Fit(Transform old, string modelName, List<string> log, bool topAlign = false,
                              bool tipUp = false, bool sizeFromTopDisc = false, bool selfVisual = false,
                              bool quiet = false, Transform hideAlso = null, bool flat = true)
        {
            var model = FindModel(modelName);
            if (model == null) { if (!quiet) log.Add(old.name + ": " + modelName + " 모델 없음"); return null; }
            Transform parent = selfVisual ? old : old.parent;
            if (parent.GetComponentsInChildren<Transform>(true).Any(c => c.name == Tag + old.name + "_" + old.GetSiblingIndex()))
            { if (!quiet) log.Add(old.name + ": 이미 배치됨"); return null; }

            // 옛 모습의 범위 (parent 로컬)
            var ob = BoundsIn(parent, old, includeChildren: false);
            if (ob.size == Vector3.zero) { if (!quiet) log.Add(old.name + ": 메시 없음"); return null; }

            Transform holder = parent;
            if (selfVisual)
            {
                var v = new GameObject(Tag + "Visual").transform;
                Undo.RegisterCreatedObjectUndo(v.gameObject, "v");
                v.SetParent(old, false);
                var s = old.localScale; v.localScale = new Vector3(1 / s.x, 1 / s.y, 1 / s.z);
                holder = v;
                ob = BoundsIn(holder, old, includeChildren: false);
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, holder);
            Undo.RegisterCreatedObjectUndo(inst, "fit");
            inst.name = Tag + old.name + "_" + old.GetSiblingIndex();
            var t = inst.transform;
            t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;

            var mb = BoundsIn(t, t, true);                       // 모델 자기 축 범위
            Vector3 msz = mb.size;
            if (sizeFromTopDisc)
            {   // 줄기 달린 연잎: 크기는 잎 원판(위쪽)으로만 잼
                msz.y = Mathf.Min(msz.y, 0.2f);
            }

            // 축 짝짓기: 모델의 긴 축 순서 ↔ 옛 모습의 긴 축 순서
            int[] mo = Order(msz), oo = Order(ob.size);
            var map = new int[3];                                 // model axis → target axis
            for (int i = 0; i < 3; i++) map[mo[i]] = oo[i];
            if (flat && !tipUp)
            {   // 잎·연잎은 눕힌 채로: 모델 위(Y) → 위(Y), 수평 두 축은 긴 것끼리
                int mLong = msz.x >= msz.z ? 0 : 2, oLong = ob.size.x >= ob.size.z ? 0 : 2;
                map[1] = 1; map[mLong] = oLong; map[2 - mLong] = 2 - oLong;
                mo = new[] { mLong, 2 - mLong, 1 };
            }
            Vector3[] cols = new Vector3[3];
            for (int a = 0; a < 3; a++) cols[a] = Axis(map[a]);
            if (tipUp)
            {   // 잎(+X 로 뻗음)이 위로 서게: 모델 X → 위(Y)
                cols[0] = Vector3.up * (mb.center.x < 0 ? -1f : 1f);   // 잎 끝이 위로
                int other = MaxAxisXZ(ob.size);
                cols[2] = Axis(other);
                cols[1] = Vector3.Cross(cols[2], cols[0]);
            }
            // 오른손/왼손 맞추기
            if (Vector3.Dot(Vector3.Cross(cols[0], cols[1]), cols[2]) < 0) cols[2] = -cols[2];
            // 연잎·잎이 뒤집히지 않게 (모델 위 Y 가 아래를 보면 뒤집음)
            if (!tipUp && Vector3.Dot(cols[1], Vector3.up) < -0.5f) { cols[1] = -cols[1]; cols[2] = -cols[2]; }
            var rot = Quaternion.LookRotation(cols[2], cols[1]);
            t.localRotation = rot;

            // 크기: 큰 두 축은 옛 모습에 맞추고, 가장 얇은 축은 두 배율의 평균 (납작해지지 않게)
            Vector3 sc = Vector3.one;
            float s0 = Size(ob.size, map[mo[0]]) / Mathf.Max(msz[mo[0]], 1e-3f);
            float s1 = Size(ob.size, map[mo[1]]) / Mathf.Max(msz[mo[1]], 1e-3f);
            sc[mo[0]] = s0; sc[mo[1]] = s1; sc[mo[2]] = (s0 + s1) / 2f;
            t.localScale = sc;

            // 위치: 범위 중심 맞춤 (topAlign 이면 윗면 높이 맞춤)
            var nb = BoundsIn(holder, t, true);
            Vector3 delta = ob.center - nb.center;
            if (topAlign) delta.y = ob.max.y - nb.max.y;
            if (sizeFromTopDisc) delta.y = ob.max.y - nb.max.y;
            t.localPosition += delta;
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);

            Hide(old);
            if (hideAlso != null) Hide(hideAlso);
            FixMaterials(inst);
            if (!quiet) log.Add(old.name + " → " + modelName);
            return inst;
        }

        static void Pile(Transform pollen, List<string> log)
        {
            if (pollen.Find(Tag + "LeafPile") != null) { log.Add("꽃가루: 이미 배치됨"); return; }
            var model = FindModel("EF_LeafPile");
            if (model == null) { log.Add("꽃가루: EF_LeafPile 모델 없음"); return; }
            var b = BoundsIn(pollen, pollen, true);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, pollen);
            Undo.RegisterCreatedObjectUndo(inst, "pile");
            inst.name = Tag + "LeafPile";
            var t = inst.transform;
            t.localRotation = Quaternion.identity; t.localScale = Vector3.one; t.localPosition = Vector3.zero;
            var mb = BoundsIn(pollen, t, true);
            bool swap = (b.size.x >= b.size.z) != (mb.size.x >= mb.size.z);
            if (swap) { t.localRotation = Quaternion.Euler(0, 90, 0); mb = BoundsIn(pollen, t, true); }
            float k = Mathf.Max(b.size.x / Mathf.Max(mb.size.x, 0.01f), b.size.z / Mathf.Max(mb.size.z, 0.01f));
            t.localScale = Vector3.one * Mathf.Clamp(k, 0.5f, 3f);
            mb = BoundsIn(pollen, t, true);
            var d = b.center - mb.center; d.y = b.min.y - mb.min.y;
            t.localPosition += d;
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            foreach (var r in pollen.GetComponentsInChildren<Renderer>(true))
                if (!r.transform.IsChildOf(t)) { Undo.RecordObject(r, "hide"); r.enabled = false; }
            FixMaterials(inst);
            log.Add("꽃가루 → 마른 낙엽 더미 (알갱이 렌더러 끔, FLOW 때 같이 사라짐)");
        }

        static void BigLotus(Transform hinge, List<string> log)
        {
            if (hinge.Find(Tag + "LotusBig") != null) { log.Add("E 회전 잎: 이미 배치됨"); return; }
            var model = FindModel("EF_LotusBig");
            if (model == null) { log.Add("E 회전 잎: EF_LotusBig 모델 없음"); return; }
            var support = hinge.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "Rotate leaf bridge support");
            var leaf = hinge.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "Rotation leaf");
            if (support == null) { log.Add("E 회전 잎: Rotate leaf bridge support 없음"); return; }
            var sb = BoundsIn(hinge, support, false);
            int longAx = MaxAxisXZ(sb.size);
            Vector3 outDir = Axis(longAx) * Mathf.Sign(sb.center[longAx] == 0 ? 1 : sb.center[longAx]);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, hinge);
            Undo.RegisterCreatedObjectUndo(inst, "lotus");
            inst.name = Tag + "LotusBig";
            var t = inst.transform;
            t.localPosition = Vector3.zero; t.localScale = Vector3.one; t.localRotation = Quaternion.identity;
            var mb = BoundsIn(t, t, true);
            bool mLongX = mb.size.x >= mb.size.z;
            Vector3 mL = mLongX ? new Vector3(Mathf.Sign(mb.center.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(mb.center.z));
            float mLen = mLongX ? mb.size.x : mb.size.z;
            t.localRotation = Quaternion.LookRotation(outDir, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(mL, Vector3.up));
            float k = (Mathf.Abs(sb.center[longAx]) + sb.size[longAx] / 2f) / Mathf.Max(mLen, 0.01f);
            t.localScale = Vector3.one * k;
            // 잎 윗면을 발판 윗면 높이에
            var nb = BoundsIn(hinge, t, true);
            float discTop = nb.max.y;
            t.localPosition += new Vector3(0, sb.max.y - discTop, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            Hide(support);
            if (leaf != null) Hide(leaf);
            FixMaterials(inst);
            log.Add("E 회전 잎 → 큰 연잎 (지름 약 " + (mLen * k).ToString("0.0") + "m, 줄기 = 경첩)");
        }

        static void Tint(GameObject inst, Transform motionOwner, Color? withered = null, float seconds = 1f)
        {
            if (inst == null) return;
            var c = Undo.AddComponent<EFBloomTint>(inst);
            if (motionOwner != null) c.source = motionOwner.GetComponent<EF1123Motion>();
            if (withered.HasValue) c.withered = withered.Value;
            c.seconds = seconds;
        }

        // ------------------------------------------------------------------ 도우미
        static IEnumerable<Transform> All(params string[] names) =>
            UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => names.Contains(t.name) && !t.name.StartsWith(Tag)).OrderBy(t => t.GetSiblingIndex()).ToList();

        static Transform FindScene(string name) => All(name).FirstOrDefault();

        static Transform Near(Transform t, string name)
        {
            if (t.parent == null) return null;
            return t.parent.Cast<Transform>().Where(c => c.name == name)
                .OrderBy(c => (c.position - t.position).sqrMagnitude).FirstOrDefault();
        }

        static void Hide(Transform t)
        {
            var r = t.GetComponent<Renderer>();
            if (r != null && r.enabled) { Undo.RecordObject(r, "hide"); r.enabled = false; }
        }

        static Bounds BoundsIn(Transform space, Transform under, bool includeChildren)
        {
            bool has = false; var b = new Bounds();
            var mfs = includeChildren ? under.GetComponentsInChildren<MeshFilter>(true) : under.GetComponents<MeshFilter>();
            foreach (var mf in mfs)
            {
                if (mf.sharedMesh == null) continue;
                var mbb = mf.sharedMesh.bounds;
                var m = space.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var p = m.MultiplyPoint3x4(mbb.center + Vector3.Scale(mbb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!has) { b = new Bounds(p, Vector3.zero); has = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        static int[] Order(Vector3 v) => new[] { 0, 1, 2 }.OrderByDescending(i => v[i]).ToArray();
        static float Size(Vector3 v, int i) => v[i];
        static Vector3 Axis(int i) => i == 0 ? Vector3.right : (i == 1 ? Vector3.up : Vector3.forward);
        static int MaxAxisXZ(Vector3 v) => v.x >= v.z ? 0 : 2;

        static GameObject FindModel(string prefix)
        {
            var paths = AssetDatabase.FindAssets(prefix + " t:Model").Concat(AssetDatabase.FindAssets(prefix + " t:Prefab"))
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .Where(p =>
                {
                    string f = Path.GetFileNameWithoutExtension(p);
                    if (!f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
                    string rest = f.Substring(prefix.Length);     // EF_Lotus 가 EF_LotusStem/Big 을 잡지 않게
                    return rest.Length == 0 || rest[0] == '.' || rest[0] == ' ' || char.IsDigit(rest[0]);
                })
                .OrderByDescending(p => File.GetLastWriteTime(p)).ToList();
            return paths.Count == 0 ? null : AssetDatabase.LoadAssetAtPath<GameObject>(paths[0]);
        }

        static Material Mat(string name)
        {
            var path = AssetDatabase.FindAssets(name + " t:Material").Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p).Equals(name, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.Contains("FantasyEnvironments")).FirstOrDefault();
            return path != null ? AssetDatabase.LoadAssetAtPath<Material>(path) : null;
        }

        static Material DryLeaf()
        {
            var m = Mat("EF_LeafDry");
            if (m != null) return m;
            var plant = Mat("Plant");
            if (plant == null) return null;
            m = new Material(plant) { name = "EF_LeafDry" };
            var c = new Color(0.46f, 0.37f, 0.27f);          // 칙칙한 마른 갈색
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            Directory.CreateDirectory("Assets/EverflowMap/Materials");
            AssetDatabase.CreateAsset(m, "Assets/EverflowMap/Materials/EF_LeafDry.mat");
            return m;
        }

        static void FixMaterials(GameObject inst)
        {
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || AssetDatabase.GetAssetPath(mats[i]).EndsWith(".mat")) continue;
                    string n = mats[i].name.Split('.')[0].Trim();
                    var m = n == "EF_LeafDry" ? DryLeaf() : Mat(n);
                    if (m != null) { mats[i] = m; changed = true; }
                }
                if (changed) { Undo.RecordObject(r, "mat"); r.sharedMaterials = mats; }
            }
        }
    }
}
#endif
