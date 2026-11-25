using System;
using System.Collections.Generic;
using UnityEngine;

namespace EverflowMap
{
    // 전 → 후 복원: 코스 별을 얻으면 그 구역이, 6개 다 모으면 맵 전체가 살아나요.
    // 별 자리에서 둥글게 퍼지며 땅 칠(메마른 흙 → 풀), 풀·꽃, 나무(마른 그루터기 → 초록 나무), 물, 안개 색이 바뀌어요.
    // 지형은 플레이할 때 복사본을 써서 원본 지형 파일은 바뀌지 않아요.
    [DefaultExecutionOrder(200)]
    public sealed class EFRestoration : MonoBehaviour
    {
        [Serializable]
        public sealed class Region
        {
            public string name;
            [Tooltip("코스 번호 (0 B, 1 E, 2 D, 3 C, 4 W, 5 West). -1 = 별 6개 다 모았을 때 (맵 전체)")]
            public int course = -1;
            public Transform center;
            public float radius = 24;
            [NonSerialized] public float start = -1, lastDetail = -99;
        }

        public EF1123World world;
        public Terrain terrain;
        public EFTerrainStates states;
        public Region[] regions = new Region[0];
        public EFRestoreWater[] waters = new EFRestoreWater[0];

        [Header("퍼지는 모양")]
        public float waveSeconds = 4f;
        public float edge = 6f;
        public float treeGrowSeconds = 1.2f;

        [Header("안개 (전 → 후)")]
        public bool driveFog = true;
        public Color fogBefore = new Color(.80f, .70f, .52f);
        public Color fogAfter = new Color(.55f, .82f, .92f);
        public float fogDensityBefore = .006f, fogDensityAfter = .003f;

        TerrainData data;
        float[,,] splatB, splatA, splat;
        float[,] life;                 // 0 = 전, 1 = 후
        int[][,] detB, detA;
        float[] treeBorn;
        int aRes, layers, dRes;
        float nextApply;
        bool ok;

        void Start()
        {
            if (terrain == null) terrain = Terrain.activeTerrain;
            if (terrain == null || states == null || !states.before.Valid || !states.after.Valid) { enabled = false; return; }

            data = Instantiate(terrain.terrainData);                     // 복사본으로 (원본 파일 보호)
            terrain.terrainData = data;
            var col = terrain.GetComponent<TerrainCollider>();
            if (col != null) col.terrainData = data;

            aRes = data.alphamapResolution; layers = data.alphamapLayers; dRes = data.detailResolution;
            if (states.before.alphaRes != aRes || states.before.layers != layers) { Debug.LogWarning("[복원] 저장된 칠과 지형 해상도/레이어 수가 달라요. '전/후 저장'을 다시 해 주세요."); enabled = false; return; }
            splatB = EFTerrainStates.ReadSplat(states.before);
            splatA = EFTerrainStates.ReadSplat(states.after);
            splat = (float[,,])splatB.Clone();
            life = new float[aRes, aRes];
            if (states.before.detailRes == dRes && states.after.detailRes == dRes)
            {
                detB = EFTerrainStates.ReadDetail(states.before);
                detA = EFTerrainStates.ReadDetail(states.after);
            }
            treeBorn = new float[states.after.trees.Length];
            for (int i = 0; i < treeBorn.Length; i++) treeBorn[i] = -1;

            data.SetAlphamaps(0, 0, splat);
            if (detB != null) for (int l = 0; l < detB.Length && l < data.detailPrototypes.Length; l++) data.SetDetailLayer(0, 0, l, detB[l]);
            ApplyTrees();
            foreach (var w in waters) if (w != null) w.Set(0);
            if (driveFog) { RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; }
            ok = true;
        }

        void Update()
        {
            if (!ok || world == null) return;
            bool active = false;
            for (int i = 0; i < regions.Length; i++)
            {
                var r = regions[i];
                if (r.start < 0)
                {
                    bool done = r.course >= 0
                        ? r.course < world.courses.Length && world.courses[r.course].collected
                        : world.Count >= world.courses.Length;
                    if (done) r.start = Time.time;
                }
                if (r.start >= 0 && Time.time - r.start < waveSeconds + treeGrowSeconds + .5f) active = true;
            }
            if (active && Time.time >= nextApply)
            {
                nextApply = Time.time + .1f;
                for (int i = 0; i < regions.Length; i++)
                    if (regions[i].start >= 0 && Time.time - regions[i].start < waveSeconds + .3f) Spread(regions[i]);
                ApplyTrees();
            }
            foreach (var w in waters)
            {
                if (w == null || w.region < 0 || w.region >= regions.Length) continue;
                var r = regions[w.region];
                w.Set(r.start < 0 ? 0 : (Time.time - r.start - waveSeconds * .3f) / Mathf.Max(w.seconds, .1f));
            }
            if (driveFog)
            {
                float k = 0; int n = 0;
                foreach (var r in regions) { if (r.course < 0) continue; n++; if (r.start >= 0) k += Mathf.Clamp01((Time.time - r.start) / waveSeconds); }
                k = n > 0 ? k / n : 0;
                RenderSettings.fogColor = Color.Lerp(fogBefore, fogAfter, k);
                RenderSettings.fogDensity = Mathf.Lerp(fogDensityBefore, fogDensityAfter, k);
            }
        }

