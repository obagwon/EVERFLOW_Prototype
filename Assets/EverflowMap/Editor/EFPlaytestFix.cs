#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EverflowMap.EditorTools
{
    // 플레이 점검에서 나온 문제 한 번에 고치기
    // 메뉴: Tools > EVERFLOW > 플레이 점검 고치기
    // (W 는 W 고치기 메뉴를 따로 자동 실행. 나머지는 Ctrl+Z 한 번으로 되돌림)
    public static class EFPlaytestFix
    {
        const string Dir = "Assets/EverflowMap/PlaytestFix";
        static readonly Vector3 WindmillOriginal = new Vector3(52.14f, 38.34f, 91.39f);
        static readonly Vector3 FinishOriginal = new Vector3(54.24f, 38.22f, 86.73f);

        [MenuItem("Tools/EVERFLOW/플레이 점검 고치기")]
        static void Run()
        {
            if (!EditorUtility.DisplayDialog("플레이 점검 고치기", "씬을 먼저 저장(Ctrl+S)해 두셨나요?", "진행", "취소")) return;
            var log = new List<string>();
            var world = Object.FindObjectsByType<EF1123World>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (world == null) { EditorUtility.DisplayDialog("점검", "EF1123World 없음", "확인"); return; }

            // 0) W: 마커 지우고 W 고치기 (아직 안 했으면)
            var marker = Find("W_Sluice_Target");
            if (marker != null) { Undo.DestroyObjectImmediate(marker.gameObject); log.Add("W: 땅속에 있던 W_Sluice_Target 마커 지움"); }
            if (Find("W02 Operator deck") == null)
            {
                var fixW = System.Type.GetType("EverflowMap.EditorTools.EFFixW");
                var run = fixW?.GetMethod("Run", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (run != null) { run.Invoke(null, null); log.Add("W: W 고치기 실행 (조작 발판·체크포인트·도개교 FREEZE·D 압력발판)"); }
                else log.Add("W: EFFixW.cs 가 없어 W 고치기를 못 함");
            }

            Undo.IncrementCurrentGroup(); int g = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("플레이 점검 고치기");
            try
            {
                BFirstStream(world, log);
                BLevel(log);
                RoundWater(world, log);
                Withered(log);
                Stations(log);
                Colliders(log);
                Plate(world, log);
                ArrivedSeed(log);
                LensWindow(log);
                Windmill(world, log);
                Steep(world, log);
            }
            catch (System.Exception e) { Debug.LogException(e); log.Add("오류: " + e.Message); }
            Undo.CollapseUndoOperations(g);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("[플레이 점검 고치기]\n" + string.Join("\n", log));
            EditorUtility.DisplayDialog("플레이 점검 고치기", string.Join("\n", log), "확인");
        }

        // ---------------------------------------------------------------- 1. B 첫 물줄기: 물받이 전엔 수조 밖으로 떨어짐
        static void BFirstStream(EF1123World world, List<string> log)
        {
            var chute = Find("B catchment source");
            var t = Target("잎 물받이");
            if (chute == null || t == null) { log.Add("1) B catchment source / 잎 물받이 없음"); return; }
            var leaf = Find("Leaf catchment");
            Vector3 f = chute.forward * (chute.lossyScale.z * .5f);
            Vector3 e1 = chute.position + f, e2 = chute.position - f;
            Vector3 spout = leaf != null && Vector3.Distance(e1, leaf.position) > Vector3.Distance(e2, leaf.position) ? e1 : e2;
            var old = Find("B spout fall"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            float ground = Ground(spout, chute);
            float h = Mathf.Max(.5f, spout.y - ground);
            var go = new GameObject("B spout fall"); Undo.RegisterCreatedObjectUndo(go, "fall");
            go.transform.SetParent(chute.parent, true);
            go.transform.position = new Vector3(spout.x, spout.y - h * .5f, spout.z);
            go.transform.rotation = Quaternion.LookRotation(Flat(chute.forward), Vector3.up);   // 물줄기 폭 = 물받이 폭 방향
            var mesh = FallMesh(new Vector3(Mathf.Max(.35f, chute.lossyScale.x * 1.2f), h, .1f));
            Save(mesh, "B_spout_fall");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = WaterMat("EF_FallWater", 0);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            AddWater(world, mr);
            // 시작: 떨어지는 물 O, 물받이로 가는 물 X  → 잎 물받이 BLOOM: 반대로
            Undo.RecordObject(chute.gameObject, "off"); chute.gameObject.SetActive(false);
            var so = new SerializedObject(t);
            AddTo(so.FindProperty("reveal"), chute.gameObject);
            AddTo(so.FindProperty("hide"), go);
            so.ApplyModifiedProperties();
            log.Add("1) B: 처음엔 물이 기둥에서 수조 밖으로 곧장 떨어짐 → 잎 물받이 BLOOM 하면 물받이로 흘러 수조로");
        }

        // ---------------------------------------------------------------- 2. B 수위
        static void BLevel(List<string> log)
        {
            var fill = Find("B fill level");
            if (fill == null) { log.Add("2) B fill level 없음"); return; }
            var tank = Find("B_Watertank");
            float top = 24.2f;
            if (tank != null)
            {
                var walls = tank.Cast<Transform>().Where(w => w.name.StartsWith("B_Wall")).ToList();
                if (walls.Count > 0) top = walls.Max(w => w.position.y + Mathf.Abs(w.lossyScale.y) * .5f);
            }
            var m = fill.GetComponent<EF1123Motion>();
            float rise = m != null && fill.parent != null ? fill.parent.TransformVector(m.offset).y : 0;
            float want = top - .55f - rise;                     // 다 찼을 때 벽 위에서 0.55m 아래
            if (fill.position.y > want)
            {
                Undo.RecordObject(fill, "level"); fill.position = new Vector3(fill.position.x, want, fill.position.z);
                log.Add("2) B 수조 물높이 낮춤 (다 차도 벽 윗면보다 0.55m 아래)");
            }
            else log.Add("2) B 수조 물높이는 이미 낮음");
        }

        // ---------------------------------------------------------------- 4. 네모 물 → 가장자리 부드러운 둥근 물
        static void RoundWater(EF1123World world, List<string> log)
        {
            int n = 0;
            foreach (var r in world.water)
            {
                if (r == null) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                string mn = mf.sharedMesh.name;
                if (mn != "Cube" && mn != "Plane") continue;
                Vector3 s = Abs(r.transform.lossyScale);
                if (mn == "Plane") s = new Vector3(s.x * 10, s.y, s.z * 10);
                bool pool = Mathf.Min(s.x, s.z) > 1.5f;
                var mesh = SurfaceMesh(s, pool, mn == "Cube" ? .5f : 0, mn == "Plane" ? 10 : 1);
                Save(mesh, "Water_" + r.name);
                Undo.RecordObject(mf, "water"); mf.sharedMesh = mesh;
                n++;
            }
            log.Add("4) 네모난 물 " + n + "개 → 둥글고 가장자리가 부드러운 물 (호수는 타원, 물길은 끝이 부드럽게)");
        }

        // ---------------------------------------------------------------- 5·6. 처음엔 시든 색
        static void Withered(List<string> log)
        {
            int n = 0, skip = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("Leaf_Asset_") || t.GetComponentInParent<EFBloomTint>() != null) continue;
                if (t.name.Contains("Seed") || t.name.Contains("씨앗") || t.name.Contains("Pile") || t.name == "Leaf_Asset_Visual") continue;
                var src = t.GetComponentInParent<EF1123Motion>();
                if (src == null) { skip++; continue; }
                var tint = Undo.AddComponent<EFBloomTint>(t.gameObject);
                tint.source = src; tint.seconds = 1.6f;
                tint.withered = new Color(.56f, .5f, .34f); tint.grown = Color.white;
                n++;
            }
            log.Add("5·6) 연잎·잎 " + n + "개: 처음엔 칙칙한 색 → 그 장치가 움직이면(물이 차거나 BLOOM·FLOW·FREEZE) 초록" + (skip > 0 ? " (장치 없는 " + skip + "개는 그대로)" : ""));
        }

        // ---------------------------------------------------------------- 7~9. 조작 발판: 땅에 붙이고 빛나는 원 표시
        static void Stations(List<string> log)
        {
            var mat = RingMat();
            int n = 0, snapped = 0;
            var done = new HashSet<Transform>();
            foreach (var t in Object.FindObjectsByType<EF1123Target>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.station == null || !done.Add(t.station)) continue;
                var st = t.station;
                if (t.title == "잎 나룻배" && t.stationRadius < 2.2f) { Undo.RecordObject(t, "r"); t.stationRadius = 2.2f; }
                // 땅·발판에 붙이기
                var hits = Physics.RaycastAll(st.position + Vector3.up * .9f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => h.collider.GetComponentInParent<EFMapPlayer>() == null && h.collider.GetComponentInParent<EF1123Target>() == null)
                    .OrderBy(h => h.distance).ToList();
                if (hits.Count > 0 && Mathf.Abs(hits[0].point.y - st.position.y) > .25f)
                { Undo.RecordObject(st, "snap"); st.position = new Vector3(st.position.x, hits[0].point.y, st.position.z); snapped++; }
                // 원 표시
                var old = st.Find("Station Ring"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
                var go = new GameObject("Station Ring"); Undo.RegisterCreatedObjectUndo(go, "ring");
                go.transform.SetParent(st, false);
                var ls = st.lossyScale; go.transform.localScale = new Vector3(1 / Mathf.Max(ls.x, .001f), 1 / Mathf.Max(ls.y, .001f), 1 / Mathf.Max(ls.z, .001f));
                go.transform.localPosition = Vector3.zero; go.transform.rotation = Quaternion.identity;
                go.transform.position = st.position + Vector3.up * .04f;
                var mesh = Ring(Mathf.Min(t.stationRadius, 2.6f));
                Save(mesh, "Ring_" + st.name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                n++;
            }
            log.Add("7~9) 조작 발판 " + n + "곳에 빛나는 원 표시, " + snapped + "곳은 높이를 땅·발판에 붙임. C 나룻배 발판 범위 1.3 → 2.2m");
        }

        // ---------------------------------------------------------------- 10~12. 모델 모양대로 충돌
        static readonly string[] Solid = { "EF_WoodFlat", "EF_WoodFlatUp", "EF_WoodStairs", "EF_WoodWalk", "EF_E_Bench", "EF_E_Slab", "EF_E_Ledge",
                                           "EF_C_Bridge", "EF_LotusBig", "EF_LotusStem", "EF_Lotus", "E_HighShelf_Rock3", "EF_S_TowerStep" };
        static void Colliders(List<string> log)
        {
            int n = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var root = PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject);
                if (root != t.gameObject) continue;
                var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject);
                string key = src != null ? src.name : t.name;
                if (!Solid.Any(s => key == s || t.name == s || t.name.StartsWith(s + " ") || key.StartsWith(s + "."))) continue;
                foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                    var mc = Undo.AddComponent<MeshCollider>(mf.gameObject); mc.sharedMesh = mf.sharedMesh; n++;
                }
            }
            log.Add("10~12) 나무 길·계단, E 돌다리, Rock3, 연잎, C 다리 등 " + n + "개에 모양대로 충돌 넣음 (이제 올라설 수 있음)");
        }

        // ---------------------------------------------------------------- 13. D 압력발판
        static void Plate(EF1123World world, List<string> log)
        {
            var plate = Find("D hidden pressure plate");
            if (plate == null) { log.Add("13) D 압력발판이 없음 (W 고치기가 안 됐을 수 있음)"); return; }
            var p = plate.GetComponent<EFPressurePlate>() ?? Undo.AddComponent<EFPressurePlate>(plate.gameObject);
            p.world = world;
            var pollen = Find("D dancing pollen"); if (pollen != null) p.cover = pollen.gameObject;
            log.Add("13) D 압력발판: 낙엽이 치워진 뒤 밟으면 눌림 (여는 장치는 EFPressurePlate 의 Opens 에 넣기)");
        }

        // ---------------------------------------------------------------- 14. 도착한 씨앗 조준 구체 되돌리기
        static void ArrivedSeed(List<string> log)
        {
            var t = Target("도착한 씨앗");
            if (t == null) { log.Add("14) 도착한 씨앗 없음"); return; }
            var vis = t.transform.Find("Leaf_Asset_Visual");
            if (vis != null) Undo.DestroyObjectImmediate(vis.gameObject);
            foreach (var r in t.GetComponents<Renderer>()) { Undo.RecordObject(r, "on"); r.enabled = true; }
            log.Add("14) D '도착한 씨앗' 조준 구체를 원래 구체로 되돌림");
        }

        // ---------------------------------------------------------------- 15. 렌즈방 창문을 꽃 점프대 쪽으로
        static void LensWindow(List<string> log)
        {
            var west = Find("West | Flower and lens");
            var floor = west != null ? Under(west, "F window and lens chamber floor") : null;
            var lintel = west != null ? Under(west, "F window lintel") : null;
            var pad = west != null ? Under(west, "F01 Flower pad") : null;
            if (floor == null || lintel == null || pad == null) { log.Add("15) 렌즈방/꽃 점프대를 못 찾음"); return; }
            Vector3 win = Flat(lintel.position - floor.position), want = Flat(pad.position - floor.position);
            float ang = Vector3.SignedAngle(win, want, Vector3.up);
            if (Mathf.Abs(ang) < 5) { log.Add("15) 렌즈방 창문이 이미 꽃 쪽을 향함"); return; }
            var skip = new HashSet<string> { "F01 Flower pad", "Flower pad foundation", "Flower pad growth", "도약 꽃", "Trail sign",
                                             "West approach from upper road", "West trail to flower" };
            Vector3 pivot = floor.position;
            foreach (var c in west.Cast<Transform>().Where(q => !skip.Contains(q.name)).ToList())
            { Undo.RecordObject(c, "rot"); c.RotateAround(pivot, Vector3.up, ang); }
            log.Add("15) 렌즈방을 " + ang.ToString("0") + "° 돌려 창문이 꽃 점프대를 보게 함 → 꽃에서 날아갈 때 벽에 부딪혀 되돌아가던 문제");
        }

        // ---------------------------------------------------------------- 17. 풍차 입구 장치를 풍차 따라
        static void Windmill(EF1123World world, List<string> log)
        {
            var mill = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "Windmill" && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject));
            var g = Find("G | Windmill finish");
            if (mill == null || g == null) { log.Add("17) 풍차 / G | Windmill finish 없음"); return; }
            Vector3 d = mill.position - WindmillOriginal;
            Undo.RecordObject(g, "g"); g.position = d;
            Undo.RecordObject(world, "finish"); world.finishCenter = FinishOriginal + d;
            log.Add("17) 풍차 입구 문·계단·벽 충돌·도착 지점을 풍차 위치에 맞춤 (" + d.x.ToString("0.0") + ", " + d.y.ToString("0.0") + ", " + d.z.ToString("0.0") + ")");
        }

        // ---------------------------------------------------------------- 18. 벽타기 막기
        static void Steep(EF1123World world, List<string> log)
        {
            if (world.player == null) { log.Add("18) 플레이어 없음"); return; }
            if (world.player.GetComponent<EFSteepGuard>() == null) Undo.AddComponent<EFSteepGuard>(world.player.gameObject);
            log.Add("18) 벽타기 막기: 44° 넘는 경사에선 점프 불가 + 미끄러짐 (EFSteepGuard 의 Max Slope 로 조절)");
        }

        // ================================================================ 메시·재질
        static Mesh SurfaceMesh(Vector3 s, bool pool, float y, float unit)
        {
            int nx = pool ? 18 : 4, nz = pool ? 18 : Mathf.Clamp(Mathf.CeilToInt(s.z / .5f), 4, 24);
            float fade = pool ? 0 : Mathf.Min(.3f, Mathf.Min(s.x, s.z) * .3f);
            var v = new List<Vector3>(); var c = new List<Color>(); var uv = new List<Vector2>(); var uv2 = new List<Vector2>(); var tr = new List<int>();
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float u = i / (float)nx * 2 - 1, w = j / (float)nz * 2 - 1;
                    float a;
                    if (pool) { float rr = Mathf.Sqrt(u * u + w * w) / 1.1f; a = 1 - Mathf.SmoothStep(0, 1, (rr - .8f) / .2f); }
                    else
                    {
                        float ex = (1 - Mathf.Abs(u)) * s.x * .5f, ez = (1 - Mathf.Abs(w)) * s.z * .5f;
                        a = Mathf.SmoothStep(0, 1, Mathf.Min(ex, ez) / Mathf.Max(fade, .01f));
                    }
                    v.Add(new Vector3(u * .5f * unit, y, w * .5f * unit));
                    c.Add(new Color(0, 0, 0, a));
                    uv.Add(new Vector2((u * .5f + .5f), (w * .5f + .5f) * s.z / Mathf.Max(s.x, .01f)));
                    uv2.Add(new Vector2(0, pool ? .15f : 0));
                }
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++) { int k = j * (nx + 1) + i; tr.AddRange(new[] { k, k + nx + 1, k + 1, k + 1, k + nx + 1, k + nx + 2 }); }
            return Build(v, c, uv, uv2, tr, pool ? "Pool" : "Stream");
        }

        static Mesh FallMesh(Vector3 s)
        {
            float W = s.x, H = s.y; int nu = 4, nv = Mathf.Max(8, Mathf.CeilToInt(H * 2));
            var v = new List<Vector3>(); var c = new List<Color>(); var uv = new List<Vector2>(); var uv2 = new List<Vector2>(); var t = new List<int>();
            for (int j = 0; j <= nv; j++)
                for (int i = 0; i <= nu; i++)
                {
                    float u = i / (float)nu * 2 - 1, k = j / (float)nv;
                    v.Add(new Vector3(u * W * .5f, H * .5f - k * H, Mathf.Sin(k * Mathf.PI) * .08f));
                    c.Add(new Color(1, 0, 0, 1 - Mathf.SmoothStep(0, 1, (Mathf.Abs(u) - .7f) / .3f)));
                    uv.Add(new Vector2(u * W * .25f, k * H * .5f)); uv2.Add(new Vector2(k, 0));
                }
            for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++) { int a = j * (nu + 1) + i; t.AddRange(new[] { a, a + 1, a + nu + 1, a + 1, a + nu + 2, a + nu + 1 }); }
            return Build(v, c, uv, uv2, t, "Fall");
        }

        static Mesh Ring(float r)
        {
            int n = 48; float w = .07f;
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * (r - w)); v.Add(d * (r + w));
            }
            for (int i = 0; i < n; i++) { int k = i * 2; t.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 }); }
            var m = new Mesh { name = "EF_StationRing" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static Mesh Build(List<Vector3> v, List<Color> c, List<Vector2> uv, List<Vector2> uv2, List<int> t, string name)
        {
            var m = new Mesh { name = "EF_" + name + "Water" };
            m.SetVertices(v); m.SetColors(c); m.SetUVs(0, uv); m.SetUVs(1, uv2); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        static void Save(Mesh m, string name)
        {
            Directory.CreateDirectory(Dir);
            string safe = string.Concat(name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) || ch == ' ' ? '_' : ch));
            string path = Dir + "/" + safe + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
        }

        static Material WaterMat(string name, float worldUV)
        {
            string path = Dir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var src = AssetDatabase.FindAssets("ContinuousWater t:Material").Select(AssetDatabase.GUIDToAssetPath).Select(p => AssetDatabase.LoadAssetAtPath<Material>(p)).FirstOrDefault();
            if (src == null) return null;
            Directory.CreateDirectory(Dir);
            m = new Material(src) { name = name };
            m.SetFloat("_WorldUV", worldUV); m.SetFloat("_Wet", 1); m.SetFloat("_Arrival", 1);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Material RingMat()
        {
            string path = Dir + "/EF_StationRing.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            Directory.CreateDirectory(Dir);
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color")) { name = "EF_StationRing" };
            var col = new Color(.55f, 1f, .85f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void AddWater(EF1123World world, Renderer r)
        {
            var so = new SerializedObject(world); var wp = so.FindProperty("water");
            for (int i = 0; i < wp.arraySize; i++) if (wp.GetArrayElementAtIndex(i).objectReferenceValue == r) return;
            wp.arraySize++; wp.GetArrayElementAtIndex(wp.arraySize - 1).objectReferenceValue = r;
            so.ApplyModifiedProperties();
        }

        static void AddTo(SerializedProperty arr, Object o)
        {
            for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == o) return;
            arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = o;
        }

        static float Ground(Vector3 p, Transform ignore)
        {
            var tr = Terrain.activeTerrain;
            if (tr != null) return tr.SampleHeight(p) + tr.transform.position.y;     // 수로 = 지형
            foreach (var h in Physics.RaycastAll(p + Vector3.up * .2f, Vector3.down, 30, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (!h.collider.transform.IsChildOf(ignore)) return h.point.y;
            var t = Terrain.activeTerrain;
            return t != null ? t.SampleHeight(p) + t.transform.position.y : p.y - 3;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }
        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        static Transform Find(string n) => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == n);
        static Transform Under(Transform r, string n) => r.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
        static EF1123Target Target(string title) => Object.FindObjectsByType<EF1123Target>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.title == title || t.name == title);
    }
}
#endif
