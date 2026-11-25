#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using V = EverflowMap.EditorTools.EFVinesRoots;

namespace EverflowMap.EditorTools
{
    // West: 지형 층 두 개 (렌즈방 층 / 꽃 점프대 층) + 꽃 점프대 옮기기 + 안 쓰는 계단 끄기 + 렌즈방에 WoodenHouse 맞추기
    //  1) Tools > EVERFLOW > West 1. 층 표시 상자 만들기
    //       'West_Layer_Lens', 'West_Layer_Flower' 상자가 생김. 옮기고 크기(X·Z) 바꾸세요. 상자 윗면 = 그 층 땅 높이.
//       렌즈층 상자를 옮기면 렌즈방 전체가, 꽃층 상자를 옮기면 꽃 점프대가 같이 옮겨져요.
    //  2) Tools > EVERFLOW > West 2. 층 적용 (지형·꽃 점프대·계단)
    //  3) WoodenHouse 프리팹을 씬에 끌어다 놓고 원하는 방향으로 돌린 뒤 선택 →
    //     Tools > EVERFLOW > West 3. 선택한 집을 렌즈방에 맞추기
    // 점프 높이 1.35m 기준: 수레층 → 꽃층 은 1.0m (점프 가능), 꽃층 → 렌즈층 은 3.8m (꽃 점프대로만)
    public static class EFWestLayers
    {
        const float CartY = 27.77f;           // 수레 있는 층 높이
        const float FlowerRise = 1.0f;        // 수레층보다 1m 위 (점프로 올라감)
        const float LensFloorBottom = 32.63f; // 지금 렌즈방 바닥 아랫면

        [MenuItem("Tools/EVERFLOW/West 1. 층 표시 상자 만들기")]
        static void Markers()
        {
            V.Begin("West 층 상자", out int g);
            var pad = V.Find("Flower pad foundation");
            Make("West_Layer_Lens", new Vector3(61.5f, LensFloorBottom, 75.25f), new Vector3(11f, 1f, 12f));
            Make("West_Layer_Flower", new Vector3(pad != null ? pad.position.x : 64f, CartY + FlowerRise, pad != null ? pad.position.z : 68.6f), new Vector3(5f, 1f, 5f));
            V.End(g, "West 층 상자", new List<string>
            {
                "상자 2개를 만들었어요.",
                "West_Layer_Lens : 렌즈방 층 (윗면 높이 " + LensFloorBottom + " = 지금 렌즈방 바닥)",
                "West_Layer_Flower : 꽃 점프대 층 (윗면 높이 " + (CartY + FlowerRise) + " = 수레층 + 1m)",
                "탑과 폭포 사이 등 원하는 자리로 옮기고 X·Z 크기를 바꾼 뒤 'West 2. 층 적용' 을 누르세요.",
                "높이를 바꾸고 싶으면 상자를 위아래로 옮기면 돼요 (윗면이 땅 높이)."
            });
        }

        static void Make(string name, Vector3 topCenter, Vector3 size)
        {
            if (V.Find(name) != null) return;
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(c, name);
            c.name = name;
            Object.DestroyImmediate(c.GetComponent<Collider>());
            c.transform.localScale = size;
            c.transform.position = topCenter - Vector3.up * (size.y / 2);
        }