        // 별 자리에서 둥글게 퍼지기
        void Spread(Region r)
        {
            Vector3 c = r.center != null ? r.center.position : transform.position;
            float t = Mathf.Clamp01((Time.time - r.start) / waveSeconds);
            float R = r.radius, ring = t * (R + edge);
            Vector3 o = terrain.transform.position, size = data.size;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((c.x - R - o.x) / size.x * (aRes - 1)), 0, aRes - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((c.x + R - o.x) / size.x * (aRes - 1)), 0, aRes - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((c.z - R - o.z) / size.z * (aRes - 1)), 0, aRes - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((c.z + R - o.z) / size.z * (aRes - 1)), 0, aRes - 1);
            if (x1 < x0 || z1 < z0) return;
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            var block = new float[h, w, layers];
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    float wx = o.x + x / (float)(aRes - 1) * size.x, wz = o.z + z / (float)(aRes - 1) * size.z;
                    float d = Mathf.Sqrt((wx - c.x) * (wx - c.x) + (wz - c.z) * (wz - c.z));
                    float k = (1 - Mathf.SmoothStep(0, 1, (d - (ring - edge)) / edge)) * (1 - Mathf.SmoothStep(0, 1, (d - (R - edge)) / edge));
                    if (k > life[z, x]) life[z, x] = k;
                    float L = life[z, x];
                    for (int l = 0; l < layers; l++)
                    {
                        float v = Mathf.Lerp(splatB[z, x, l], splatA[z, x, l], L);
                        splat[z, x, l] = v; block[z - z0, x - x0, l] = v;
                    }
                }
            data.SetAlphamaps(x0, z0, block);

            if (detB == null) return;
            bool big = R > 80;                                            // 맵 전체 복원은 풀을 덜 자주 (무거움)
            if (big && t < 1 && Time.time - r.lastDetail < .6f) return;
            r.lastDetail = Time.time;
            int dx0 = x0 * dRes / aRes, dx1 = Mathf.Min(dRes - 1, (x1 + 1) * dRes / aRes), dz0 = z0 * dRes / aRes, dz1 = Mathf.Min(dRes - 1, (z1 + 1) * dRes / aRes);
            int dw = dx1 - dx0 + 1, dh = dz1 - dz0 + 1;
            for (int l = 0; l < detB.Length && l < data.detailPrototypes.Length; l++)
            {
                var d = new int[dh, dw];
                for (int z = 0; z < dh; z++)
                    for (int x = 0; x < dw; x++)
                    {
                        float L = life[Mathf.Min(aRes - 1, (dz0 + z) * aRes / dRes), Mathf.Min(aRes - 1, (dx0 + x) * aRes / dRes)];
                        d[z, x] = L > .5f ? detA[l][dz0 + z, dx0 + x] : detB[l][dz0 + z, dx0 + x];
                    }
                data.SetDetailLayer(dx0, dz0, l, d);
            }
        }

        float LifeAt(Vector3 n) => life[Mathf.Clamp(Mathf.RoundToInt(n.z * (aRes - 1)), 0, aRes - 1), Mathf.Clamp(Mathf.RoundToInt(n.x * (aRes - 1)), 0, aRes - 1)];

        static readonly List<TreeInstance> list = new List<TreeInstance>();
        void ApplyTrees()
        {
            list.Clear();
            int protos = data.treePrototypes.Length;
            foreach (var t in states.before.trees)
                if (t.proto < protos && LifeAt(t.position) < .5f) list.Add(Make(t, 1));
            for (int i = 0; i < states.after.trees.Length; i++)
            {
                var t = states.after.trees[i];
                if (t.proto >= protos) continue;
                if (LifeAt(t.position) < .5f) { treeBorn[i] = -1; continue; }
                if (treeBorn[i] < 0) treeBorn[i] = Time.time;
                float g = Mathf.SmoothStep(0, 1, (Time.time - treeBorn[i]) / treeGrowSeconds);
                if (g > .02f) list.Add(Make(t, g));
            }
            data.SetTreeInstances(list.ToArray(), true);
        }

        static TreeInstance Make(EFTerrainStates.Tree t, float g) => new TreeInstance
        {
            position = t.position, prototypeIndex = t.proto, widthScale = t.width * g, heightScale = t.height * g,
            rotation = t.rotation, color = Color.white, lightmapColor = Color.white
        };

        void OnDrawGizmosSelected()
        {
            foreach (var r in regions)
            {
                if (r.center == null || r.course < 0) continue;
                Gizmos.color = new Color(.3f, 1f, .5f, .6f);
                Gizmos.DrawWireSphere(r.center.position, r.radius);
            }
        }
    }
}
