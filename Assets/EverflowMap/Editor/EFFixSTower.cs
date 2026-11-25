#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using V = EverflowMap.EditorTools.EFVinesRoots;

namespace EverflowMap.EditorTools
{
    // S 전망대 탑 고치기
    // 메뉴: Tools > EVERFLOW > S 탑 고치기 (탑 모델·넓은 계단·덩굴다리)
    //  1) 꼭대기 상판을 탑 지름만큼 작게 (계단 위를 덮지 않게)
    //  2) 나선 계단 1~47 발판을 넓게 (벽→바깥 2.2m → 3.2m, 폭 1.3 → 1.7m)
    //  3) EF_S_Tower 모델을 회색 탑 자리에, 난간 트임을 1번 계단 쪽으로. 계단 발판도 EF_S_TowerStep 으로
    //  4) 47번 이후(48~52 자리)는 부서진 발판만 벽에 남김. 아래쪽 TWR_Broken 들도 부서진 발판 모양으로
    //  5) 47번 계단 옆 벽에 말라붙은 덩굴. '전망대 뿌리' 조준 구체를 여기로 옮기고 이름을 '마른 벽 덩굴' 로.
    //     47번 계단에 서서 BLOOM → 덩굴이 살아나며 TWR_Ka_Landing 까지 덩굴다리가 자람 (밟을 수 있음)
    //  6) 꼭대기에서 바깥으로 자라던 뿌리 계단(Growing root descent)은 이제 안 켜짐
    // Ctrl+Z 한 번이면 전부 되돌아가요.
    public static class EFFixSTower
    {
        const float StepOut = 3.2f, StepSide = 1.7f;
        const float BridgeHalfW = 1.15f, BridgeFlat = .33f, BridgeR = 6.2f;

        [MenuItem("Tools/EVERFLOW/S 탑 고치기 (탑 모델·넓은 계단·덩굴다리)")]
        public static void Run()
        {
            V.Begin("S 탑 고치기", out int group);
            var log = new List<string>();
            try { Fix(log); }
            catch (Exception e) { Debug.LogException(e); log.Add("오류: " + e.Message); }
            V.End(group, "S 탑 고치기", log);
        }

