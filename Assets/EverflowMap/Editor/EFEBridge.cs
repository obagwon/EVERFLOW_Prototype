#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using V = EverflowMap.EditorTools.EFVinesRoots;

namespace EverflowMap.EditorTools
{
    // E 무너진 돌다리: 그레이박스에 돌 조각 모델 입히기 + 높은 턱을 FE Rock3 바위로
    // 메뉴: Tools > EVERFLOW > E 무너진 돌다리 + Rock3
    // 회색 상자는 숨기기만 (충돌·움직임 그대로). 다시 누르면 예전 모델 지우고 새로. Ctrl+Z 로 되돌림.
    public static class EFEBridge
    {
        static readonly (string gb, string model)[] Pieces =
        {
            ("E bench join", "EF_E_Bench"),
            ("PH_E_Slab - growing stone", "EF_E_Slab"),
            ("E middle ledge", "EF_E_Ledge"),
        };

        [MenuItem("Tools/EVERFLOW/E 무너진 돌다리 + Rock3")]
        static void Run()
        {
            V.Begin("E 돌다리", out int g);
            var log = new List<string>();
            var E = V.Find("E | Slab and rotating leaf");
            foreach (var (gbName, modelName) in Pieces)
            {
                var gb = E != null ? V.Under(E, gbName) : V.Find(gbName);
                var model = FindModel(modelName);
                if (gb == null || model == null) { log.Add("· " + gbName + ": " + (gb == null ? "없음" : modelName + " 모델 없음 → 블렌더 EF_EBridge_build.py 먼저")); continue; }
                Fit(gb, model);
                log.Add("· " + gbName + " → " + modelName);
            }

            // 높은 턱 = Rock3
            var floor = E != null ? V.Under(E, "E high shelf reward") : null;
            floor = floor != null ? V.Under(floor, "Reward room floor") : null;
            var rock = Prefab("Rock3");
            if (floor != null && rock != null)
            {
                foreach (var old in E.Cast<Transform>().Where(t => t.name == "E_HighShelf_Rock3").ToList()) Undo.DestroyObjectImmediate(old.gameObject);
                var r = (GameObject)PrefabUtility.InstantiatePrefab(rock, E);
                Undo.RegisterCreatedObjectUndo(r, "rock");
                r.name = "E_HighShelf_Rock3";
                r.transform.position = Vector3.zero; r.transform.rotation = Quaternion.Euler(0, 35, 0); r.transform.localScale = Vector3.one;
                Bounds rb = Rend(r.transform);
                Vector3 fs = new Vector3(Mathf.Abs(floor.lossyScale.x), Mathf.Abs(floor.lossyScale.y), Mathf.Abs(floor.lossyScale.z));
                float top = floor.position.y + fs.y * .5f;
                float ground = V.Ground(floor.position);
                float h = Mathf.Max(2f, top - ground + 1.5f);                         // 땅 속까지 묻히게
                float w = Mathf.Max(fs.x, fs.z) * 1.6f;
                r.transform.localScale = new Vector3(w / Mathf.Max(rb.size.x, .01f), h / Mathf.Max(rb.size.y, .01f), w / Mathf.Max(rb.size.z, .01f));
                rb = Rend(r.transform);
                r.transform.position += new Vector3(floor.position.x - rb.center.x, top - .03f - rb.max.y, floor.position.z - rb.center.z);
                foreach (var c in r.GetComponentsInChildren<Collider>()) Undo.DestroyObjectImmediate(c);   // 걷는 판정은 보상방 바닥으로
                log.Add("· 높은 턱: FE Rock3 바위 (윗면 = 보상방 바닥 높이, 아래는 땅 속까지)");
            }
            else log.Add("· Rock3: " + (floor == null ? "E high shelf reward / Reward room floor 없음" : "Rock3 프리팹 없음"));
            V.End(g, "E 돌다리", log);
        }

        static void Fit(Transform gb, GameObject model)
        {
            var old = gb.Cast<Transform>().FirstOrDefault(t => t.name == "Art"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var holder = new GameObject("Art").transform;               // 부모 크기를 되돌리는 빈 오브젝트
            Undo.RegisterCreatedObjectUndo(holder.gameObject, "art");
            holder.SetParent(gb, false);
            var ls = gb.localScale; holder.localScale = new Vector3(1 / ls.x, 1 / ls.y, 1 / ls.z);

            var m = (GameObject)PrefabUtility.InstantiatePrefab(model, holder);
            Undo.RegisterCreatedObjectUndo(m, "m");
            m.transform.position = Vector3.zero; m.transform.rotation = Quaternion.identity; m.transform.localScale = Vector3.one;
            Bounds mb = Rend(m.transform);
            Vector3 mLong = mb.size.x >= mb.size.z ? Vector3.right : Vector3.forward;   // 모델 길이 방향
            Vector3 s = new Vector3(Mathf.Abs(gb.lossyScale.x), Mathf.Abs(gb.lossyScale.y), Mathf.Abs(gb.lossyScale.z));
            Vector3 gLong = s.x >= s.z ? gb.right : gb.forward;                          // 상자 길이 방향
            m.transform.rotation = Quaternion.LookRotation(gLong, gb.up) * Quaternion.Inverse(Quaternion.LookRotation(mLong, Vector3.up));
            m.transform.position = gb.position + gb.up * (s.y * .5f);                    // 피벗 = 판 윗면 가운데
            var mat = V.Mat("Windmill");
            if (mat != null) foreach (var r in m.GetComponentsInChildren<Renderer>()) r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            foreach (var r in gb.GetComponents<MeshRenderer>()) { Undo.RecordObject(r, "hide"); r.enabled = false; }
        }

        static Bounds Rend(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.one);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        static GameObject FindModel(string name)
        {
            var p = AssetDatabase.FindAssets(name + " t:Model").Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == name);
            return p != null ? AssetDatabase.LoadAssetAtPath<GameObject>(p) : null;
        }

        static GameObject Prefab(string name)
        {
            var p = AssetDatabase.FindAssets(name + " t:Prefab").Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == name);
            return p != null ? AssetDatabase.LoadAssetAtPath<GameObject>(p) : null;
        }
    }
}
#endif