        [MenuItem("Tools/EVERFLOW/West 2. 층 적용 (지형·꽃 점프대·계단)")]
        static void Apply()
        {
            V.Begin("West 층 적용", out int g);
            var log = new List<string>();
            var lens = V.Find("West_Layer_Lens"); var flower = V.Find("West_Layer_Flower");
            if (lens == null || flower == null) log.Add("먼저 'West 1. 층 표시 상자 만들기' 를 눌러 주세요.");
            else
            {
                Stamp(lens, log); Stamp(flower, log);

                // 렌즈방 통째로 렌즈층 위로 (상자 가운데 = 렌즈방 바닥 가운데)
                var floor = UnderWest("F window and lens chamber floor");
                var west = V.Find("West | Flower and lens");
                if (floor != null && west != null)
                {
                    Vector3 bottom = floor.position - Vector3.up * (floor.lossyScale.y * .5f);
                    Vector3 top = lens.position + Vector3.up * (lens.lossyScale.y * .5f);
                    Vector3 d = top - bottom;
                    if (d.sqrMagnitude > 1e-4f)
                    {
                        var skip = new HashSet<string> { "F01 Flower pad", "Flower pad foundation", "Flower pad growth", "도약 꽃", "Trail sign",
                                                         "West approach from upper road", "West trail to flower" };
                        foreach (var t in west.Cast<Transform>().Where(t => !skip.Contains(t.name)).ToList())
                        { Undo.RecordObject(t, "move"); t.position += d; }
                        log.Add("렌즈방 전체를 렌즈층 상자 위로 옮김 (" + d.x.ToString("0.0") + ", " + d.y.ToString("0.0") + ", " + d.z.ToString("0.0") + ")");
                    }
                }

                // 꽃 점프대 묶음을 꽃층 위로
                var found = V.Find("Flower pad foundation");
                if (found != null)
                {
                    float padTop = found.position.y + found.lossyScale.y * .5f;
                    Vector3 top = flower.position + Vector3.up * (flower.lossyScale.y * .5f);
                    Vector3 delta = new Vector3(top.x - found.position.x, top.y + .2f - padTop, top.z - found.position.z);
                    foreach (var n in new[] { "F01 Flower pad", "Flower pad foundation", "Flower pad growth", "도약 꽃", "Trail sign" })
                    {
                        var t = UnderWest(n);
                        if (t == null) continue;
                        Undo.RecordObject(t, "move"); t.position += delta;
                    }
                    log.Add("꽃 점프대(받침·꽃·조준 구체·표지판)를 꽃층 가운데로 옮김");
                }

                // 안 쓰는 계단 끄기 (지우지 않고 꺼 둠)
                foreach (var n in new[] { "West approach from upper road", "West trail to flower" })
                {
                    var t = UnderWest(n);
                    if (t != null && t.gameObject.activeSelf) { Undo.RecordObject(t.gameObject, "off"); t.gameObject.SetActive(false); log.Add("'" + n + "' 계단 끔"); }
                }

                foreach (var m in new[] { lens, flower })
                    foreach (var r in m.GetComponents<Renderer>()) { Undo.RecordObject(r, "hide"); r.enabled = false; }
                log.Add("표시 상자는 숨김 (다시 고치려면 Mesh Renderer 켜고 옮긴 뒤 다시 적용)");
            }
            V.End(g, "West 층 적용", log);
        }

        static void Stamp(Transform box, List<string> log)
        {
            float top = box.position.y + box.lossyScale.y * .5f;
            const float edge = .6f;                                   // 절벽 가장자리 경사 폭
            int touched = 0;
            foreach (var t in Terrain.activeTerrains)
            {
                var td = t.terrainData; var o = t.transform.position; var sz = td.size;
                int res = td.heightmapResolution;
                Undo.RegisterCompleteObjectUndo(td, "terrain");
                var h = td.GetHeights(0, 0, res, res);
                bool any = false;
                for (int zi = 0; zi < res; zi++)
                    for (int xi = 0; xi < res; xi++)
                    {
                        Vector3 w = new Vector3(o.x + xi / (float)(res - 1) * sz.x, top, o.z + zi / (float)(res - 1) * sz.z);
                        Vector3 l = box.InverseTransformPoint(w);
                        float dx = (Mathf.Abs(l.x) - .5f) * box.lossyScale.x, dz = (Mathf.Abs(l.z) - .5f) * box.lossyScale.z;
                        float d = Mathf.Max(dx, dz);                           // 상자 밖으로 몇 m
                        if (d > edge) continue;
                        float want = Mathf.Clamp01((top - o.y) / sz.y);
                        float k = d <= 0 ? 1 : 1 - Mathf.SmoothStep(0, 1, d / edge);
                        h[zi, xi] = Mathf.Lerp(h[zi, xi], want, k);
                        any = true;
                    }
                if (any) { td.SetHeights(0, 0, h); touched++; }
            }
            log.Add(box.name + ": 지형을 높이 " + top.ToString("0.00") + " 로 평평하게" + (touched == 0 ? " (지형을 못 찾음!)" : ""));
        }

