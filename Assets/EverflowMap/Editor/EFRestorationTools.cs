#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EverflowMap.EditorTools
{
    // 전/후 복원 편집 도구 (FE 데모 씬처럼 지형에 칠·나무·풀을 그리는 방식)
    //  복원 1. 설치 — 지형 레이어·나무·풀 종류 추가, 구역 7개(코스 6 + 전체), 바람, '전' = 지금 지형, '후' = 자동 생성
    //  복원 2. '전' 보기 / '후' 보기 — 그 상태를 지형에 불러와요. Unity 지형 도구(Paint Texture / Trees / Details)로 고치고
    //  복원 3. 지금 지형을 '전'으로 저장 / '후'로 저장
    //  복원 4. 선택한 상자를 물로 — 납작한 상자 = 호수, 길쭉하게 세운 상자 = 폭포. 가까운 구역이 살아날 때 물이 차요
    public static class EFRestorationTools
    {
        const string Dir = "Assets/EverflowMap/Restoration";
        static readonly string[] AfterTrees = { "Oak_tree1", "Oak_tree2", "Oak_tree3", "Birch_tree1", "Birch_tree2", "Deciduous_tree1", "Deciduous_tree2", "Deciduous_tree3", "Pine_tree1", "Pine_tree2",
                                               "EA03_Plant_Bush_02a_PRE", "EA03_Plant_Bush_02c_PRE", "EA03_Plant_Bush_02e_PRE", "EA03_Plant_Bush_02g_PRE" };
        static readonly string[] BeforeTrees = { "Deciduous_tree_stump", "Pine_stump", "EA03_Plant_Bush_Dry_02a_PRE", "EA03_Plant_Bush_Dry_02b_PRE", "EA03_Plant_Bush_Dry_02d_PRE" };
        static readonly string[] Grass = { "Grass1", "Grass2", "Grass3", "Grass4" };
        static readonly string[] Flowers = { "Flower1", "Flower3", "Flower5", "Flower7", "EA03_Plant_Flower_01a_PRE", "EA03_Plant_Flower_01c_PRE", "EA03_Plant_Flower_01e_PRE" };
        static readonly string[] CourseNames = { "B", "E", "D", "C", "W", "West" };

        // ================================================================ 1. 설치
        [MenuItem("Tools/EVERFLOW/복원 1. 설치 (지형 레이어·나무·풀·구역, 후 자동 생성)")]
        static void Install()
        {
            var terrain = Terrain.activeTerrain ?? Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).FirstOrDefault();
            var world = Object.FindObjectsByType<EF1123World>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (terrain == null || world == null) { EditorUtility.DisplayDialog("복원", "Terrain 또는 EF1123World 를 못 찾았어요.", "확인"); return; }
            if (!EditorUtility.DisplayDialog("복원 1. 설치", "지형 파일에 레이어·나무·풀 종류를 추가하고, 지금 지형을 '전'으로 저장한 뒤 '후'를 자동으로 만들어요.\n(씬과 프로젝트를 먼저 저장해 두세요)", "진행", "취소")) return;
            var d = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(d, "복원 설치");
            var log = new List<string>();

            // 지형 레이어
            var layers = d.terrainLayers.ToList();
            foreach (var p in AssetDatabase.FindAssets("t:TerrainLayer").Select(AssetDatabase.GUIDToAssetPath).Where(q => q.Contains("FantasyEnvironments")))
            {
                var tl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(p);
                if (tl != null && !layers.Contains(tl)) layers.Add(tl);
            }
            d.terrainLayers = layers.ToArray();
            log.Add("지형 레이어: " + string.Join(", ", layers.Where(l => l != null).Select(l => l.name)));

            // 나무·풀 종류
            var trees = d.treePrototypes.ToList();
            foreach (var n in AfterTrees.Concat(BeforeTrees))
            {
                var pf = Prefab(n); if (pf == null || trees.Any(t => t.prefab == pf)) continue;
                trees.Add(new TreePrototype { prefab = pf });
            }
            d.treePrototypes = trees.ToArray();
            var det = d.detailPrototypes.ToList();
            foreach (var n in Grass.Concat(Flowers))
            {
                var pf = Prefab(n); if (pf == null || det.Any(x => x.prototype == pf)) continue;
                bool flower = Flowers.Contains(n);
                det.Add(new DetailPrototype
                {
                    prototype = pf, usePrototypeMesh = true, useInstancing = true, renderMode = DetailRenderMode.VertexLit,
                    minWidth = flower ? .8f : .9f, maxWidth = flower ? 1.2f : 1.4f, minHeight = flower ? .8f : .9f, maxHeight = flower ? 1.2f : 1.5f,
                    noiseSpread = .3f, healthyColor = Color.white, dryColor = Color.white
                });
            }
            d.detailPrototypes = det.ToArray();
            d.RefreshPrototypes();
            log.Add("나무 종류 " + d.treePrototypes.Length + "개, 풀·꽃 종류 " + d.detailPrototypes.Length + "개");

            // 상태 파일
            var states = States(true);
            Save(states.before, d, states, "before");
            Generate(terrain, d, log);
            Save(states.after, d, states, "after");
            if (deadCache != null)                                     // '전' 에 그루터기·마른 덤불 더하기
            {
                states.before.trees = states.before.trees.Concat(deadCache.Select(t => new EFTerrainStates.Tree
                { position = t.position, proto = t.prototypeIndex, width = t.widthScale, height = t.heightScale, rotation = t.rotation })).ToArray();
                deadCache = null; EditorUtility.SetDirty(states);
            }
            Load(states.before, d);                                   // 편집 화면은 '전' 으로
            log.Add("'전' = 지금 지형, '후' = 자동 생성 (풀·흙길·바위 칠 + 나무 + 풀·꽃)");

            // 복원 오브젝트·구역
            var go = GameObject.Find("EVERFLOW Restoration") ?? new GameObject("EVERFLOW Restoration");
            Undo.RegisterCreatedObjectUndo(go, "restore");
            var r = go.GetComponent<EFRestoration>() ?? go.AddComponent<EFRestoration>();
            r.world = world; r.terrain = terrain; r.states = states;
            var regs = new List<EFRestoration.Region>();
            for (int i = 0; i < world.courses.Length; i++)
            {
                var c = Child(go.transform, "Region " + (i < CourseNames.Length ? CourseNames[i] : i.ToString()));
                if (world.courses[i].star != null && c.localPosition == Vector3.zero) c.position = world.courses[i].star.position;
                var old = r.regions.FirstOrDefault(x => x.course == i);
                regs.Add(new EFRestoration.Region { name = c.name, course = i, center = c, radius = old != null ? old.radius : 26 });
            }
            var all = Child(go.transform, "Region ALL (별 6개)");
            if (all.localPosition == Vector3.zero) all.position = world.finishCenter;
            regs.Add(new EFRestoration.Region { name = all.name, course = -1, center = all, radius = Mathf.Max(d.size.x, d.size.z) * 1.5f });
            r.regions = regs.ToArray();
            EditorUtility.SetDirty(r);

            if (Object.FindObjectsByType<WindZone>(FindObjectsSortMode.None).Length == 0)
            {
                var wz = new GameObject("WindZone"); Undo.RegisterCreatedObjectUndo(wz, "wind");
                var z = wz.AddComponent<WindZone>(); z.windMain = .4f; z.windTurbulence = .3f;
                wz.transform.rotation = Quaternion.Euler(0, 40, 0);
                log.Add("바람(WindZone) 추가 — 나무·풀 흔들림");
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            AssetDatabase.SaveAssets();
            Selection.activeObject = go;
            EditorUtility.DisplayDialog("복원 1. 설치", string.Join("\n", log) + "\n\n'EVERFLOW Restoration' 을 선택하면 구역 범위(초록 원)가 보여요. Region 오브젝트를 옮기거나 Radius 를 바꾸세요.", "확인");
        }

        // '후' 자동 생성: 완만한 땅 = 풀, 가파른 곳 = 바위, 원래 흙길은 그대로 + 나무·풀·꽃
        static void Generate(Terrain terrain, TerrainData d, List<string> log)
        {
            int res = d.alphamapResolution, L = d.alphamapLayers;
            var a = d.GetAlphamaps(0, 0, res, res);
            int grass = Find(d, "grass"), rock = Find(d, "rock"), road = Find(d, "road"), stone = Find(d, "stone");
            if (grass < 0) { log.Add("풀 레이어를 못 찾아 칠은 건너뜀"); }
            else
                for (int z = 0; z < res; z++)
                    for (int x = 0; x < res; x++)
                    {
                        float nx = x / (float)(res - 1), nz = z / (float)(res - 1);
                        float steep = d.GetSteepness(nx, nz);
                        float keep = (road >= 0 ? a[z, x, road] : 0) + (stone >= 0 ? a[z, x, stone] : 0);
                        keep = Mathf.Clamp01(keep);
                        float rk = rock >= 0 ? Mathf.SmoothStep(0, 1, (steep - 30) / 15f) : 0;
                        for (int l = 0; l < L; l++) a[z, x, l] = (l == road || l == stone) ? a[z, x, l] : 0;
                        float rest = 1 - keep;
                        if (rock >= 0) a[z, x, rock] += rest * rk;
                        a[z, x, grass] += rest * (1 - rk);
                    }
            d.SetAlphamaps(0, 0, a);

            // 풀·꽃
            int dr = d.detailResolution;
            float cell = d.size.x / dr; float area = cell * cell;
            var rnd = new System.Random(7);
            var protos = d.detailPrototypes;
            var okCell = new bool[dr, dr];
            for (int z = 0; z < dr; z++)
                for (int x = 0; x < dr; x++)
                {
                    float nx = x / (float)(dr - 1), nz = z / (float)(dr - 1);
                    int ax = Mathf.Min(res - 1, Mathf.RoundToInt(nx * (res - 1))), az = Mathf.Min(res - 1, Mathf.RoundToInt(nz * (res - 1)));
                    okCell[z, x] = d.GetSteepness(nx, nz) <= 28 && (grass < 0 || a[az, ax, grass] >= .6f);
                }
            for (int l = 0; l < protos.Length; l++)
            {
                string n = protos[l].prototype != null ? protos[l].prototype.name : "";
                bool isGrass = Grass.Contains(n), isFlower = Flowers.Contains(n);
                if (!isGrass && !isFlower) continue;
                float perM2 = isGrass ? 2.2f / Grass.Length * 2 : .25f / Flowers.Length * 2;
                var map = new int[dr, dr];
                for (int z = 0; z < dr; z++)
                    for (int x = 0; x < dr; x++)
                    {
                        if (!okCell[z, x]) continue;
                        float nx = x / (float)(dr - 1), nz = z / (float)(dr - 1);
                        float patch = Mathf.PerlinNoise(nx * 40 + l * 7, nz * 40);           // 뭉쳐서 자라게
                        float want = perM2 * area * (isFlower ? Mathf.SmoothStep(0, 1, (patch - .55f) / .2f) * 3 : .4f + patch);
                        int count = (int)want + (rnd.NextDouble() < want - (int)want ? 1 : 0);
                        map[z, x] = Mathf.Min(count, 255);
                    }
                d.SetDetailLayer(0, 0, l, map);
            }

            // 나무: 퍼즐 장치·발판과 4.5m 이상 떨어진 완만한 풀밭에만
            Physics.SyncTransforms();
            var tp = d.treePrototypes;
            var afterIdx = Enumerable.Range(0, tp.Length).Where(i => tp[i].prefab != null && AfterTrees.Contains(tp[i].prefab.name)).ToArray();
            var beforeIdx = Enumerable.Range(0, tp.Length).Where(i => tp[i].prefab != null && BeforeTrees.Contains(tp[i].prefab.name)).ToArray();
            var list = new List<TreeInstance>();
            var existing = d.treeInstances;
            float step = 8f;
            Vector3 o = terrain.transform.position;
            for (float z = step / 2; z < d.size.z && afterIdx.Length > 0; z += step)
                for (float x = step / 2; x < d.size.x; x += step)
                {
                    float px = x + (float)(rnd.NextDouble() - .5) * step * .8f, pz = z + (float)(rnd.NextDouble() - .5) * step * .8f;
                    float nx = px / d.size.x, nz = pz / d.size.z;
                    if (nx < 0 || nx > 1 || nz < 0 || nz > 1 || d.GetSteepness(nx, nz) > 22) continue;
                    int ax = Mathf.RoundToInt(nx * (res - 1)), az = Mathf.RoundToInt(nz * (res - 1));
                    if (grass >= 0 && a[az, ax, grass] < .7f) continue;
                    if (Mathf.PerlinNoise(nx * 18, nz * 18) < .45f) continue;                  // 숲 덩어리
                    Vector3 w = new Vector3(o.x + px, o.y + d.GetInterpolatedHeight(nx, nz), o.z + pz);
                    if (Physics.OverlapSphere(w + Vector3.up * 2, 4.5f, ~0, QueryTriggerInteraction.Ignore).Any(c => !(c is TerrainCollider))) continue;
                    float s = .85f + (float)rnd.NextDouble() * .35f;
                    list.Add(new TreeInstance { position = new Vector3(nx, 0, nz), prototypeIndex = afterIdx[rnd.Next(afterIdx.Length)], widthScale = s, heightScale = s, rotation = (float)rnd.NextDouble() * Mathf.PI * 2, color = Color.white, lightmapColor = Color.white });
                }
            d.SetTreeInstances(existing.Concat(list).ToArray(), true);
            log.Add("후 나무 " + list.Count + "그루 추가");

            // '전' 나무: 같은 자리 일부에 그루터기·마른 덤불 (지금 지형 나무에 더해서 저장됨)
            if (beforeIdx.Length > 0)
            {
                var dead = list.Where((t, i) => i % 3 == 0).Select(t => { t.prototypeIndex = beforeIdx[rnd.Next(beforeIdx.Length)]; return t; }).ToList();
                deadCache = dead;
            }
        }
        static List<TreeInstance> deadCache;

        static int Find(TerrainData d, string key)
        {
            var ls = d.terrainLayers;
            for (int i = 0; i < ls.Length; i++) if (ls[i] != null && ls[i].name.ToLower().Contains(key)) return i;
            return -1;
        }

        // ================================================================ 2·3. 보기 / 저장
        [MenuItem("Tools/EVERFLOW/복원 2. '전' 보기 (메마른 상태 편집)")] static void ViewBefore() => View(true);
        [MenuItem("Tools/EVERFLOW/복원 2. '후' 보기 (살아난 상태 편집)")] static void ViewAfter() => View(false);
        [MenuItem("Tools/EVERFLOW/복원 3. 지금 지형을 '전'으로 저장")] static void SaveBefore() => SaveCurrent(true);
        [MenuItem("Tools/EVERFLOW/복원 3. 지금 지형을 '후'로 저장")] static void SaveAfter() => SaveCurrent(false);

        static void View(bool before)
        {
            var t = Terrain.activeTerrain; var s = States(false);
            if (t == null || s == null) { EditorUtility.DisplayDialog("복원", "먼저 '복원 1. 설치' 를 해 주세요.", "확인"); return; }
            Undo.RegisterCompleteObjectUndo(t.terrainData, "view");
            if (!Load(before ? s.before : s.after, t.terrainData)) return;
            EditorUtility.DisplayDialog("복원", (before ? "'전'" : "'후'") + " 상태를 지형에 불러왔어요.\n지형 도구로 고친 뒤 '복원 3. 지금 지형을 " + (before ? "'전'" : "'후'") + "으로 저장' 을 꼭 눌러 주세요.", "확인");
        }

        static void SaveCurrent(bool before)
        {
            var t = Terrain.activeTerrain; var s = States(false);
            if (t == null || s == null) { EditorUtility.DisplayDialog("복원", "먼저 '복원 1. 설치' 를 해 주세요.", "확인"); return; }
            Save(before ? s.before : s.after, t.terrainData, s, before ? "before" : "after");
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("복원", "지금 지형을 " + (before ? "'전'" : "'후'") + " 으로 저장했어요.", "확인");
        }

        static EFTerrainStates States(bool create)
        {
            string path = Dir + "/EF_TerrainStates.asset";
            var s = AssetDatabase.LoadAssetAtPath<EFTerrainStates>(path);
            if (s == null && create)
            {
                Directory.CreateDirectory(Dir);
                s = ScriptableObject.CreateInstance<EFTerrainStates>();
                AssetDatabase.CreateAsset(s, path);
            }
            return s;
        }

        static void Save(EFTerrainStates.State st, TerrainData d, EFTerrainStates asset, string tag)
        {
            foreach (var old in st.splat.Concat(st.detail)) if (old != null) Object.DestroyImmediate(old, true);
            int res = d.alphamapResolution, L = d.alphamapLayers;
            var a = d.GetAlphamaps(0, 0, res, res);
            st.alphaRes = res; st.layers = L;
            st.splat = new Texture2D[(L + 3) / 4];
            for (int k = 0; k < st.splat.Length; k++)
            {
                var tex = new Texture2D(res, res, TextureFormat.RGBA32, false, true) { name = tag + "_splat" + k };
                var px = new Color32[res * res];
                for (int y = 0; y < res; y++)
                    for (int x = 0; x < res; x++)
                    {
                        byte V(int l) => l < L ? (byte)Mathf.RoundToInt(Mathf.Clamp01(a[y, x, l]) * 255) : (byte)0;
                        px[y * res + x] = new Color32(V(k * 4), V(k * 4 + 1), V(k * 4 + 2), V(k * 4 + 3));
                    }
                tex.SetPixels32(px); tex.Apply();
                AssetDatabase.AddObjectToAsset(tex, asset); st.splat[k] = tex;
            }
            int dr = d.detailResolution; st.detailRes = dr;
            st.detail = new Texture2D[d.detailPrototypes.Length];
            for (int l = 0; l < st.detail.Length; l++)
            {
                var m = d.GetDetailLayer(0, 0, dr, dr, l);
                var tex = new Texture2D(dr, dr, TextureFormat.R8, false, true) { name = tag + "_detail" + l };
                var px = new Color32[dr * dr];
                for (int y = 0; y < dr; y++) for (int x = 0; x < dr; x++) px[y * dr + x] = new Color32((byte)Mathf.Clamp(m[y, x], 0, 255), 0, 0, 255);
                tex.SetPixels32(px); tex.Apply();
                AssetDatabase.AddObjectToAsset(tex, asset); st.detail[l] = tex;
            }
            st.trees = d.treeInstances.Select(t => new EFTerrainStates.Tree { position = t.position, proto = t.prototypeIndex, width = t.widthScale, height = t.heightScale, rotation = t.rotation }).ToArray();
            EditorUtility.SetDirty(asset);
        }

        static bool Load(EFTerrainStates.State st, TerrainData d)
        {
            if (!st.Valid) { EditorUtility.DisplayDialog("복원", "저장된 상태가 없어요.", "확인"); return false; }
            if (st.alphaRes != d.alphamapResolution || st.layers != d.alphamapLayers)
            { EditorUtility.DisplayDialog("복원", "지형 레이어 수나 해상도가 저장할 때와 달라요. 지금 지형을 다시 저장해 주세요.", "확인"); return false; }
            d.SetAlphamaps(0, 0, EFTerrainStates.ReadSplat(st));
            if (st.detailRes == d.detailResolution)
            {
                var det = EFTerrainStates.ReadDetail(st);
                for (int l = 0; l < det.Length && l < d.detailPrototypes.Length; l++) d.SetDetailLayer(0, 0, l, det[l]);
            }
            d.SetTreeInstances(st.trees.Where(t => t.proto < d.treePrototypes.Length).Select(t => new TreeInstance
            { position = t.position, prototypeIndex = t.proto, widthScale = t.width, heightScale = t.height, rotation = t.rotation, color = Color.white, lightmapColor = Color.white }).ToArray(), true);
            return true;
        }

        // ================================================================ 4. 물
        [MenuItem("Tools/EVERFLOW/복원 4. 선택한 상자를 물로 (호수·폭포)")]
        static void Water()
        {
            var r = Object.FindObjectsByType<EFRestoration>(FindObjectsSortMode.None).FirstOrDefault();
            var world = Object.FindObjectsByType<EF1123World>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (r == null || Selection.transforms.Length == 0) { EditorUtility.DisplayDialog("복원 4", r == null ? "먼저 '복원 1. 설치' 를 해 주세요." : "물 자리에 놓은 상자(Cube)를 선택하고 눌러 주세요.\n납작한 상자 = 호수 (윗면 = 수면), 세운 얇은 상자 = 폭포.", "확인"); return; }
            Undo.IncrementCurrentGroup(); int g = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("물");
            var baseMat = AssetDatabase.FindAssets("ContinuousWater t:Material").Select(AssetDatabase.GUIDToAssetPath).Select(p => AssetDatabase.LoadAssetAtPath<Material>(p)).FirstOrDefault();
            var pool = WaterMat("EF_PoolWater", baseMat, 1); var fall = WaterMat("EF_FallWater", baseMat, 0);
            var made = new List<string>(); var waters = r.waters.ToList(); var rends = new List<Renderer>();
            foreach (var box in Selection.transforms)
            {
                Vector3 s = new Vector3(Mathf.Abs(box.lossyScale.x), Mathf.Abs(box.lossyScale.y), Mathf.Abs(box.lossyScale.z));
                bool isFall = s.y > Mathf.Min(s.x, s.z) * 2f && s.y > 1.5f;
                var old = box.Find("Water"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
                var go = new GameObject("Water"); Undo.RegisterCreatedObjectUndo(go, "w");
                go.transform.SetParent(box, false);
                go.transform.localScale = new Vector3(1 / box.localScale.x, 1 / box.localScale.y, 1 / box.localScale.z);
                go.transform.position = box.position + box.up * (isFall ? 0 : s.y * .5f);
                go.transform.rotation = box.rotation;
                var mesh = isFall ? FallMesh(s) : PoolMesh(s);
                Directory.CreateDirectory(Dir);
                AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(Dir + "/Water_" + box.name + ".asset"));
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = isFall ? fall : pool;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var w = go.AddComponent<EFRestoreWater>(); w.waterfall = isFall;
                w.region = Nearest(r, box.position);
                foreach (var bc in box.GetComponents<Collider>()) Undo.DestroyObjectImmediate(bc);
                foreach (var br in box.GetComponents<MeshRenderer>()) { Undo.RecordObject(br, "hide"); br.enabled = false; }
                waters.Add(w); rends.Add(mr);
                made.Add(box.name + " → " + (isFall ? "폭포" : "호수") + " (구역 " + r.regions[w.region].name + ")");
            }
            Undo.RecordObject(r, "waters"); r.waters = waters.ToArray(); EditorUtility.SetDirty(r);
            if (world != null)
            {
                var so = new SerializedObject(world); var wp = so.FindProperty("water");
                foreach (var rr in rends) { wp.arraySize++; wp.GetArrayElementAtIndex(wp.arraySize - 1).objectReferenceValue = rr; }
                so.ApplyModifiedProperties();
            }
            Undo.CollapseUndoOperations(g);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            EditorUtility.DisplayDialog("복원 4", string.Join("\n", made) + "\n\n그 구역 별을 얻으면 물이 차오르고(호수) 쏟아져요(폭포). 구역은 EFRestoreWater 의 Region 번호로 바꿀 수 있어요.", "확인");
        }

        static int Nearest(EFRestoration r, Vector3 p)
        {
            int best = r.regions.Length - 1; float bd = float.MaxValue;
            for (int i = 0; i < r.regions.Length; i++)
            {
                var g = r.regions[i]; if (g.course < 0 || g.center == null) continue;
                float d = Vector3.Distance(g.center.position, p) / Mathf.Max(g.radius, 1);
                if (d < bd && d < 1.6f) { bd = d; best = i; }
            }
            return best;
        }

        static Material WaterMat(string name, Material src, float worldUV)
        {
            string path = Dir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null || src == null) return m ?? src;
            Directory.CreateDirectory(Dir);
            m = new Material(src) { name = name };
            m.SetFloat("_WorldUV", worldUV); m.SetFloat("_Wet", 1); m.SetFloat("_Arrival", 1);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Mesh PoolMesh(Vector3 s)
        {
            int n = 16; var v = new List<Vector3>(); var c = new List<Color>(); var uv = new List<Vector2>(); var uv2 = new List<Vector2>(); var t = new List<int>();
            for (int z = 0; z <= n; z++)
                for (int x = 0; x <= n; x++)
                {
                    float u = x / (float)n * 2 - 1, w = z / (float)n * 2 - 1;
                    float rr = Mathf.Sqrt(u * u + w * w) / 1.12f;                         // 둥근 웅덩이
                    v.Add(new Vector3(u * s.x * .5f, 0, w * s.z * .5f));
                    c.Add(new Color(0, 0, 0, 1 - Mathf.SmoothStep(0, 1, (rr - .78f) / .22f)));
                    uv.Add(new Vector2(u * s.x * .5f, w * s.z * .5f)); uv2.Add(new Vector2(0, .15f));
                }
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++) { int i = z * (n + 1) + x; t.AddRange(new[] { i, i + n + 1, i + 1, i + 1, i + n + 1, i + n + 2 }); }
            return Build(v, c, uv, uv2, t, "Pool");
        }

        static Mesh FallMesh(Vector3 s)
        {
            bool alongX = s.x >= s.z;
            float W = alongX ? s.x : s.z, H = s.y;
            int nu = 6, nv = Mathf.Max(8, Mathf.CeilToInt(H * 2));
            var v = new List<Vector3>(); var c = new List<Color>(); var uv = new List<Vector2>(); var uv2 = new List<Vector2>(); var t = new List<int>();
            for (int j = 0; j <= nv; j++)
                for (int i = 0; i <= nu; i++)
                {
                    float u = i / (float)nu * 2 - 1, k = j / (float)nv;                  // k: 0 = 위, 1 = 아래
                    float y = H * .5f - k * H;
                    float bulge = Mathf.Sin(k * Mathf.PI) * .12f;
                    v.Add(alongX ? new Vector3(u * W * .5f, y, bulge) : new Vector3(bulge, y, u * W * .5f));
                    float a = (1 - Mathf.SmoothStep(0, 1, (Mathf.Abs(u) - .75f) / .25f)) * Mathf.SmoothStep(0, 1, k / .06f + .4f);
                    c.Add(new Color(1, 0, 0, a));
                    uv.Add(new Vector2(u * W * .25f, k * H * .5f)); uv2.Add(new Vector2(k, 0));
                }
            for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++) { int a = j * (nu + 1) + i; t.AddRange(new[] { a, a + 1, a + nu + 1, a + 1, a + nu + 2, a + nu + 1 }); }
            return Build(v, c, uv, uv2, t, "Fall");
        }

        static Mesh Build(List<Vector3> v, List<Color> c, List<Vector2> uv, List<Vector2> uv2, List<int> t, string name)
        {
            var m = new Mesh { name = "EF_" + name + "Water" };
            m.SetVertices(v); m.SetColors(c); m.SetUVs(0, uv); m.SetUVs(1, uv2); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        // ================================================================ 도우미
        static GameObject Prefab(string name) =>
            AssetDatabase.FindAssets(name + " t:Prefab").Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p) == name).Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p)).FirstOrDefault();

        static Transform Child(Transform parent, string name)
        {
            var c = parent.Find(name);
            if (c != null) return c;
            var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
#endif
