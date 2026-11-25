#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EverflowMap.EditorTools
{
    // W 코스(수문·도개교) 그레이박스를 에셋으로 '대체'.
    // 메뉴: Tools > EVERFLOW > W 코스 에셋으로 대체
    //  - Maintenance sluice           → EF_SluicePanel        (수문 장치 그 자체가 됨)
    //  - W 계단 3묶음의 Step들          → EF_WoodWalk 나무 경사로 (콜라이더 포함), Step은 삭제
    //  - Service windmill spindle     → EF_A2_Drum            (풍차 장치 그 자체가 됨, 회전축 유지)
    //  - Opened by the windmill rope  → EF_A2_Bridge          (도개교 장치 그 자체가 됨) + EF_A2_BridgeHinge 고정
    //  - 줄감개 ↔ 도개교 줄 2가닥      → EFRopeLink
    // 대체할 때 옛 오브젝트의 퍼즐 스크립트(EF1123Motion)와 콜라이더를 새 오브젝트로 옮기고,
    // 씬 안 모든 스크립트의 연결(Motions 목록 등)을 새 오브젝트로 바꾼 다음 옛 것을 지워요. 이름도 같게.
    // Ctrl+Z 한 번이면 전부 되돌아가요.
    public static class EFPlaceWAssets
    {
        const string Tag = "W_Asset_";

        [MenuItem("Tools/EVERFLOW/W 코스 에셋으로 대체")]
        public static void Run()
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("W 코스 에셋으로 대체");
            var log = new List<string>();

            var root = FindInScene("W | Sluice and drawbridge");
            if (root == null) { EditorUtility.DisplayDialog("W 대체", "'W | Sluice and drawbridge' 를 씬에서 못 찾았어요.", "확인"); return; }

            GameObject drum = null, bridge = null;
            try
            {
                Sluice(root, log);
                Ramps(root, log);
                drum = Drum(root, log);
                bridge = Bridge(root, log);
                Ropes(root, drum, bridge, log);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                log.Add("오류: " + e.Message + "  (Ctrl+Z 로 되돌린 뒤 이 메시지를 알려 주세요)");
            }

            Undo.CollapseUndoOperations(group);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            string msg = string.Join("\n", log);
            Debug.Log("[W 대체]\n" + msg);
            EditorUtility.DisplayDialog("W 코스 에셋으로 대체", msg + "\n\n마음에 안 들면 Ctrl+Z 한 번으로 전부 되돌려져요.", "확인");
        }

        // ================================================================== 수문
        static void Sluice(Transform root, List<string> log)
        {
            var old = FindUnder(root, "Maintenance sluice");
            var model = FindModel("EF_SluicePanel");
            if (old == null || model == null) { log.Add("수문: " + (old == null ? "Maintenance sluice 없음" : "EF_SluicePanel 모델 없음") + " → 건너뜀"); return; }
            if (IsAsset(old)) { log.Add("수문: 이미 대체됨"); return; }

            Vector3 want = old.lossyScale;                       // 그레이박스 판 크기 (m), 장치 로컬 축 기준
            var inst = Spawn(model, old.parent, old.localPosition, old.localRotation);
            var b = MeshBounds(inst.transform);
            Quaternion fix = b.size.z > b.size.x ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;   // 가로가 X 가 되게
            Vector3 sz = RotSize(fix, b.size);                    // 장치 축 기준 모델 크기
            Vector3 kDev = new Vector3(want.x / Mathf.Max(sz.x, 0.01f), want.y / Mathf.Max(sz.y, 0.01f), 1f);
            inst.transform.localRotation = old.localRotation * fix;
            inst.transform.localScale = Abs(Quaternion.Inverse(fix) * kDev);
            Recenter(inst.transform, old.position);

            TakeOver(old, inst, fix, log, keepChildren: new string[0]);
            log.Add("수문: Maintenance sluice → EF_SluicePanel (" + want.x.ToString("0.00") + " x " + want.y.ToString("0.00") + "m)");
        }

        // ================================================================== 계단 → 나무 경사로
        static void Ramps(Transform root, List<string> log)
        {
            var model = FindModel("EF_WoodWalk");
            if (model == null) { log.Add("계단: EF_WoodWalk 모델 없음 → 건너뜀"); return; }
            foreach (var gname in new[] { "W service stairs", "W upper service stairs", "W return staircase" })
            {
                var grp = FindUnder(root, gname);
                if (grp == null) { log.Add("계단: " + gname + " 없음"); continue; }
                var steps = grp.GetComponentsInChildren<Transform>(true)
                    .Where(t => t != grp && t.name.StartsWith("Step") && t.GetComponent<Collider>() != null)
                    .OrderBy(t => t.position.y).ToList();
                if (steps.Count < 2) { log.Add("계단: " + gname + " 이미 대체됐거나 Step 없음"); continue; }

                var first = steps[0]; var last = steps[steps.Count - 1];
                Vector3 dirH = last.position - first.position; dirH.y = 0; dirH.Normalize();
                float depth = first.lossyScale.z, half = first.lossyScale.y / 2f, width = first.lossyScale.x;
                Vector3 a = first.position + Vector3.up * half - dirH * depth / 2f;
                Vector3 bp = last.position + Vector3.up * half + dirH * depth / 2f;
                Vector3 d = bp - a; float len = d.magnitude; d /= len;
                Vector3 n = Vector3.Cross(Vector3.Cross(d, Vector3.up), d).normalized; if (n.y < 0) n = -n;

                var probe = Spawn(model, grp, Vector3.zero, Quaternion.identity);
                var pb = MeshBounds(probe.transform);
                bool longX = pb.size.x >= pb.size.z;
                Vector3 L = longX ? new Vector3(Mathf.Sign(pb.center.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(pb.center.z));
                float modLen = longX ? pb.size.x : pb.size.z, modW = longX ? pb.size.z : pb.size.x;
                Undo.DestroyObjectImmediate(probe);

                int count = Mathf.Max(1, Mathf.CeilToInt(len / 2f));
                float seg = len / count;
                Quaternion rot = Quaternion.LookRotation(d, n) * Quaternion.Inverse(Quaternion.LookRotation(L, Vector3.up));
                for (int i = 0; i < count; i++)
                {
                    var m = Spawn(model, grp, Vector3.zero, Quaternion.identity);
                    m.name = Tag + "Ramp " + (i + 1);
                    m.transform.position = a + d * (seg * i);
                    m.transform.rotation = rot;
                    var ls = Vector3.one;
                    if (longX) { ls.x = seg / modLen; ls.z = width / modW; } else { ls.z = seg / modLen; ls.x = width / modW; }
                    m.transform.localScale = ls;
                    Record(m.transform);
                    FixMaterials(m);
                    foreach (var mf in m.GetComponentsInChildren<MeshFilter>())
                        if (mf.GetComponent<Collider>() == null) Undo.AddComponent<BoxCollider>(mf.gameObject);   // 걷는 판정
                }
                foreach (var st in steps) Undo.DestroyObjectImmediate(st.gameObject);
                log.Add("계단: " + gname + " → 나무 경사로 " + count + "조각 (길이 " + len.ToString("0.0") + "m, 폭 " + width.ToString("0.00") + "m), Step " + steps.Count + "개 삭제");
            }
        }

        // ================================================================== 정비 풍차 → 줄감개
        static GameObject Drum(Transform root, List<string> log)
        {
            var old = FindUnder(root, "Service windmill spindle");
            var model = FindModel("EF_A2_Drum");
            if (old == null || model == null) { log.Add("줄감개: " + (old == null ? "Service windmill spindle 없음" : "EF_A2_Drum 모델 없음") + " → 건너뜀"); return null; }
            if (IsAsset(old)) { log.Add("줄감개: 이미 대체됨"); return old.gameObject; }

            var blades = FindUnder(old, "Existing scaled windmill blades");
            float targetD = 2.4f;
            if (blades != null) { var bb = BoundsIn(old, blades); targetD = Mathf.Max(bb.size.x, bb.size.y); }

            var inst = Spawn(model, old.parent, old.localPosition, old.localRotation);
            var b = MeshBounds(inst.transform);
            // 통의 긴 축 → 장치의 Z (EF1123Motion 이 Z로 돌림)
            Quaternion fix = Quaternion.identity;
            if (b.size.x >= b.size.y && b.size.x >= b.size.z) fix = Quaternion.Euler(0, 90, 0);
            else if (b.size.y >= b.size.x && b.size.y >= b.size.z) fix = Quaternion.Euler(90, 0, 0);
            inst.transform.localRotation = old.localRotation * fix;
            Vector3 dsz = RotSize(fix, b.size);
            float k = targetD / Mathf.Max(Mathf.Max(dsz.x, dsz.y), 0.01f);
            inst.transform.localScale = Vector3.one * k;
            Recenter(inst.transform, old.position);

            TakeOver(old, inst, fix, log, keepChildren: new string[0]);
            log.Add("줄감개: Service windmill spindle → EF_A2_Drum (지름 " + targetD.ToString("0.0") + "m). 받침 기둥은 그대로");
            return inst;
        }

        // ================================================================== 도개교 → A2 다리
        static GameObject Bridge(Transform root, List<string> log)
        {
            var old = FindUnder(root, "Opened by the windmill rope");
            var model = FindModel("EF_A2_Bridge");
            if (old == null || model == null) { log.Add("도개교: " + (old == null ? "Opened by the windmill rope 없음" : "EF_A2_Bridge 모델 없음") + " → 건너뜀"); return null; }
            if (IsAsset(old)) { log.Add("도개교: 이미 대체됨"); return old.gameObject; }

            var col = FindUnder(old, "Bridge planks collision");
            if (col == null) { log.Add("도개교: Bridge planks collision 없음 → 건너뜀"); return null; }
            Vector3 cpos = col.localPosition, csz = col.localScale;
            int longAx = MaxAxis(csz), thinAx = MinAxis(csz), wideAx = 3 - longAx - thinAx;
            Vector3 Lw = Axis(longAx) * (cpos[longAx] < 0 ? -1f : 1f);
            Vector3 Up = Axis(thinAx);
            var mo = old.GetComponent<EF1123Motion>();
            Quaternion openLocal = old.localRotation * Quaternion.Euler(mo != null ? mo.turn : Vector3.zero);
            Quaternion parentRot = old.parent != null ? old.parent.rotation : Quaternion.identity;
            if (Vector3.Dot(parentRot * openLocal * Up, Vector3.up) < 0) Up = -Up;
            float farEnd = Mathf.Abs(cpos[longAx]) + csz[longAx] / 2f, width = csz[wideAx];

            var inst = Spawn(model, old.parent, old.localPosition, old.localRotation);
            var b = MeshBounds(inst.transform);
            bool mLongX = b.size.x >= b.size.z;
            Vector3 mL = mLongX ? new Vector3(Mathf.Sign(b.center.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(b.center.z));
            float mFar = mLongX ? Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)) : Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z));
            float mW = mLongX ? b.size.z : b.size.x;
            Quaternion fix = Quaternion.LookRotation(Lw, Up) * Quaternion.Inverse(Quaternion.LookRotation(mL, Vector3.up));
            inst.transform.localRotation = old.localRotation * fix;
            var ls = Vector3.one;
            if (mLongX) { ls.x = farEnd / mFar; ls.z = width / mW; } else { ls.z = farEnd / mFar; ls.x = width / mW; }
            inst.transform.localScale = ls;
            inst.transform.localPosition = old.localPosition;          // 피벗 = 경첩 축
            Record(inst.transform);

            // 고정 경첩대 (열린 자세 기준, 움직이지 않음)
            var hingeModel = FindModel("EF_A2_BridgeHinge");
            if (hingeModel != null)
            {
                var hinge = Spawn(hingeModel, old.parent, old.localPosition, Quaternion.identity);
                hinge.name = Tag + "BridgeHinge";
                hinge.transform.rotation = parentRot * openLocal * fix;
                var hs = Vector3.one; if (mLongX) hs.z = width / mW; else hs.x = width / mW;
                hinge.transform.localScale = hs;
                Record(hinge.transform);
                FixMaterials(hinge);
            }

            TakeOver(old, inst, fix, log, keepChildren: new[] { "Bridge planks collision" });
            var keptCol = FindUnder(inst.transform, "Bridge planks collision");
            if (keptCol != null) HideRenderer(keptCol);
            log.Add("도개교: Opened by the windmill rope → EF_A2_Bridge (길이 " + farEnd.ToString("0.0") + "m, 폭 " + width.ToString("0.0") + "m)" + (hingeModel != null ? " + 고정 경첩대" : ""));
            return inst;
        }

        // ================================================================== 줄 2가닥
        static void Ropes(Transform root, GameObject drum, GameObject bridge, List<string> log)
        {
            if (drum == null || bridge == null) { log.Add("줄: 줄감개나 다리가 없어 건너뜀"); return; }
            var ropeType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("EverflowMap.EFRopeLink")).FirstOrDefault(t => t != null);
            if (ropeType == null) { log.Add("줄: EFRopeLink.cs 가 없어 건너뜀"); return; }
            if (root.Find(Tag + "Rope A") != null) { log.Add("줄: 이미 있음"); return; }
            var spools = drum.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("RopeSpool")).ToList();
            var ties = bridge.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("RopeTie")).ToList();
            if (spools.Count < 2 || ties.Count < 2) { log.Add("줄: RopeSpool/RopeTie 가 FBX에 없어 건너뜀 (블렌더 스크립트 EXPORT_DIR 로 내보내면 생김)"); return; }

            var t0 = ties[0]; var t1 = ties[1];
            if (Vector3.Distance(spools[0].position, t1.position) + Vector3.Distance(spools[1].position, t0.position) <
                Vector3.Distance(spools[0].position, t0.position) + Vector3.Distance(spools[1].position, t1.position))
            { var tmp = t0; t0 = t1; t1 = tmp; }

            var ropeMat = RopeMaterial();
            float radius = 0.42f * drum.transform.lossyScale.x;
            var pairs = new[] { (spools[0], t0, "A"), (spools[1], t1, "B") };
            foreach (var p in pairs)
            {
                var go = new GameObject(Tag + "Rope " + p.Item3);
                Undo.RegisterCreatedObjectUndo(go, "rope");
                go.transform.SetParent(root, false);
                var comp = go.AddComponent(ropeType);
                var so = new SerializedObject(comp);
                so.FindProperty("spool").objectReferenceValue = p.Item1;
                so.FindProperty("target").objectReferenceValue = p.Item2;
                so.FindProperty("spoolRadius").floatValue = radius;
                so.FindProperty("width").floatValue = 0.05f;
                so.ApplyModifiedPropertiesWithoutUndo();
                var lr = go.GetComponent<LineRenderer>();
                if (lr != null && ropeMat != null) lr.sharedMaterial = ropeMat;
            }
            log.Add("줄: 줄감개 ↔ 도개교 줄 2가닥 연결");
        }

        // ================================================================== 대체의 핵심
        // old 의 퍼즐 스크립트·콜라이더를 inst 로 옮기고, 씬의 모든 참조를 inst 로 바꾸고, old 를 지운다.
        static void TakeOver(Transform old, GameObject inst, Quaternion fix, List<string> log, string[] keepChildren)
        {
            var map = new Dictionary<UnityEngine.Object, UnityEngine.Object>
            {
                { old.gameObject, inst }, { old, inst.transform }
            };

            foreach (var c in old.GetComponents<Component>())
            {
                if (c is Transform || c is MeshFilter || c is MeshRenderer) continue;
                if (!UnityEditorInternal.ComponentUtility.CopyComponent(c)) continue;
                if (!UnityEditorInternal.ComponentUtility.PasteComponentAsNew(inst)) continue;
                var copy = inst.GetComponents(c.GetType()).Last();
                map[c] = copy;

                if (c is EF1123Motion om && copy is EF1123Motion nm)
                {
                    // 회전 장치는 '같은 실제 축'으로 돌도록 turn/sway 를 새 로컬 축으로 바꿈
                    Undo.RecordObject(nm, "motion");
                    nm.turn = ReAxis(om.turn, fix);
                    nm.sway = ReAxis(om.sway, fix);
                }
                if (c is BoxCollider ob && copy is BoxCollider nb)
                {
                    Undo.RecordObject(nb, "col");
                    Vector3 wc = old.TransformPoint(ob.center);
                    nb.center = inst.transform.InverseTransformPoint(wc);
                    nb.size = WorldBoxToLocal(old, ob.size, inst.transform);
                }
            }

            foreach (var name in keepChildren)
            {
                var ch = FindUnder(old, name);
                if (ch != null) Undo.SetTransformParent(ch, inst.transform, "keep child");
            }

            int changed = RemapSceneReferences(old.gameObject.scene, map);
            string oldName = old.name;
            Undo.DestroyObjectImmediate(old.gameObject);
            inst.name = oldName;
            FixMaterials(inst);
            log.Add("  └ 연결 " + changed + "곳을 새 오브젝트로 바꿈");
        }

        static Vector3 ReAxis(Vector3 euler, Quaternion fix)
        {
            if (euler == Vector3.zero) return euler;
            // 한 축만 쓰는 회전(예: Z 1080)이라고 보고, 그 축을 새 로컬 좌표로 옮김
            Vector3 axis = euler.normalized; float amount = euler.magnitude;
            Vector3 na = Quaternion.Inverse(fix) * axis;
            na = new Vector3(Mathf.Round(na.x), Mathf.Round(na.y), Mathf.Round(na.z));
            return na * amount;
        }

        static Vector3 WorldBoxToLocal(Transform from, Vector3 size, Transform to)
        {
            Vector3[] ax = { from.right, from.up, from.forward };
            Vector3 ws = Vector3.Scale(size, from.lossyScale);
            Vector3[] tx = { to.right, to.up, to.forward };
            Vector3 r = Vector3.zero;
            for (int i = 0; i < 3; i++)
            {
                float s = 0; for (int j = 0; j < 3; j++) s += Mathf.Abs(Vector3.Dot(tx[i], ax[j])) * Mathf.Abs(ws[j]);
                r[i] = s / Mathf.Max(Mathf.Abs(to.lossyScale[i]), 1e-4f);
            }
            return r;
        }

        static int RemapSceneReferences(UnityEngine.SceneManagement.Scene scene, Dictionary<UnityEngine.Object, UnityEngine.Object> map)
        {
            int count = 0;
            foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null || mb.gameObject.scene != scene) continue;
                if (map.ContainsKey(mb.gameObject)) continue;                 // 곧 지울 옛 오브젝트
                var so = new SerializedObject(mb);
                var it = so.GetIterator(); bool dirty = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    if (it.name == "m_GameObject" || it.name == "m_Script") continue;
                    if (map.TryGetValue(it.objectReferenceValue, out var rep) && !map.ContainsValue(mb))
                    {
                        it.objectReferenceValue = rep; dirty = true; count++;
                    }
                }
                if (dirty) so.ApplyModifiedProperties();
            }
            return count;
        }

        // ================================================================== 도우미
        static bool IsAsset(Transform t) => PrefabUtility.IsPartOfPrefabInstance(t.gameObject);

        static Transform FindInScene(string name) =>
            UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == name);

        static Transform FindUnder(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        static GameObject FindModel(string prefix)
        {
            var paths = AssetDatabase.FindAssets(prefix + " t:Model")
                .Concat(AssetDatabase.FindAssets(prefix + " t:Prefab"))
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .Where(p =>
                {
                    string f = Path.GetFileNameWithoutExtension(p);
                    if (!f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
                    string rest = f.Substring(prefix.Length);          // EF_A2_Bridge 가 EF_A2_BridgeHinge 를 잡지 않게
                    return rest.Length == 0 || rest[0] == '.' || rest[0] == ' ' || char.IsDigit(rest[0]);
                })
                .OrderByDescending(p => File.GetLastWriteTime(p)).ToList();
            return paths.Count == 0 ? null : AssetDatabase.LoadAssetAtPath<GameObject>(paths[0]);
        }

        static GameObject Spawn(GameObject model, Transform parent, Vector3 localPos, Quaternion localRot)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            Undo.RegisterCreatedObjectUndo(inst, "place");
            Undo.RecordObject(inst.transform, "place");
            inst.transform.localPosition = localPos;
            inst.transform.localRotation = localRot;
            inst.transform.localScale = Vector3.one;
            return inst;
        }

        static void Record(Transform t)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }

        static void HideRenderer(Transform t)
        {
            var r = t.GetComponent<Renderer>();
            if (r == null || !r.enabled) return;
            Undo.RecordObject(r, "hide"); r.enabled = false;
        }

        // 메시 범위: 자기 로컬 좌표 (자기 회전·스케일 제외)
        static Bounds MeshBounds(Transform inst) => BoundsIn(inst, inst);

        // 회전(90도 단위)한 상자의 축별 크기
        static Vector3 RotSize(Quaternion q, Vector3 size)
        {
            Vector3 r = Vector3.zero;
            Vector3[] ax = { q * Vector3.right, q * Vector3.up, q * Vector3.forward };
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++) r[i] += Mathf.Abs(ax[j][i]) * size[j];
            return r;
        }

        static Bounds BoundsIn(Transform space, Transform under)
        {
            bool has = false; var b = new Bounds();
            foreach (var mf in under.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var m = space.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, Corner(i)));
                    if (!has) { b = new Bounds(p, Vector3.zero); has = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        static Vector3 Corner(int i) => new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);

        // 메시 중심이 target(월드) 에 오도록 옮김
        static void Recenter(Transform inst, Vector3 targetWorld)
        {
            var b = BoundsIn(inst, inst);
            Vector3 centerWorld = inst.TransformPoint(b.center);
            inst.position += targetWorld - centerWorld;
            Record(inst);
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        static int MaxAxis(Vector3 v) => v.x >= v.y && v.x >= v.z ? 0 : (v.y >= v.z ? 1 : 2);
        static int MinAxis(Vector3 v) => v.x <= v.y && v.x <= v.z ? 0 : (v.y <= v.z ? 1 : 2);
        static Vector3 Axis(int i) => i == 0 ? Vector3.right : (i == 1 ? Vector3.up : Vector3.forward);

        // ------------------------------------------------------------------ 머티리얼
        static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

        static Material FEMaterial(string name)
        {
            if (matCache.TryGetValue(name, out var m) && m != null) return m;
            var path = AssetDatabase.FindAssets(name + " t:Material").Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p).Equals(name, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.Contains("FantasyEnvironments")).FirstOrDefault();
            m = path != null ? AssetDatabase.LoadAssetAtPath<Material>(path) : null;
            matCache[name] = m;
            return m;
        }

        static Material RopeMaterial()
        {
            var m = FEMaterial("EF_Rope");
            if (m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(shader) { name = "EF_Rope" };
            var c = new Color(0.43f, 0.31f, 0.18f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            Directory.CreateDirectory("Assets/EverflowMap/Materials");
            AssetDatabase.CreateAsset(m, "Assets/EverflowMap/Materials/EF_Rope.mat");
            matCache["EF_Rope"] = m;
            return m;
        }

        static void FixMaterials(GameObject inst)
        {
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (r is LineRenderer) continue;
                var mats = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    if (AssetDatabase.GetAssetPath(mats[i]).EndsWith(".mat")) continue;      // 이미 리맵됨
                    string baseName = mats[i].name.Split('.')[0].Trim();
                    var fe = baseName == "EF_Rope" ? RopeMaterial() : FEMaterial(baseName);
                    if (fe != null) { mats[i] = fe; changed = true; }
                }
                if (changed) { Undo.RecordObject(r, "mat"); r.sharedMaterials = mats; }
            }
        }
    }
}
#endif
