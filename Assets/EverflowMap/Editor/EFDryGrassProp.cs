// Assets/EverflowMap/Editor/EFDryGrassProp.cs
// 메뉴: EVERFLOW > 마른 풀 프롭 만들기 (선택한 풀 프리팹)
//
// 풀 텍스처가 진한 초록(파랑≈0)이라 Healthy Color(곱하기)로는 베이지가 안 나옴.
// → 풀 프리팹의 텍스처를 "무늬는 그대로, 색만 마른 바닥과 같은 회베이지"로 다시 칠한 복사본을 만들고
//   재질·프리팹도 복사본(_Dry)으로 만든 뒤, 지형 디테일 목록에서 원래 풀을 마른 풀로 바꿔 끼움.
// - 원본 텍스처·재질·프리팹은 안 건드림. 디테일 교체는 Ctrl+Z 가능.
// - 나중에 회복 연출 때는 원래(초록) 프리팹으로 다시 바꿔 끼우면 됨.
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Text;

public static class EFDryGrassProp
{
    static readonly Color Target = new Color32(0xA6, 0x9C, 0x86, 0xFF);  // 마른 바닥 색과 같게
    const float Contrast = 1.0f;     // 잎 무늬(밝고 어두운 부분) 대비
    const float KeepHue = 0.08f;     // 원래 색 차이를 남기는 정도
    const float TipLift = 0.10f;     // 밝은 부분(잎 끝)을 살짝 노랗게
    const string OutDir = "Assets/EverflowMap/DryGrass";

    [MenuItem("EVERFLOW/마른 풀 프롭 만들기 (선택한 풀 프리팹)")]
    static void Run()
    {
        var prefabs = Selection.objects.OfType<GameObject>().Where(g => PrefabUtility.IsPartOfPrefabAsset(g)).ToList();
        if (prefabs.Count == 0)
        {
            EditorUtility.DisplayDialog("마른 풀 프롭", "Project 창에서 풀 프리팹(예: Grass1)을 선택하고 실행해 주세요.\n여러 개 선택 가능.", "확인");
            return;
        }
        if (!AssetDatabase.IsValidFolder("Assets/EverflowMap")) AssetDatabase.CreateFolder("Assets", "EverflowMap");
        if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/EverflowMap", "DryGrass");

        var sb = new StringBuilder();
        foreach (var src in prefabs)
        {
            var mr = src.GetComponentInChildren<MeshRenderer>(); var mf = src.GetComponentInChildren<MeshFilter>();
            if (!mr || !mr.sharedMaterial || !mf) { sb.AppendLine($"{src.name}: 메시/재질 없음"); continue; }
            var mat = mr.sharedMaterial;
            string texProp = mat.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
            var tex = mat.GetTexture(texProp) as Texture2D;
            if (!tex) { sb.AppendLine($"{src.name}: 텍스처 없음"); continue; }

            // 1) 텍스처
            var px = Read(tex, out int w, out int h);
            Vector3 m = MeanUsed(px, w, h, mf.sharedMesh);
            Vector3 tg = YCC(Target);
            var o = new Color[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a < 0.02f) { o[i] = c; continue; }
                var y = YCC(c);
                float ly = (y.x - m.x) * Contrast + tg.x;
                float lift = Mathf.Clamp01((y.x - m.x) * 3f) * TipLift;   // 밝은 끝을 살짝 노랗게
                var r = RGB(new Vector3(ly, (y.y - m.y) * KeepHue + tg.y - lift * 0.5f, (y.z - m.z) * KeepHue + tg.z + lift * 0.2f));
                r.a = c.a; o[i] = r;
            }
            string texPath = $"{OutDir}/{tex.name}_dry.png";
            var nt = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            nt.SetPixels(o); nt.Apply(); File.WriteAllBytes(texPath, nt.EncodeToPNG()); Object.DestroyImmediate(nt);
            AssetDatabase.ImportAsset(texPath);
            var ai = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
            var bi = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (bi)
            {
                bi.sRGBTexture = true; bi.alphaSource = TextureImporterAlphaSource.FromInput; bi.alphaIsTransparency = true;
                if (ai) { bi.maxTextureSize = ai.maxTextureSize; bi.filterMode = ai.filterMode; bi.mipmapEnabled = ai.mipmapEnabled; bi.wrapMode = ai.wrapMode; }
                bi.SaveAndReimport();
            }
            var dryTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            // 2) 재질
            string matPath = $"{OutDir}/{mat.name}_Dry.mat";
            var dm = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!dm) { dm = new Material(mat); AssetDatabase.CreateAsset(dm, matPath); }
            else dm.CopyPropertiesFromMaterial(mat);
            dm.SetTexture(texProp, dryTex);
            if (dm.HasProperty("_BaseColor")) dm.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(dm);

