#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EverflowMap.EditorTools
{
    // 뿌리·덩굴 줄기 만들기 + E 회전 연잎 좌우 흔들기 + B 기둥 시든 덩굴
    // 메뉴
    //  Tools > EVERFLOW > 뿌리·덩굴 만들기 (B·E·W + E 연잎 좌우)
    //  Tools > EVERFLOW > 선택한 기둥에 B 시든 덩굴 감기
    // 둘 다 Ctrl+Z 한 번으로 되돌아가요. 다시 돌리면 예전 것을 지우고 새로 만들어요.
    public static class EFVinesRoots
    {
        const string MatDir = "Assets/EverflowMap/Materials";

        [MenuItem("Tools/EVERFLOW/뿌리·덩굴 만들기 (B·E·W + E 연잎 좌우)")]
        public static void RunAll()
        {
            Begin("뿌리·덩굴 만들기", out int group);
            var log = new List<string>();
            try
            {
                // S 뿌리 길은 'S 탑 고치기' 메뉴로 옮김 (탑 나선 계단 + 덩굴다리)
                BCatchmentStem(log);
                ESlabRoots(log);
                WSluiceVine(log);
                ELeafYaw(log);
            }
            catch (Exception e) { Debug.LogException(e); log.Add("오류: " + e.Message); }
            End(group, "뿌리·덩굴 만들기", log);
        }

        [MenuItem("Tools/EVERFLOW/선택한 기둥에 B 시든 덩굴 감기")]
        public static void RunPier()
        {
            var pier = Selection.activeTransform ?? Find("Spout pier");
            if (pier == null)
            {
                EditorUtility.DisplayDialog("B 시든 덩굴", "덩굴을 감을 기둥(물 나오는 곳 기둥)을 Hierarchy 에서 먼저 선택하고 다시 눌러 주세요.", "확인");
                return;
            }
            Begin("B 시든 덩굴", out int group);
            var log = new List<string>();
            try { BPierVine(pier, log); }
            catch (Exception e) { Debug.LogException(e); log.Add("오류: " + e.Message); }
            End(group, "B 시든 덩굴", log);
        }

        // ================================================================ B 물받이 잎 줄기
        static void BCatchmentStem(List<string> log)
        {
            var lc = Find("Leaf catchment");
            var m = lc != null ? lc.GetComponent<EF1123Motion>() : null;
            if (m == null) { log.Add("B) 'Leaf catchment' 없음"); return; }
            var parent = Unscaled(lc.parent);
            Remove(parent, "B_CatchmentStem");

            Vector3 pivot = lc.position;
            var b = WorldBounds(lc);
            Vector3 back = b.HasValue ? -Flat(b.Value.center - pivot) : Vector3.left;
            if (back == Vector3.zero) back = Vector3.left;
            Vector3 start = pivot + back * 1.7f; start.y = Ground(start) - .25f;
            var pts = new List<Vector3>
            {
                start,
                Vector3.Lerp(start, pivot, .3f) + Vector3.up * .35f,
                pivot + back * .55f + Vector3.up * .05f,
                pivot + back * .15f,
                pivot
            };
            MakeVine("B_CatchmentStem", parent, pts, .1f, .045f, 1, VineMat(), m, true, m.seconds);
            log.Add("B) 물받이 잎 줄기: 땅에서 잎자루까지. 잎 물받이에 BLOOM 하면 잎과 같이 자라요.");
        }

        // ================================================================ E 석판 뿌리
        static void ESlabRoots(List<string> log)
        {
            var sup = Find("Slab root support");
            var m = sup != null ? sup.GetComponent<EF1123Motion>() : null;
            if (m == null) { log.Add("E) 'Slab root support' 없음"); return; }
            var parent = Unscaled(sup.parent);
            Remove(parent, "E_SlabRoots");

            var b = WorldBounds(sup);
            float top = sup.position.y + ((b.HasValue ? b.Value.max.y : sup.position.y + .1f) - sup.position.y) * m.openScale.y;
            Vector3 axis = sup.position;
            float baseY = Mathf.Min(Ground(axis), b.HasValue ? b.Value.min.y : axis.y) - .25f;

            var holder = new GameObject("E_SlabRoots");
            Undo.RegisterCreatedObjectUndo(holder, "e");
            holder.transform.SetParent(parent, false);
            holder.transform.position = new Vector3(axis.x, baseY, axis.z);
            var mat = RootMat();
            for (int k = 0; k < 3; k++)
            {
                var pts = new List<Vector3>();
                float phase = k * 120f * Mathf.Deg2Rad;
                for (int j = 0; j <= 8; j++)
                {
                    float t = j / 8f, a = phase + t * 5.2f, rad = Mathf.Lerp(.6f, .12f, t);
                    pts.Add(new Vector3(axis.x + Mathf.Cos(a) * rad, Mathf.Lerp(baseY, top, t), axis.z + Mathf.Sin(a) * rad));
                }
                MakeVine("Root " + (k + 1), holder.transform, pts, .22f, .09f, 1, mat, m, true, m.seconds).delay = k * .06f;
            }
            foreach (var r in sup.GetComponentsInChildren<MeshRenderer>())
                if (r.enabled && r.GetComponent<EFVine>() == null) { Undo.RecordObject(r, "hide"); r.enabled = false; }
            log.Add("E) 석판 뿌리: 세 가닥이 꼬이며 석판 밑까지 자라요 (회색 기둥은 숨김).");
        }

        // ================================================================ W 수문 덩굴
        static void WSluiceVine(List<string> log)
        {
            var vine = Find("Sluice vine");
            var vm = vine != null ? vine.GetComponent<EF1123Motion>() : null;
            var W = Find("W | Sluice and drawbridge");
            var sl = W != null ? Under(W, "Maintenance sluice") : Find("Maintenance sluice");
            var sm = sl != null ? sl.GetComponent<EF1123Motion>() : null;
            if (vm == null || sl == null) { log.Add("W) 'Sluice vine' 또는 'Maintenance sluice' 없음"); return; }
            var parent = Unscaled(vine.parent);
            Remove(parent, "W_SluiceVine");

            var bb = WorldBounds(sl);
            if (!bb.HasValue) { log.Add("W) 수문 크기를 못 잼"); return; }
            var b = bb.Value;
            bool thinX = b.size.x < b.size.z;
            Vector3 normal = thinX ? Vector3.right : Vector3.forward, across = thinX ? Vector3.forward : Vector3.right;
            float halfT = (thinX ? b.extents.x : b.extents.z), halfW = (thinX ? b.extents.z : b.extents.x);
            var w01 = Find("W01 Sluice approach");
            float front = w01 != null && Vector3.Dot(w01.position - b.center, normal) < 0 ? -1 : 1;
            float lift = sm != null && sl.parent != null ? sl.parent.TransformVector(sm.offset).y : 3f;
            float topY = b.max.y + lift;

            var holder = new GameObject("W_SluiceVine");
            Undo.RegisterCreatedObjectUndo(holder, "w");
            holder.transform.SetParent(parent, false);
            holder.transform.position = b.center;
            var mat = VineMat();
            var leaf = LeafModel();
            var rnd = new System.Random(7);
            int leafCount = 0;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 x0 = b.center + across * (s * halfW * .72f) + normal * (front * (halfT + .12f));
                float baseY = Mathf.Min(Ground(x0), b.min.y) - .2f;
                var pts = new List<Vector3>();
                for (int j = 0; j <= 6; j++)
                {
                    float t = j / 6f;
                    pts.Add(new Vector3(x0.x, Mathf.Lerp(baseY, topY - .1f, t), x0.z) + across * (Mathf.Sin(t * 9f + s) * .16f));
                }
                Vector3 top = new Vector3(x0.x, topY, x0.z);
                pts.Add(top + Vector3.up * .2f - normal * (front * (halfT + .12f) * .5f));
                pts.Add(top - normal * (front * (halfT + .12f) * 1.15f) - Vector3.up * .15f);   // 문 윗단을 갈고리처럼 감아 쥠
                var v = MakeVine(s < 0 ? "Vine L" : "Vine R", holder.transform, pts, .12f, .05f, 1, mat, vm, true, vm.seconds * 1.1f);
                leafCount += AddLeaves(v, leaf, 6, .45f, .15f, .85f, rnd, normal * front);
            }
            foreach (var r in vine.GetComponentsInChildren<MeshRenderer>())
                if (r.enabled && r.GetComponent<EFVine>() == null && !r.transform.IsChildOf(holder.transform)) { Undo.RecordObject(r, "hide"); r.enabled = false; }
            log.Add("W) 수문 덩굴: 수문 양옆에서 두 가닥이 타고 올라 문 윗단을 감아 쥐어요 (올라간 높이 기준, 잎 " + leafCount + "장). 회색 덩굴은 숨김.");
        }

        // ================================================================ E 회전 연잎: 좌우로
        static void ELeafYaw(List<string> log)
        {
            var hinge = Find("Leaf hinge");
            var m = hinge != null ? hinge.GetComponent<EF1123Motion>() : null;
            if (m == null) { log.Add("E) 'Leaf hinge' 없음"); return; }
            if (Under(hinge, "Leaf tilt") == null)
            {
                var tilt = new GameObject("Leaf tilt").transform;
                Undo.RegisterCreatedObjectUndo(tilt.gameObject, "tilt");
                tilt.SetParent(hinge, false);
                foreach (var c in hinge.Cast<Transform>().Where(c => c != tilt).ToList())
                    Undo.SetTransformParent(c, tilt, "tilt");
                Quaternion old = hinge.rotation;
                Undo.RecordObject(hinge, "yaw");
                hinge.localRotation = Quaternion.identity;
                tilt.rotation = old;                 // 잎 모양·기울기는 그대로
            }
            Undo.RecordObject(m, "yaw");
            m.sway = new Vector3(0, 40, 0);          // 위아래 까딱임 없이 수평으로 좌우만
            m.alignmentTolerance = 15;
            log.Add("E) 회전 연잎: 수평으로 좌우 ±40°만 흔들려요 (가운데 근처 ±15°일 때 FREEZE 성공).");
        }

        // ================================================================ B 기둥 시든 덩굴
        static void BPierVine(Transform pier, List<string> log)
        {
            var bb = WorldBounds(pier);
            if (!bb.HasValue) { log.Add("선택한 오브젝트에 모양(Renderer)이 없어요"); return; }
            var b = bb.Value;
            var bloom = Target("잎 물받이");
            var src = bloom != null && bloom.motions != null && bloom.motions.Length > 0 ? bloom.motions[0] : null;
            var parent = Unscaled(pier.parent);
            Remove(parent, "B_WitheredVine");

            var holder = new GameObject("B_WitheredVine");
            Undo.RegisterCreatedObjectUndo(holder, "b");
            holder.transform.SetParent(parent, false);
            holder.transform.position = b.center;

            // 기둥 표면을 광선으로 더듬어 바짝 붙게 (임시 충돌체로 재고 바로 지움)
            var temp = new List<MeshCollider>();
            foreach (var mf in pier.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null && mf.GetComponent<EFVine>() == null)
                { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; temp.Add(mc); }
            Physics.SyncTransforms();
            Vector3 axis = b.center;
            float far = Mathf.Max(b.extents.x, b.extents.z) * 2f + 2f;
            const float VR = .06f;
            float Surface(float ang, float y, float fallback)
            {
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                var ray = new Ray(new Vector3(axis.x, y, axis.z) + dir * far, -dir);
                float best = float.MaxValue;
                foreach (var c in temp) if (c.Raycast(ray, out var hit, far * 2)) best = Mathf.Min(best, hit.distance);
                return best < float.MaxValue ? far - best : fallback;
            }

            float y0 = Mathf.Max(b.min.y, Ground(b.center) - .1f) + .05f;
            // 아래 굵기의 1.3배 넘게 튀어나오는 곳(지붕 처마 등) 아래까지만 감음
            float AvgR(float y) { float sum = 0; for (int i = 0; i < 8; i++) sum += Surface(i * Mathf.PI / 4, y, 0); return sum / 8; }
            float baseR = AvgR(y0 + .4f), y1 = y0 + .5f;
            for (float y = y0 + .4f; y < b.max.y - .1f; y += .2f)
            {
                if (AvgR(y) > baseR * 1.3f + .15f) break;
                y1 = y;
            }
            y1 = Mathf.Max(y0 + .8f, y1 - .25f);
            float h = y1 - y0;

            var mat = VineMat();
            var leaf = LeafModel();
            var rnd = new System.Random(11);
            int leaves = 0;
            for (int k = 0; k < 2; k++)
            {
                float turns = Mathf.Clamp(h / (k == 0 ? 1.5f : 1.9f), 1f, 3.5f);     // 느슨하게
                int n = Mathf.CeilToInt(turns * 24);
                var pts = new List<Vector3>();
                float lastR = baseR;
                for (int j = 0; j <= n; j++)
                {
                    float t = j / (float)n;
                    float ang = k * Mathf.PI + .7f * k + t * turns * Mathf.PI * 2 * (k == 0 ? 1 : -1);
                    float y = Mathf.Lerp(y0, y1 - k * .4f, t) + Mathf.Sin(t * 17f + k) * .12f;     // 삐뚤빼뚤
                    float r = Surface(ang, y, lastR); lastR = r;
                    r += VR * 1.1f;
                    pts.Add(new Vector3(axis.x + Mathf.Cos(ang) * r, y, axis.z + Mathf.Sin(ang) * r));
                }
                var v = MakeVine(k == 0 ? "Vine A" : "Vine B", holder.transform, pts, VR, VR * .5f, 1, mat, src, false, 1.6f);
                v.smooth = 3;
                v.witheredThickness = .6f; v.witheredLeaf = .4f;
                leaves += AddLeaves(v, leaf, 10, .45f, .06f, .97f, rnd, Vector3.zero);
            }
            foreach (var c in temp) UnityEngine.Object.DestroyImmediate(c);
            var tint = Undo.AddComponent<EFBloomTint>(holder);
            tint.source = src; tint.seconds = 1.6f;
            tint.withered = new Color(.56f, .46f, .32f); tint.grown = new Color(.85f, 1f, .72f);
            log.Add("B) '" + pier.name + "' 에 시든 덩굴 2가닥 + 잎 " + leaves + "장. " +
                    (src != null ? "'잎 물받이' 에 BLOOM 하면 굵어지고 잎이 커지며 생기 있는 색으로 바뀌어요." : "주의: '잎 물받이' 를 못 찾아 Source 가 비었어요 (켜지자마자 살아남)."));
        }

        // ================================================================ 만들기 도우미
        internal static EFVine MakeVine(string name, Transform parent, List<Vector3> world, float rBase, float rTip, float flat,
                               Material mat, EF1123Motion source, bool grow, float seconds)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "vine");
            go.transform.SetParent(parent, false);
            go.transform.position = world[0];
            go.transform.rotation = Quaternion.identity;
            var mr = go.AddComponent<MeshRenderer>(); go.AddComponent<MeshFilter>();
            mr.sharedMaterial = mat;
            var v = go.AddComponent<EFVine>();
            v.points = world.Select(p => go.transform.InverseTransformPoint(p)).ToList();
            v.radiusBase = rBase; v.radiusTip = rTip; v.flatness = flat;
            v.sides = rBase > .5f ? 14 : (rBase > .15f ? 10 : 7);
            v.source = source; v.grow = grow; v.seconds = Mathf.Max(.2f, seconds);
            v.uvLength = Mathf.PI * rBase * (1 + flat) * 2f;   // 나무껍질 무늬가 늘어나지 않게
            v.Refresh();
            EditorUtility.SetDirty(v);
            return v;
        }

        internal static int AddLeaves(EFVine v, GameObject model, int count, float size, float from, float to, System.Random rnd, Vector3 facing)
        {
            if (model == null) return 0;
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(model);
            probe.transform.position = Vector3.zero; probe.transform.rotation = Quaternion.identity; probe.transform.localScale = Vector3.one;
            var pb = WorldBounds(probe.transform);
            UnityEngine.Object.DestroyImmediate(probe);
            if (!pb.HasValue) return 0;
            Vector3 c = pb.Value.center, sz = pb.Value.size;
            Vector3 leafDir = Mathf.Abs(c.x) >= Mathf.Abs(c.z) ? new Vector3(Mathf.Sign(c.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(c.z));
            float length = Mathf.Max(sz.x, sz.z);
            Quaternion modelInv = Quaternion.Inverse(Quaternion.LookRotation(leafDir, Vector3.up));

            var list = new List<Transform>(v.leaves ?? new Transform[0]);
            var at = new List<float>(v.leafAt ?? new float[0]);
            var plant = Mat("Plant");
            for (int i = 0; i < count; i++)
            {
                float t = Mathf.Lerp(from, to, (i + (float)rnd.NextDouble() * .6f) / count);
                v.Sample(t, out Vector3 p, out Vector3 dir);
                Vector3 side = Vector3.Cross(dir, Vector3.up);
                if (side.sqrMagnitude < .01f) side = Vector3.Cross(dir, Vector3.forward);
                side = Quaternion.AngleAxis((float)rnd.NextDouble() * 360f, dir) * side.normalized;
                if (facing != Vector3.zero && Vector3.Dot(side, facing) < 0) side = -side;
                Vector3 outDir = (side + Vector3.up * .35f).normalized;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, v.transform);
                Undo.RegisterCreatedObjectUndo(inst, "leaf");
                inst.name = "Leaf";
                inst.transform.localPosition = p;
                inst.transform.rotation = Quaternion.LookRotation(outDir, Vector3.up) * modelInv
                                          * Quaternion.AngleAxis((float)rnd.NextDouble() * 50f - 25f, leafDir);
                inst.transform.localScale = Vector3.one * (size / Mathf.Max(length, .01f) * (.8f + (float)rnd.NextDouble() * .4f));
                if (plant != null)
                    foreach (var r in inst.GetComponentsInChildren<Renderer>())
                        r.sharedMaterials = Enumerable.Repeat(plant, r.sharedMaterials.Length).ToArray();
                foreach (var col in inst.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(col);
                list.Add(inst.transform); at.Add(t);
            }
            v.leaves = list.ToArray(); v.leafAt = at.ToArray();
            v.Refresh();
            EditorUtility.SetDirty(v);
            return count;
        }

        internal static Material RootMat() => BarkMat("EF_Root", new Color(.95f, .88f, .8f));
        internal static Material VineMat() => BarkMat("EF_VineStem", new Color(.62f, .76f, .44f));

        static Material BarkMat(string name, Color tint)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(shader) { name = name };
            var tex = AssetDatabase.FindAssets("Oak_bark t:Texture2D").Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p) == "Oak_bark").Select(p => AssetDatabase.LoadAssetAtPath<Texture2D>(p)).FirstOrDefault();
            if (tex != null)
            {
                foreach (var prop in new[] { "_BaseMap", "_MainTex" })
                    if (m.HasProperty(prop)) { m.SetTexture(prop, tex); m.SetTextureScale(prop, new Vector2(.5f, 1)); }   // 왼쪽 나무껍질 부분만
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .08f);
            Directory.CreateDirectory(MatDir);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        internal static Material Mat(string name) =>
            AssetDatabase.FindAssets(name + " t:Material").Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p).Equals(name, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.Contains("FantasyEnvironments")).Select(p => AssetDatabase.LoadAssetAtPath<Material>(p)).FirstOrDefault();

        internal static GameObject LeafModel()
        {
            var p = AssetDatabase.FindAssets("EF_Leaf t:Model").Select(AssetDatabase.GUIDToAssetPath)
                .Where(x => { var f = Path.GetFileNameWithoutExtension(x); return f == "EF_Leaf" || (f.StartsWith("EF_Leaf") && f.Length > 7 && (f[7] == '.' || f[7] == ' ' || char.IsDigit(f[7]))); })
                .OrderByDescending(x => File.GetLastWriteTime(x)).FirstOrDefault();
            return p != null ? AssetDatabase.LoadAssetAtPath<GameObject>(p) : null;
        }

        // ================================================================ 일반 도우미
        internal static void Begin(string title, out int group)
        {
            Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(title);
        }

        internal static void End(int group, string title, List<string> log)
        {
            Undo.CollapseUndoOperations(group);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            var msg = string.Join("\n", log);
            Debug.Log("[" + title + "]\n" + msg);
            EditorUtility.DisplayDialog(title, msg + "\n\n마음에 안 들면 Ctrl+Z 한 번으로 전부 되돌려져요.", "확인");
        }

        internal static void Remove(Transform parent, string name)
        {
            if (parent == null) return;
            foreach (var t in parent.Cast<Transform>().Where(t => t.name == name).ToList())
                Undo.DestroyObjectImmediate(t.gameObject);
        }

        internal static Transform Unscaled(Transform t)
        {
            while (t != null && (t.lossyScale - Vector3.one).sqrMagnitude > .0001f) t = t.parent;
            return t;
        }

        internal static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero; }

        internal static Bounds? WorldBounds(Transform t)
        {
            Bounds? b = null;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (r is LineRenderer || r.GetComponent<EFVine>() != null) continue;
                var mf = r.GetComponent<MeshFilter>();
                Bounds rb;
                if (mf != null && mf.sharedMesh != null)
                {
                    // 꺼진 Renderer 도 크기를 재도록 메시 범위를 직접 변환
                    var lb = mf.sharedMesh.bounds; var m = r.transform.localToWorldMatrix;
                    rb = new Bounds(m.MultiplyPoint3x4(lb.center), Vector3.zero);
                    for (int i = 0; i < 8; i++)
                        rb.Encapsulate(m.MultiplyPoint3x4(lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2f - 1))));
                }
                else if (r.enabled) rb = r.bounds; else continue;
                if (b.HasValue) { var x = b.Value; x.Encapsulate(rb); b = x; } else b = rb;
            }
            return b;
        }

        internal static float Ground(Vector3 p)
        {
            foreach (var t in Terrain.activeTerrains)
            {
                var o = t.transform.position; var s = t.terrainData.size;
                if (p.x >= o.x && p.x <= o.x + s.x && p.z >= o.z && p.z <= o.z + s.z)
                    return t.SampleHeight(p) + o.y;
            }
            if (Physics.Raycast(p + Vector3.up * 3, Vector3.down, out var hit, 40, ~0, QueryTriggerInteraction.Ignore)) return hit.point.y;
            return p.y - 1;
        }

        internal static Transform Find(string name) =>
            UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == name);

        internal static Transform Under(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        internal static EF1123Target Target(string title) =>
            UnityEngine.Object.FindObjectsByType<EF1123Target>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.title == title || t.name == title);
    }
}
#endif
