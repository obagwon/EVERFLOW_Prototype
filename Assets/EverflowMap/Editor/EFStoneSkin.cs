#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using V = EverflowMap.EditorTools.EFVinesRoots;

namespace EverflowMap.EditorTools
{
    // 그레이박스 상자에 FE 풍차 돌벽 겉면 입히기 + C 돌아오는 다리
    //  Tools > EVERFLOW > B 수조 돌벽 입히기 (벽 + 바닥)
    //  Tools > EVERFLOW > 선택한 그레이박스에 돌벽 입히기      (E, West 등 아무 상자나. 자식까지)
    //  Tools > EVERFLOW > C 돌아오는 다리 → EF_C_Bridge
    // 모양·충돌은 그대로, 보이는 메시만 돌 UV 가 입혀진 상자로 바뀜. Ctrl+Z 로 되돌림.
    public static class EFStoneSkin
    {
        static readonly Vector4 Wall = new Vector4(0.032f, 0.318f, 0.250f, 0.483f);
        static readonly Vector4 Cap = new Vector4(0.005f, 0.278f, 0.275f, 0.309f);
        const float PW = 2.2f, PH = 1.66f, CapLen = 2.7f, CapRow = .62f;
        const string MeshDir = "Assets/EverflowMap/Meshes";

        [MenuItem("Tools/EVERFLOW/B 수조 돌벽 입히기 (벽 + 바닥)")]
        static void BTank()
        {
            V.Begin("B 수조 돌벽", out int g);
            var log = new List<string>();
            var tank = V.Find("B_Watertank");
            if (tank == null) log.Add("B_Watertank 없음");
            else
            {
                var walls = tank.Cast<Transform>().Where(t => t.name.StartsWith("B_Wall") && t.gameObject.activeSelf).ToList();
                if (V.Under(tank, "B_Tank_Floor") == null && walls.Count > 0)
                {
                    var b = new Bounds(walls[0].position, Vector3.zero);
                    foreach (var w in walls) b.Encapsulate(new Bounds(w.position, Abs(w.lossyScale)));
                    var f = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Undo.RegisterCreatedObjectUndo(f, "floor");
                    f.name = "B_Tank_Floor";
                    f.transform.SetParent(tank, true);
                    f.transform.position = new Vector3(b.center.x, b.min.y + .15f, b.center.z);
                    f.transform.localScale = new Vector3(b.size.x, .3f, b.size.z);
                    log.Add("수조 바닥(B_Tank_Floor) 추가: 벽 맨 아래 높이");
                }
                int n = 0;
                foreach (var t in tank.GetComponentsInChildren<Transform>(true)) if (Skin(t)) n++;
                log.Add("돌벽 입힌 상자 " + n + "개");
            }
            V.End(g, "B 수조 돌벽", log);
        }

        [MenuItem("Tools/EVERFLOW/선택한 그레이박스에 돌벽 입히기")]
        static void Selected()
        {
            if (Selection.transforms.Length == 0) { EditorUtility.DisplayDialog("돌벽", "그레이박스(상자)를 Hierarchy 에서 선택하고 눌러 주세요. 그룹을 고르면 안의 상자 전부.", "확인"); return; }
            V.Begin("돌벽 입히기", out int g);
            int n = 0, skip = 0;
            foreach (var s in Selection.transforms)
                foreach (var t in s.GetComponentsInChildren<Transform>(true))
                { var mf = t.GetComponent<MeshFilter>(); if (mf == null) continue; if (Skin(t)) n++; else skip++; }
            V.End(g, "돌벽 입히기", new List<string> { "돌벽 입힌 상자 " + n + "개" + (skip > 0 ? ", 상자가 아니라서 건너뜀 " + skip + "개 (원기둥·에셋 등)" : "") });
        }