        [MenuItem("Tools/EVERFLOW/West 3. 선택한 집을 렌즈방에 맞추기")]
        static void FitHouse()
        {
            var house = Selection.activeTransform;
            if (house == null) { EditorUtility.DisplayDialog("렌즈방 집", "WoodenHouse 를 씬에 놓고 선택한 뒤 눌러 주세요.", "확인"); return; }
            V.Begin("렌즈방 집", out int g);
            var log = new List<string>();
            string[] shell = { "F east side wall", "F west side wall", "F lens room roof", "F window lintel", "F southern left window pier", "F southern right window pier",
                               "Back lintel", "Front lintel", "Reward room canopy", "Reward west wall", "Sealed back wall", "East exit left jamb", "East exit right jamb", "East exit lintel" };
            var parts = shell.Select(UnderWest).Where(t => t != null).ToList();
            var floor = UnderWest("F window and lens chamber floor");
            if (parts.Count == 0 || floor == null) log.Add("렌즈방 그레이박스를 못 찾음");
            else
            {
                Bounds room = WB(parts[0]); foreach (var p in parts) room.Encapsulate(WB(p));
                Undo.RecordObject(house, "fit");
                house.localScale = Vector3.one;
                Bounds hb = Rend(house);
                float k = Mathf.Max(room.size.x / Mathf.Max(hb.size.x, .01f), room.size.z / Mathf.Max(hb.size.z, .01f)) * 1.04f;
                house.localScale = Vector3.one * k;
                hb = Rend(house);
                float floorTop = floor.position.y + floor.lossyScale.y * .5f;
                house.position += new Vector3(room.center.x - hb.center.x, floorTop - .05f - hb.min.y, room.center.z - hb.center.z);
                foreach (var c in house.GetComponentsInChildren<Collider>()) Undo.DestroyObjectImmediate(c);
                var west = V.Find("West | Flower and lens");
                if (west != null && house.parent != west) Undo.SetTransformParent(house, west, "parent");   // 렌즈층을 옮기면 집도 같이   // 드나드는 길은 그레이박스 충돌로
                foreach (var p in parts) foreach (var r in p.GetComponents<MeshRenderer>()) { Undo.RecordObject(r, "hide"); r.enabled = false; }
                log.Add("집을 렌즈방 크기에 맞춤 (배율 " + k.ToString("0.00") + "). 집의 충돌은 빼고, 회색 벽·지붕은 숨김 (벽 충돌은 그대로)");
                log.Add("방향이 안 맞으면 집을 Y로 90° 돌리고 이 메뉴를 다시 누르세요.");
            }
            V.End(g, "렌즈방 집", log);
        }


        // ================================================================ 4. 있는 에셋으로 장치 입히기
        // 바꾸고 싶으면 아래 프리팹 이름만 고치고 메뉴를 다시 누르면 돼요.
        static readonly (string obj, string prefab, bool thin, bool bottom, bool flame)[] Props =
        {
            ("Flower jump pad",          "EA03_Plant_Flower_01a_PRE", false, true,  false),   // 꽃 점프대 (밟는 판 윗면 높이에 맞춤)
            ("Flower pad foundation",    "Stone1",                    false, true,  false),   // 점프대 받침 흙돌
            ("Lens plinth",              "Furniture_table_small",     false, true,  false),   // 렌즈 받침
            ("Iron lens rim",            "Food_plate",                true,  false, false),   // 렌즈 쇠 테 (접시를 세워서)
            ("Dormant light receiver",   "Tavern_interior_lamp",      false, true,  false),   // 빛 받는 등 (꺼짐)
            ("Powered light receiver",   "Tavern_interior_lamp",      false, true,  true),    // 빛 받는 등 (켜짐 + 불꽃)
        };