        static void Fix(List<string> log)
        {
            var shaft = V.Find("TWR_Shaft");
            var top = V.Find("TWR_Top");
            var landing = V.Find("TWR_Ka_Landing");
            if (shaft == null || top == null || landing == null) throw new Exception("TWR_Shaft / TWR_Top / TWR_Ka_Landing 을 못 찾음");
            var grp = shaft.parent;
            Vector3 axis = shaft.position;
            float R = shaft.lossyScale.x * .5f;
            float deckTop = top.position.y + top.lossyScale.y;            // 원기둥: 높이 = scale.y * 2

            var steps = grp.Cast<Transform>()
                .Where(t => t.name.StartsWith("TWR_Step") && t.gameObject.activeSelf && int.TryParse(t.name.Substring(8), out int n) && n >= 1 && n <= 47)
                .OrderBy(t => int.Parse(t.name.Substring(8))).ToList();
            if (steps.Count < 2) throw new Exception("TWR_Step01~47 을 못 찾음");

            // 1) 상판
            Undo.RecordObject(top, "top");
            top.localScale = new Vector3(shaft.localScale.x, top.localScale.y, shaft.localScale.z);
            log.Add("1) 꼭대기 상판을 탑 지름(" + (R * 2).ToString("0.#") + "m)으로 줄임");

            // 2) 계단 넓히기 (벽 쪽 끝은 그대로, 바깥으로 넓힘)
            foreach (var s in steps)
            {
                Vector3 d = Dir(s.position, axis);
                Undo.RecordObject(s, "step");
                s.localScale = new Vector3(StepOut, s.localScale.y, StepSide);
                Vector3 p = axis + d * (R + StepOut * .5f); p.y = s.position.y;
                s.position = p;
            }
            log.Add("2) 계단 " + steps.Count + "개를 바깥으로 넓힘 (3.2m)");

            // 3) 탑·계단 모델
            foreach (var n in new[] { "S_Tower_Art", "S_Tower_StepArt", "S_VineBridge", "S_VineBridge_Walk", "S_WallVine", "S vine wake", "S01 Vine step" })
                V.Remove(grp, n);
            var towerModel = FindModel("EF_S_Tower");
            var stepModel = FindModel("EF_S_TowerStep");
            var brokenModel = FindModel("EF_S_TowerStepBroken");
            if (towerModel != null)
            {
                var t = (GameObject)PrefabUtility.InstantiatePrefab(towerModel, grp);
                Undo.RegisterCreatedObjectUndo(t, "tower");
                t.name = "S_Tower_Art";
                t.transform.position = new Vector3(axis.x, deckTop - 36f, axis.z);
                t.transform.rotation = Quaternion.identity;
                var open = t.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "Opening");
                if (open != null)
                    t.transform.rotation = Quaternion.FromToRotation(Dir(open.position, t.transform.position), Dir(steps[0].position, axis));
                foreach (var mf in t.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>();
                FixMat(t);
                Hide(shaft); Hide(top);
                log.Add("3) 탑 모델 배치 (난간 트임 → 1번 계단 쪽), 회색 탑은 숨김 (충돌은 그대로)");
            }
            else log.Add("3) EF_S_Tower 모델이 없어요 → 블렌더 스크립트 EF_STower_build.py 먼저");

            var art = new GameObject("S_Tower_StepArt").transform;
            Undo.RegisterCreatedObjectUndo(art.gameObject, "art");
            art.SetParent(grp, false);
            int placed = 0;
            if (stepModel != null)
                foreach (var s in steps) { Piece(stepModel, art, s.position + Vector3.up * (s.lossyScale.y * .5f), Dir(s.position, axis), 0); Hide(s); placed++; }

            // 4) 부서진 발판: 48~52 자리 + 아래 TWR_Broken
            Transform s46 = steps[steps.Count - 2], s47 = steps[steps.Count - 1];
            float a46 = Ang(s46.position, axis), a47 = Ang(s47.position, axis);
            float da = Mathf.DeltaAngle(a46, a47), dy = s47.position.y - s46.position.y;
            float aL = Ang(landing.position, axis);
            float total = Mathf.DeltaAngle(a47, aL);
            int gaps = Mathf.Max(0, Mathf.RoundToInt(total / da) - 1);
            float y47 = s47.position.y + s47.lossyScale.y * .5f;
            float yL = landing.position.y + landing.lossyScale.y * .5f;
            if (brokenModel != null)
            {
                for (int k = 1; k <= gaps; k++)
                {
                    float a = a47 + da * k;
                    Vector3 p = axis + Polar(a) * (R + StepOut * .5f); p.y = y47 + dy * k - .6f;   // 부서져 조금 처짐
                    Piece(brokenModel, art, p, Polar(a), k * 7);
                }
                foreach (var b in grp.Cast<Transform>().Where(t => t.name.StartsWith("TWR_Broken")).ToList())
                {
                    Piece(brokenModel, art, b.position + Vector3.up * (b.lossyScale.y * .5f), Dir(b.position, axis), b.GetSiblingIndex());
                    Hide(b);
                }
            }
            log.Add("   계단 발판 모델 " + placed + "개, 47번 다음 빈자리 " + gaps + "곳은 부서진 발판");

            // 5) 덩굴
            var wake = new GameObject("S vine wake");
            Undo.RegisterCreatedObjectUndo(wake, "wake");
            wake.transform.SetParent(grp, false);
            var wm = wake.AddComponent<EF1123Motion>(); wm.seconds = 1.6f;
            var world = UnityEngine.Object.FindObjectsByType<EF1123World>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (world != null)
            {
                var wso = new SerializedObject(world);
                var mp = wso.FindProperty("motions");
                for (int i = mp.arraySize - 1; i >= 0; i--)          // 다시 돌렸을 때 지워진 예전 것 정리
                    if (mp.GetArrayElementAtIndex(i).objectReferenceValue == null) mp.DeleteArrayElementAtIndex(i);
                mp.arraySize++; mp.GetArrayElementAtIndex(mp.arraySize - 1).objectReferenceValue = wm;
                wso.ApplyModifiedProperties();
            }

            // 덩굴다리 윗면 경로 (47번 계단 → 착지대)
            var topPts = new List<Vector3>();
            int n2 = Mathf.Max(6, Mathf.CeilToInt(Mathf.Abs(total) / 8f));
            for (int i = 0; i <= n2; i++)
            {
                float t = i / (float)n2;
                float a = a47 + total * t;
                float r = Mathf.Lerp(R + StepOut * .5f, BridgeR, Mathf.Sin(t * Mathf.PI)) ;
                if (i == n2) r = Vector3.Distance(Flat(landing.position), Flat(axis));
                Vector3 p = axis + Polar(a) * r;
                p.y = Mathf.Lerp(y47, yL, Mathf.SmoothStep(0, 1, t)) + Mathf.Sin(t * Mathf.PI) * .12f;
                topPts.Add(p);
            }
            float halfH = BridgeHalfW * BridgeFlat;
            var center = topPts.Select(p => p - Vector3.up * halfH).ToList();
            float sgn = Mathf.Sign(total);
            Vector3 wallOut = axis + Polar(a47 - sgn * 10f) * (R - .2f); wallOut.y = y47 + .5f;   // 벽 속에서 나옴
            center.Insert(0, wallOut);

            var bridge = new GameObject("S_VineBridge").transform;
            Undo.RegisterCreatedObjectUndo(bridge.gameObject, "bridge");
            bridge.SetParent(grp, false); bridge.position = center[0];
            var vineMat = V.VineMat(); var leaf = V.LeafModel(); var rnd = new System.Random(3);
            V.MakeVine("Bridge vine", bridge, center, BridgeHalfW, BridgeHalfW * .85f, BridgeFlat, vineMat, wm, true, 2.2f);
            for (int k = -1; k <= 1; k += 2)
            {
                var pts = new List<Vector3>();
                for (int i = 0; i < center.Count; i++)
                {
                    Vector3 dir = Flat(center[Mathf.Min(i + 1, center.Count - 1)] - center[Mathf.Max(i - 1, 0)]).normalized;
                    Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
                    pts.Add(center[i] + side * (BridgeHalfW * k) + Vector3.up * (Mathf.Sin(i * 1.3f + k) * .14f));
                }
                var tw = V.MakeVine(k < 0 ? "Twine L" : "Twine R", bridge, pts, .17f, .06f, 1, vineMat, wm, true, 2.2f);
                tw.delay = .15f;
                V.AddLeaves(tw, leaf, 7, .55f, .12f, .95f, rnd, Vector3.zero);
            }

            // 밟는 판정 (BLOOM 하면 켜짐)
            var walk = new GameObject("S_VineBridge_Walk").transform;
            Undo.RegisterCreatedObjectUndo(walk.gameObject, "walk");
            walk.SetParent(grp, false);
            for (int i = 0; i < topPts.Count - 1; i++)
            {
                Vector3 p1 = topPts[i], p2 = topPts[i + 1];
                var c = new GameObject("Walk " + i).transform;
                c.SetParent(walk, false);
                c.position = (p1 + p2) * .5f - Vector3.up * .15f;
                c.rotation = Quaternion.LookRotation(p2 - p1, Vector3.up);
                var bc = c.gameObject.AddComponent<BoxCollider>();
                bc.size = new Vector3(BridgeHalfW * 2, .3f, Vector3.Distance(p1, p2) + .2f);
            }
            walk.gameObject.SetActive(false);

            // 벽에 말라붙은 덩굴
            var wall = new GameObject("S_WallVine").transform;
            Undo.RegisterCreatedObjectUndo(wall.gameObject, "wall");
            wall.SetParent(grp, false); wall.position = axis + Polar(a47) * R + Vector3.up * y47;
            wall.position = new Vector3(wall.position.x, y47, wall.position.z);
            for (int k = 0; k < 3; k++)
            {
                var pts = new List<Vector3>();
                float aBase = a47 - sgn * (14f - k * 12f);
                for (int j = 0; j <= 10; j++)
                {
                    float t = j / 10f;
                    float a = aBase + Mathf.Sin(t * 7f + k * 2f) * 6f + sgn * t * 8f;
                    Vector3 p = axis + Polar(a) * (R + .07f);
                    p.y = y47 - .3f + t * (3.2f + k * .6f);
                    pts.Add(p);
                }
                var wv = V.MakeVine("Dry vine " + (k + 1), wall, pts, .07f, .035f, 1, vineMat, wm, false, 1.6f);
                wv.witheredThickness = .5f; wv.witheredLeaf = .35f;
                V.AddLeaves(wv, leaf, 6, .45f, .1f, .95f, rnd, Vector3.zero);
            }
            var tint = Undo.AddComponent<EFBloomTint>(wall.gameObject);
            tint.source = wm; tint.seconds = 1.6f;
            tint.withered = new Color(.55f, .45f, .31f); tint.grown = new Color(.85f, 1f, .72f);

            // 조준 구체 옮기기
            var target = V.Target("전망대 뿌리") ?? V.Target("마른 벽 덩굴");
            var station = new GameObject("S01 Vine step").transform;
            Undo.RegisterCreatedObjectUndo(station.gameObject, "station");
            station.SetParent(grp, false);
            station.position = new Vector3(s47.position.x, y47, s47.position.z);
            if (target != null)
            {
                Undo.RecordObject(target.transform, "t");
                target.transform.position = axis + Polar(a47 - sgn * 4f) * (R + .75f) + Vector3.up * (y47 + 1.3f - axis.y);
                var so = new SerializedObject(target);
                so.FindProperty("title").stringValue = "마른 벽 덩굴";
                so.FindProperty("solution").stringValue = "덩굴이 다리로 자랐습니다. 건너편 착지대로 건너가세요.";
                so.FindProperty("station").objectReferenceValue = station;
                so.FindProperty("stationRadius").floatValue = 2.4f;
                var mo = so.FindProperty("motions"); mo.arraySize = 1; mo.GetArrayElementAtIndex(0).objectReferenceValue = wm;
                var rv = so.FindProperty("reveal"); rv.arraySize = 1; rv.GetArrayElementAtIndex(0).objectReferenceValue = walk.gameObject;
                so.ApplyModifiedProperties();
                Undo.RecordObject(target.gameObject, "name"); target.gameObject.name = "마른 벽 덩굴";
                log.Add("5) '전망대 뿌리' → '마른 벽 덩굴' 로 바꿔 47번 계단 옆 벽으로. 47번 계단에 서서 BLOOM → 덩굴다리가 착지대까지 자람");
            }
            else log.Add("5) '전망대 뿌리' 조준 구체를 못 찾음");

            // 6) 예전 뿌리 계단
            foreach (var old in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == "S_RootPath").ToList())
                Undo.DestroyObjectImmediate(old.gameObject);
            log.Add("6) 꼭대기에서 바깥으로 자라던 뿌리 계단(Growing root descent)은 이제 안 켜져요 (꺼진 채로 남겨 둠)");
        }