        [MenuItem("Tools/EVERFLOW/C 돌아오는 다리 → EF_C_Bridge")]
        static void CBridge()
        {
            V.Begin("C 다리", out int g);
            var log = new List<string>();
            var gb = V.Find("C restored return bridge");
            var model = FindModel("EF_C_Bridge");
            if (gb == null || model == null) log.Add(gb == null ? "'C restored return bridge' 없음" : "EF_C_Bridge 모델 없음 → 블렌더 EF_Props_build.py (cbridge) 먼저");
            else
            {
                var old = V.Under(gb, "C_Bridge_Art"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
                var holder = new GameObject("C_Bridge_Art").transform;      // 부모 크기를 되돌리는 빈 오브젝트
                Undo.RegisterCreatedObjectUndo(holder.gameObject, "h");
                holder.SetParent(gb, false);
                var ls = gb.localScale;
                holder.localScale = new Vector3(1 / ls.x, 1 / ls.y, 1 / ls.z);

                var m = (GameObject)PrefabUtility.InstantiatePrefab(model, holder);
                Undo.RegisterCreatedObjectUndo(m, "m");
                m.transform.position = Vector3.zero; m.transform.rotation = Quaternion.identity;
                Bounds mb = Bnd(m);
                Vector3 c = mb.center - m.transform.position;
                Vector3 mdir = Mathf.Abs(c.x) >= Mathf.Abs(c.z) ? new Vector3(Mathf.Sign(c.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(c.z));
                float mlen = Mathf.Max(mb.size.x, mb.size.z);

                Vector3 s = Abs(gb.lossyScale);
                int la = s.x >= s.z ? 0 : 2;
                Vector3 along = (la == 0 ? gb.right : gb.forward).normalized;
                float len = la == 0 ? s.x : s.z;
                Vector3 start = gb.position - along * (len / 2) + gb.up * (s.y / 2);
                m.transform.rotation = Quaternion.LookRotation(along, gb.up) * Quaternion.Inverse(Quaternion.LookRotation(mdir, Vector3.up));   // 다리판 윗면 = 회색 상자 윗면
                m.transform.position = start;
                float k = len / Mathf.Max(mlen, .01f);
                m.transform.localScale = Vector3.one * 1f;
                if (Mathf.Abs(k - 1) > .03f) m.transform.localScale = new Vector3(k, 1, k);   // 길이 맞춤 (모델은 13.06m)
                var plank = V.Mat("WoodenWall");
                if (plank != null) foreach (var r in m.GetComponentsInChildren<Renderer>()) r.sharedMaterials = Enumerable.Repeat(plank, r.sharedMaterials.Length).ToArray();
                foreach (var r in gb.GetComponents<MeshRenderer>()) { Undo.RecordObject(r, "hide"); r.enabled = false; }
                log.Add("C 돌아오는 다리: 나무 판 다리로 (회색 상자는 숨김, 움직임·충돌은 그대로)");
            }
            V.End(g, "C 다리", log);
        }

        // ---------------------------------------------------------------- 돌 상자 메시
        static bool Skin(Transform t)
        {
            var mf = t.GetComponent<MeshFilter>(); var mr = t.GetComponent<MeshRenderer>();
            if (mf == null || mr == null || mf.sharedMesh == null) return false;
            string mn = mf.sharedMesh.name;
            if (mn != "Cube" && !mn.StartsWith("Stone_")) return false;
            Vector3 s = Abs(t.lossyScale);
            var mesh = GetMesh(s);
            Undo.RecordObject(mf, "skin"); mf.sharedMesh = mesh;
            var mat = V.Mat("Windmill");
            if (mat != null) { Undo.RecordObject(mr, "mat"); mr.sharedMaterials = new[] { mat }; mr.enabled = true; }
            return true;
        }

        static Mesh GetMesh(Vector3 s)
        {
            string key = string.Format("Stone_{0:0.##}x{1:0.##}x{2:0.##}", s.x, s.y, s.z);
            string path = MeshDir + "/" + key + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            mesh = Build(s); mesh.name = key;
            Directory.CreateDirectory(MeshDir);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Mesh Build(Vector3 s)
        {
            var vs = new List<Vector3>(); var ns = new List<Vector3>(); var us = new List<Vector2>(); var ts = new List<int>();
            // (법선, u축, v축)
            var faces = new (Vector3 n, Vector3 u, Vector3 v)[]
            {
                (Vector3.right, Vector3.forward, Vector3.up), (Vector3.left, Vector3.back, Vector3.up),
                (Vector3.forward, Vector3.left, Vector3.up), (Vector3.back, Vector3.right, Vector3.up),
                (Vector3.up, s.x >= s.z ? Vector3.right : Vector3.forward, s.x >= s.z ? Vector3.forward : Vector3.left),
                (Vector3.down, s.x >= s.z ? Vector3.right : Vector3.forward, s.x >= s.z ? Vector3.back : Vector3.right),
            };
            foreach (var f in faces)
            {
                float Lu = Mathf.Abs(Vector3.Dot(s, f.u)), Lv = Mathf.Abs(Vector3.Dot(s, f.v));
                bool top = f.n == Vector3.up;
                float cw = top ? CapLen : PW, ch = top ? CapRow : PH;
                Vector4 reg = top ? Cap : Wall;
                int row = 0;
                for (float v0 = 0; v0 < Lv - 1e-4f; v0 += ch, row++)
                {
                    float v1 = Mathf.Min(v0 + ch, Lv);
                    float shift = (row % 2) * cw * .5f;
                    for (float u0 = -shift; u0 < Lu - 1e-4f; u0 += cw)
                    {
                        float a = Mathf.Max(u0, 0), b = Mathf.Min(u0 + cw, Lu);
                        if (b - a < 1e-4f) continue;
                        int i0 = vs.Count;
                        foreach (var (uu, vv) in new[] { (a, v0), (b, v0), (b, v1), (a, v1) })
                        {
                            Vector3 p = f.n * .5f + Div(f.u * (uu / Lu - .5f) + f.v * (vv / Lv - .5f), 1);
                            vs.Add(p); ns.Add(f.n);
                            float fu = (uu - u0) / cw, fv = (vv - v0) / ch;
                            us.Add(new Vector2(reg.x + fu * (reg.z - reg.x), reg.y + fv * (reg.w - reg.y)));
                        }
                        bool outward = Vector3.Dot(Vector3.Cross(f.u, f.v), f.n) > 0;
                        if (outward) ts.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
                        else ts.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
                    }
                }
            }
            var m = new Mesh();
            if (vs.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(vs); m.SetNormals(ns); m.SetUVs(0, us); m.SetTriangles(ts, 0);
            m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }

        static Vector3 Div(Vector3 a, float k) => a / k;
        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static Bounds Bnd(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>();
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(g.transform.position, Vector3.zero);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static GameObject FindModel(string name)
        {
            var p = AssetDatabase.FindAssets(name + " t:Model").Select(AssetDatabase.GUIDToAssetPath)
                .Where(x => Path.GetFileNameWithoutExtension(x) == name).FirstOrDefault();
            return p != null ? AssetDatabase.LoadAssetAtPath<GameObject>(p) : null;
        }
    }
}
#endif