        [MenuItem("Tools/EVERFLOW/West 4. 렌즈방·꽃 점프대 장치를 에셋으로")]
        static void PropsToAssets()
        {
            V.Begin("West 장치 에셋", out int g);
            var log = new List<string>();
            foreach (var p in Props)
            {
                var gb = UnderWest(p.obj);
                if (gb == null) { log.Add("· " + p.obj + ": 없음"); continue; }
                var pf = Prefab(p.prefab);
                if (pf == null) { log.Add("· " + p.obj + ": 프리팹 '" + p.prefab + "' 없음"); continue; }
                var old = gb.Cast<Transform>().FirstOrDefault(t => t.name == "Art"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

                var holder = new GameObject("Art").transform;                  // 부모 크기를 되돌리는 빈 오브젝트
                Undo.RegisterCreatedObjectUndo(holder.gameObject, "art");
                holder.SetParent(gb, false);
                var ls = gb.localScale; holder.localScale = new Vector3(1 / ls.x, 1 / ls.y, 1 / ls.z);

                var m = (GameObject)PrefabUtility.InstantiatePrefab(pf, holder);
                Undo.RegisterCreatedObjectUndo(m, "m");
                m.transform.position = Vector3.zero; m.transform.rotation = Quaternion.identity; m.transform.localScale = Vector3.one;
                Bounds mb = Rend(m.transform);
                Vector3 gsz = new Vector3(Mathf.Abs(gb.lossyScale.x), Mathf.Abs(gb.lossyScale.y), Mathf.Abs(gb.lossyScale.z));
                Quaternion rot = gb.rotation;
                Vector3 msz = mb.size;
                if (p.thin)
                {
                    int mt = Min(msz), gt = Min(gsz);                      // 얇은 축끼리 맞춤 (접시를 세움)
                    rot = gb.rotation * Quaternion.FromToRotation(Ax(mt), Ax(gt));
                    msz = Abs(Quaternion.FromToRotation(Ax(mt), Ax(gt)) * msz);
                }
                float k = Mathf.Min(gsz.x / Mathf.Max(msz.x, .01f), Mathf.Min(gsz.y / Mathf.Max(msz.y, .01f), gsz.z / Mathf.Max(msz.z, .01f)));
                if (p.obj == "Flower jump pad") k = Mathf.Min(gsz.x / Mathf.Max(msz.x, .01f), gsz.z / Mathf.Max(msz.z, .01f)) * 1.3f;   // 꽃은 조금 크게
                m.transform.rotation = rot;
                m.transform.localScale = Vector3.one * k / Mathf.Max(Mathf.Abs(holder.lossyScale.x), .0001f);
                mb = Rend(m.transform);
                if (p.obj == "Flower jump pad" && mb.size.y > gsz.y + .15f)    // 꽃 윗면 ≈ 밟는 판 윗면 (발이 꽃에 묻히지 않게)
                { m.transform.localScale *= (gsz.y + .15f) / mb.size.y; mb = Rend(m.transform); }
                Vector3 want = p.bottom ? gb.position - Vector3.up * (gsz.y * .5f) : gb.position;
                Vector3 have = p.bottom ? new Vector3(mb.center.x, mb.min.y, mb.center.z) : mb.center;
                m.transform.position += want - have;
                foreach (var c in m.GetComponentsInChildren<Collider>()) Undo.DestroyObjectImmediate(c);
                if (p.flame)
                {
                    var fx = Prefab("FX_CandleFlame");
                    if (fx != null)
                    {
                        var f = (GameObject)PrefabUtility.InstantiatePrefab(fx, m.transform);
                        Undo.RegisterCreatedObjectUndo(f, "fx");
                        f.transform.position = mb.center;
                    }
                }
                foreach (var r in gb.GetComponents<MeshRenderer>()) { Undo.RecordObject(r, "hide"); r.enabled = false; }
                log.Add("· " + p.obj + " → " + p.prefab);
            }
            log.Add("렌즈 액체·굳은 렌즈·빛줄기·창문 아치는 그대로 (효과용)");
            V.End(g, "West 장치 에셋", log);
        }

        static GameObject Prefab(string name)
        {
            var p = AssetDatabase.FindAssets(name + " t:Prefab").Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(x => System.IO.Path.GetFileNameWithoutExtension(x) == name);
            return p != null ? AssetDatabase.LoadAssetAtPath<GameObject>(p) : null;
        }
        static int Min(Vector3 v) => v.x <= v.y && v.x <= v.z ? 0 : (v.y <= v.z ? 1 : 2);
        static Vector3 Ax(int i) => i == 0 ? Vector3.right : (i == 1 ? Vector3.up : Vector3.forward);
        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static Transform UnderWest(string n)
        {
            var w = V.Find("West | Flower and lens");
            return w != null ? V.Under(w, n) : V.Find(n);
        }

        static Bounds WB(Transform t) => new Bounds(t.position, new Vector3(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.y), Mathf.Abs(t.lossyScale.z)));

        static Bounds Rend(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.one);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }
    }
}
#endif