        // ---------------------------------------------------------------- 도우미
        static void Piece(GameObject model, Transform parent, Vector3 topCenter, Vector3 outDir, int seed)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            Undo.RegisterCreatedObjectUndo(g, "piece");
            g.transform.position = topCenter; g.transform.rotation = Quaternion.identity;
            var o = g.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "Out");
            Vector3 local = o != null ? Dir(o.position, g.transform.position) : Vector3.right;
            g.transform.rotation = Quaternion.FromToRotation(local, outDir);
            if (seed != 0) g.transform.rotation = Quaternion.AngleAxis(((seed * 37) % 11 - 5) * 1.5f, outDir) * g.transform.rotation;
            FixMat(g);
        }

        static void FixMat(GameObject g)
        {
            var mat = V.Mat("Windmill");
            if (mat == null) return;
            foreach (var r in g.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
        }

        static void Hide(Transform t)
        {
            foreach (var r in t.GetComponents<MeshRenderer>())
                if (r.enabled) { Undo.RecordObject(r, "hide"); r.enabled = false; }
        }

        static GameObject FindModel(string prefix)
        {
            var p = AssetDatabase.FindAssets(prefix + " t:Model").Select(AssetDatabase.GUIDToAssetPath)
                .Where(x =>
                {
                    var f = Path.GetFileNameWithoutExtension(x);
                    if (!f.StartsWith(prefix)) return false;
                    var rest = f.Substring(prefix.Length);
                    return rest.Length == 0 || rest[0] == '.' || rest[0] == ' ' || char.IsDigit(rest[0]);
                })
                .OrderByDescending(x => File.GetLastWriteTime(x)).FirstOrDefault();
            return p != null ? AssetDatabase.LoadAssetAtPath<GameObject>(p) : null;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
        static Vector3 Dir(Vector3 p, Vector3 axis) { var d = Flat(p - axis); return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.right; }
        static float Ang(Vector3 p, Vector3 axis) => Mathf.Atan2(p.z - axis.z, p.x - axis.x) * Mathf.Rad2Deg;
        static Vector3 Polar(float deg) => new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0, Mathf.Sin(deg * Mathf.Deg2Rad));
    }
}
#endif