            // 3) 프리팹 복사본
            string pfPath = $"{OutDir}/{src.name}_Dry.prefab";
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>())
                r.sharedMaterials = r.sharedMaterials.Select(x => x == mat ? dm : x).ToArray();
            inst.name = src.name + "_Dry";
            var dryPrefab = PrefabUtility.SaveAsPrefabAsset(inst, pfPath);
            Object.DestroyImmediate(inst);

            // 4) 지형 디테일에서 원래 풀 → 마른 풀로 교체
            int swapped = 0;
            foreach (var t in Terrain.activeTerrains)
            {
                var td = t.terrainData; var ps = td.detailPrototypes; bool ch = false;
                for (int i = 0; i < ps.Length; i++)
                    if (ps[i].prototype == src)
                    {
                        if (!ch) Undo.RegisterCompleteObjectUndo(td, "마른 풀 프롭으로 교체");
                        ps[i].prototype = dryPrefab;
                        ps[i].healthyColor = Color.white;
                        ps[i].dryColor = new Color32(0xE2, 0xDA, 0xCC, 0xFF);   // 살짝 어두운 얼룩
                        ch = true; swapped++;
                    }
                if (ch) { td.detailPrototypes = ps; td.RefreshPrototypes(); EditorUtility.SetDirty(td); }
            }
            sb.AppendLine($"{src.name} → {dryPrefab.name} (텍스처 평균 {Hex(RGB(m))} → {Hex(Target)}), 디테일 교체 {swapped}곳");
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[EFDryGrassProp]\n" + sb);
        EditorUtility.DisplayDialog("마른 풀 프롭", sb + "\n밝기는 Edit Details의 Healthy Color(흰색 기준)로 미세 조정하세요.\n이제 텍스처가 무채색 계열이라 Healthy가 제대로 먹어요.", "확인");
    }

    static Vector3 MeanUsed(Color[] px, int w, int h, Mesh mesh)
    {
        Vector3 s = Vector3.zero; float n = 0;
        Vector2[] uv = null; int[] tri = null;
        try { uv = mesh.uv; tri = mesh.triangles; } catch { }
        if (uv != null && uv.Length > 0 && tri != null)
        {
            var rnd = new System.Random(1);
            for (int i = 0; i < tri.Length; i += 3)
                for (int k = 0; k < 8; k++)
                {
                    float a = (float)rnd.NextDouble(), b = (float)rnd.NextDouble();
                    if (a + b > 1) { a = 1 - a; b = 1 - b; }
                    var u = uv[tri[i]] + (uv[tri[i + 1]] - uv[tri[i]]) * a + (uv[tri[i + 2]] - uv[tri[i]]) * b;
                    int x = Mathf.Clamp((int)(Mathf.Repeat(u.x, 1) * w), 0, w - 1), y = Mathf.Clamp((int)(Mathf.Repeat(u.y, 1) * h), 0, h - 1);
                    var c = px[y * w + x]; if (c.a < 0.4f) continue;
                    s += YCC(c); n++;
                }
        }
        if (n == 0) foreach (var c in px) if (c.a >= 0.4f) { s += YCC(c); n++; }
        return n > 0 ? s / n : YCC(Color.gray);
    }

    static Color[] Read(Texture2D t, out int w, out int h)
    {
        w = t.width; h = t.height;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active; Graphics.Blit(t, rt); RenderTexture.active = rt;
        var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0); tmp.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        var p = tmp.GetPixels(); Object.DestroyImmediate(tmp); return p;
    }
    static Vector3 YCC(Color c) => new Vector3(0.299f * c.r + 0.587f * c.g + 0.114f * c.b,
        -0.168736f * c.r - 0.331264f * c.g + 0.5f * c.b, 0.5f * c.r - 0.418688f * c.g - 0.081312f * c.b);
    static Color RGB(Vector3 y) => new Color(Mathf.Clamp01(y.x + 1.402f * y.z),
        Mathf.Clamp01(y.x - 0.344136f * y.y - 0.714136f * y.z), Mathf.Clamp01(y.x + 1.772f * y.y), 1f);
    static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
}
