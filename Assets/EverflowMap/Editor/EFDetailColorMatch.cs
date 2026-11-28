// Assets/EverflowMap/Editor/EFDetailColorMatch.cs
// 메뉴: EVERFLOW > 디테일 풀 색을 지형 풀 색에 맞추기
//
// 지형 풀 레이어(이름에 grass)의 평균 색(× Color Tint)을 구하고,
// 각 디테일 메시(풀 프리팹)의 텍스처가 모델에서 실제로 쓰이는 부분의 평균 색을 구해서
// Healthy Color = 지형 풀 색 ÷ 풀 모델 색  이 되도록 맞춥니다. (Render Mode = Grass 일 때만 적용되는 색)
// Dry Color 는 Healthy 를 살짝 어둡고 노랗게 해서 얼룩(변화)용으로 넣습니다.
// - Project 창에서 텍스처를 선택하고 실행하면, 지형 대신 그 텍스처 색을 목표로 씀
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text;

public static class EFDetailColorMatch
{
    const string GrassKey = "grass";
    const float Variation = 0.12f;     // Dry Color 를 Healthy 대비 얼마나 다르게 (0 = 똑같음)

    [MenuItem("EVERFLOW/디테일 풀 색을 지형 풀 색에 맞추기")]
    static void Run()
    {
        var sb = new StringBuilder();
        var selTex = Selection.activeObject as Texture2D;
        foreach (var t in Terrain.activeTerrains)
        {
            var td = t.terrainData;
            var protos = td.detailPrototypes;
            if (protos.Length == 0) continue;

            // 목표 색
            Color target; string targetName;
            if (selTex) { target = Avg(Read(selTex), null); targetName = selTex.name + " (선택한 텍스처)"; }
            else
            {
                var layer = td.terrainLayers.FirstOrDefault(l => l && l.name.ToLower().Contains(GrassKey) && !l.name.ToLower().Contains("dry"));
                if (!layer || !layer.diffuseTexture) { sb.AppendLine($"{t.name}: 풀 지형 레이어 없음"); continue; }
                target = Avg(Read(layer.diffuseTexture), null);
                Vector4 tint = layer.diffuseRemapMax;
                target = new Color(target.r * tint.x, target.g * tint.y, target.b * tint.z, 1);
                targetName = layer.name;
            }
            sb.AppendLine($"[{t.name}] 목표: {targetName} {Hex(target)}");

            Undo.RegisterCompleteObjectUndo(td, "디테일 풀 색 맞추기");
            for (int i = 0; i < protos.Length; i++)
            {
                var p = protos[i];
                if (!p.usePrototypeMesh || !p.prototype) { sb.AppendLine($"  {i}: 텍스처 디테일이라 건너뜀"); continue; }
                var mf = p.prototype.GetComponentInChildren<MeshFilter>();
                var mr = p.prototype.GetComponentInChildren<MeshRenderer>();
                if (!mf || !mf.sharedMesh || !mr || !mr.sharedMaterial) { sb.AppendLine($"  {p.prototype.name}: 메시/재질 없음"); continue; }
                var mat = mr.sharedMaterial;
                var tex = (mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : mat.mainTexture) as Texture2D;
                Color baseCol = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : (mat.HasProperty("_Color") ? mat.color : Color.white);
                Color src = Color.white;
                if (tex) src = Avg(Read(tex, out int w, out int h), mf.sharedMesh, w, h);
                src = new Color(src.r * baseCol.r, src.g * baseCol.g, src.b * baseCol.b, 1);

                // Healthy = 목표 / 원래색 (1을 넘으면 전체를 같은 비율로 낮춤 → 색조 유지)
                Vector3 k = new Vector3(target.r / Mathf.Max(src.r, 0.02f), target.g / Mathf.Max(src.g, 0.02f), target.b / Mathf.Max(src.b, 0.02f));
                float mx = Mathf.Max(k.x, Mathf.Max(k.y, k.z));
                bool clipped = mx > 1f;
                if (clipped) k /= mx;
                var healthy = new Color(k.x, k.y, k.z, 1);
                Color.RGBToHSV(healthy, out float hh, out float ss, out float vv);
                var dry = Color.HSVToRGB(Mathf.Repeat(hh - Variation * 0.15f, 1f), Mathf.Clamp01(ss * (1f + Variation)), vv * (1f - Variation));

                p.healthyColor = healthy; p.dryColor = dry;
                protos[i] = p;
                sb.AppendLine($"  {p.prototype.name}: 모델색 {Hex(src)} → Healthy {Hex(healthy)}, Dry {Hex(dry)}" +
                              (clipped ? "  (모델이 목표보다 어두워서 완전히 못 맞춤 → 재질 Base Color를 밝게)" : "") +
                              (p.renderMode != DetailRenderMode.Grass || p.useInstancing ? "  ※ Grass 모드 + Instancing 꺼짐에서만 이 색이 보여요" : ""));
            }
            td.detailPrototypes = protos;
            td.RefreshPrototypes();
            EditorUtility.SetDirty(td);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[EFDetailColorMatch]\n" + sb);
        EditorUtility.DisplayDialog("디테일 풀 색 맞추기", sb.Length > 0 ? sb.ToString() : "디테일이 있는 지형이 없어요.", "확인");
    }

    static Color[] Read(Texture2D t) => Read(t, out _, out _);
    static Color[] Read(Texture2D t, out int w, out int h)
    {
        w = t.width; h = t.height;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active; Graphics.Blit(t, rt); RenderTexture.active = rt;
        var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0); tmp.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        var px = tmp.GetPixels(); Object.DestroyImmediate(tmp); return px;
    }

    // mesh 가 있으면 모델 UV가 가리키는 픽셀만(불투명한 것) 평균
    static Color Avg(Color[] px, Mesh mesh, int w = 0, int h = 0)
    {
        Vector3 s = Vector3.zero; float n = 0;
        if (mesh != null && w > 0)
        {
            Vector2[] uv = null;
            try { uv = mesh.uv; } catch { }
            var tri = mesh.triangles;
            if (uv != null && uv.Length > 0 && tri.Length > 0)
            {
                var rnd = new System.Random(1);
                for (int i = 0; i < tri.Length; i += 3)
                    for (int k = 0; k < 6; k++)
                    {
                        float a = (float)rnd.NextDouble(), b = (float)rnd.NextDouble();
                        if (a + b > 1) { a = 1 - a; b = 1 - b; }
                        Vector2 u = uv[tri[i]] + (uv[tri[i + 1]] - uv[tri[i]]) * a + (uv[tri[i + 2]] - uv[tri[i]]) * b;
                        int x = Mathf.Clamp((int)(Mathf.Repeat(u.x, 1f) * w), 0, w - 1), y = Mathf.Clamp((int)(Mathf.Repeat(u.y, 1f) * h), 0, h - 1);
                        var c = px[y * w + x];
                        if (c.a < 0.4f) continue;
                        s += new Vector3(c.r, c.g, c.b); n++;
                    }
                if (n > 0) return new Color(s.x / n, s.y / n, s.z / n, 1);
            }
        }
        for (int i = 0; i < px.Length; i += Mathf.Max(1, px.Length / 100000))
        {
            var c = px[i]; if (c.a < 0.4f) continue;
            s += new Vector3(c.r, c.g, c.b); n++;
        }
        return n > 0 ? new Color(s.x / n, s.y / n, s.z / n, 1) : Color.white;
    }

    static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
}
