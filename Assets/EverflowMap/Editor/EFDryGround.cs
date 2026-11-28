// Assets/EverflowMap/Editor/EFDryGround.cs
// 메뉴: EVERFLOW > 시든 바닥 (마른 풀 레이어)
//
// 지형 레이어가 4칸까지만 되니까, 안 쓰는 칸 하나를 "마른 바닥" 레이어로 갈아 끼웁니다.
// 마른 바닥 텍스처 = 지금 초록 풀 텍스처의 무늬 그대로 + 색만 채도 낮은 베이지.
// ① 만들기: 고른 칸을 마른 바닥으로 교체 (+ 지금 초록 풀 칠한 곳을 전부 마른 바닥으로 = 게임 시작 상태)
//    이미 만들었으면 텍스처 색만 다시 만듦 (색 바꿔 가며 여러 번 눌러도 됨)
// ② / ③ 편집할 때 초록으로 봤다가 다시 마른 바닥으로
// 모든 작업은 Ctrl+Z 로 되돌릴 수 있음. 원본 텍스처·레이어 파일은 안 건드림.
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class EFDryGround : EditorWindow
{
    const string OutDir = "Assets/EverflowMap/Terrain";
    const string DryName = "layer_ground_dry";

    Color dryColor = new Color32(0xA6, 0x9C, 0x86, 0xFF);
    float contrast = 0.85f;   // 무늬 대비
    float keepHue = 0.15f;    // 원래 색 차이를 얼마나 남길지
    int replaceIdx = -1;
    bool moveOldToGreen = true;
    bool greenToDry = true;

    [MenuItem("EVERFLOW/시든 바닥 (마른 풀 레이어)")]
    static void Open() { var w = GetWindow<EFDryGround>(true, "시든 바닥"); w.minSize = new Vector2(380, 330); }

    void OnGUI()
    {
        var t = MainTerrain();
        if (!t) { EditorGUILayout.HelpBox("풀 레이어(이름에 grass)가 있는 Terrain이 없어요.", MessageType.Warning); return; }
        var ls = t.terrainData.terrainLayers;
        var green = Green(ls);
        var dry = ls.FirstOrDefault(l => l && l.name == DryName);

        EditorGUILayout.LabelField("초록 풀 레이어", green ? green.name : "(없음)");
        GUILayout.Space(4);
        dryColor = EditorGUILayout.ColorField("마른 바닥 색", dryColor);
        contrast = EditorGUILayout.Slider(new GUIContent("무늬 대비", "1 = 원본 무늬 그대로"), contrast, 0.3f, 1.3f);
        keepHue = EditorGUILayout.Slider(new GUIContent("원래 색 남김", "0 = 완전히 한 색 톤, 높을수록 원래 얼룩 색이 남음"), keepHue, 0f, 0.6f);
        GUILayout.Space(6);

        if (!dry)
        {
            var names = ls.Select((l, i) => $"{i}: {(l ? l.name : "(빈 칸)")}").ToArray();
            if (replaceIdx < 0) replaceIdx = System.Array.FindIndex(ls, l => l && l != green && !l.name.ToLower().Contains("rock"));
            replaceIdx = EditorGUILayout.Popup("갈아 끼울 칸", Mathf.Clamp(replaceIdx, 0, ls.Length - 1), names);
            var target = ls.Length > 0 ? ls[Mathf.Clamp(replaceIdx, 0, ls.Length - 1)] : null;
            if (target == green) EditorGUILayout.HelpBox("초록 풀 칸은 고를 수 없어요.", MessageType.Error);
            moveOldToGreen = EditorGUILayout.ToggleLeft("그 칸에 이미 칠해진 부분은 초록 풀로 옮기기", moveOldToGreen);
            greenToDry = EditorGUILayout.ToggleLeft("지금 초록 풀 칠한 곳을 전부 마른 바닥으로 (시작 상태)", greenToDry);
            GUILayout.Space(6);
            GUI.enabled = target != green;
            if (GUILayout.Button("① 마른 바닥 만들기 (칸 교체)", GUILayout.Height(30))) Create(t, green, replaceIdx);
            GUI.enabled = true;
        }
        else
        {
            EditorGUILayout.HelpBox($"마른 바닥 레이어가 {System.Array.IndexOf(ls, dry)}번 칸에 있어요.\n색·대비를 바꾸고 아래 버튼을 누르면 텍스처만 다시 만들어요.", MessageType.Info);
            if (GUILayout.Button("① 색 다시 만들기", GUILayout.Height(30))) { MakeTexture(green, dry); }
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("② 초록으로 보기 (편집용)")) Swap(dry, green);
            if (GUILayout.Button("③ 마른 바닥으로 덮기")) Swap(green, dry);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(8);
            int di = System.Array.IndexOf(ls, dry);
            if (di != 0)
            {
                EditorGUILayout.HelpBox("경계에 초록이 남으면: 이 셰이더는 0번 칸을 '바탕'으로 깔아서, 다른 레이어가 섞이는 경계에 0번 칸 색이 비쳐요. 마른 바닥을 0번 칸으로 옮기면 해결돼요.", MessageType.Warning);
                if (GUILayout.Button("마른 바닥을 0번 칸으로 옮기기 (칠한 건 그대로)")) MoveToSlot0(dry);
            }
        }
    }

    // ───────────────────────────
    static Terrain MainTerrain() => Terrain.activeTerrains.FirstOrDefault(t => Green(t.terrainData.terrainLayers));
    static TerrainLayer Green(TerrainLayer[] ls) => ls.FirstOrDefault(l => l && l.name.ToLower().Contains("grass") && l.name != DryName);

    void Create(Terrain main, TerrainLayer green, int idx)
    {
        var dry = MakeLayer(green);
        var old = main.terrainData.terrainLayers[idx];
        int n = 0;
        foreach (var t in Terrain.activeTerrains)
        {
            var td = t.terrainData; var ls = td.terrainLayers;
            int oi = System.Array.IndexOf(ls, old), gi = System.Array.IndexOf(ls, green);
            if (oi < 0) continue;
            Undo.RegisterCompleteObjectUndo(td, "시든 바닥");
            if (moveOldToGreen && gi >= 0) Transfer(td, oi, gi);
            ls[oi] = dry; td.terrainLayers = ls;
            if (greenToDry && gi >= 0) n += Transfer(td, gi, oi);
            EditorUtility.SetDirty(td);
        }
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("시든 바닥", $"{(old ? old.name : "빈 칸")} 칸 → {DryName}\n마른 바닥으로 바꾼 칸: {n}\n\n색을 바꾸려면 창에서 색을 고르고 '색 다시 만들기'.\n되돌리기: Ctrl+Z", "확인");
    }

    void Swap(TerrainLayer from, TerrainLayer to)
    {
        int n = 0;
        foreach (var t in Terrain.activeTerrains)
        {
            var td = t.terrainData; var ls = td.terrainLayers;
            int f = System.Array.IndexOf(ls, from), d = System.Array.IndexOf(ls, to);
            if (f < 0 || d < 0) continue;
            Undo.RegisterCompleteObjectUndo(td, "시든 바닥 전환");
            n += Transfer(td, f, d); EditorUtility.SetDirty(td);
        }
        AssetDatabase.SaveAssets();
        ShowNotification(new GUIContent($"{to.name}(으)로 {n}칸"));
    }

    // 레이어 칸 순서만 바꿈 (칠한 무게도 같이 옮겨서 화면 결과는 그대로)
    static void MoveToSlot0(TerrainLayer layer)
    {
        foreach (var t in Terrain.activeTerrains)
        {
            var td = t.terrainData; var ls = td.terrainLayers;
            int i = System.Array.IndexOf(ls, layer);
            if (i <= 0) continue;
            Undo.RegisterCompleteObjectUndo(td, "마른 바닥 0번 칸으로");
            int res = td.alphamapResolution;
            var a = td.GetAlphamaps(0, 0, res, res);
            for (int z = 0; z < res; z++) for (int x = 0; x < res; x++)
            { float v = a[z, x, 0]; a[z, x, 0] = a[z, x, i]; a[z, x, i] = v; }
            (ls[0], ls[i]) = (ls[i], ls[0]);
            td.terrainLayers = ls;
            td.SetAlphamaps(0, 0, a);
            EditorUtility.SetDirty(td);
        }
        AssetDatabase.SaveAssets();
    }

    static int Transfer(TerrainData td, int from, int to)
    {
        int res = td.alphamapResolution; var a = td.GetAlphamaps(0, 0, res, res); int n = 0;
        for (int z = 0; z < res; z++) for (int x = 0; x < res; x++)
        { float v = a[z, x, from]; if (v <= 0f) continue; a[z, x, to] += v; a[z, x, from] = 0f; n++; }
        td.SetAlphamaps(0, 0, a); return n;
    }

    TerrainLayer MakeLayer(TerrainLayer green)
    {
        if (!AssetDatabase.IsValidFolder("Assets/EverflowMap")) AssetDatabase.CreateFolder("Assets", "EverflowMap");
        if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/EverflowMap", "Terrain");
        string path = $"{OutDir}/{DryName}.terrainlayer";
        var dry = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (!dry)
        {
            dry = Object.Instantiate(green);            // 타일 크기·노멀 등 초록 풀과 같게
            dry.name = DryName;
            AssetDatabase.CreateAsset(dry, path);
        }
        MakeTexture(green, dry);
        return dry;
    }

    void MakeTexture(TerrainLayer green, TerrainLayer dry)
    {
        var src = green.diffuseTexture;
        if (!src) return;
        var px = Read(src, out int w, out int h);
        Vector3 m = Vector3.zero; foreach (var c in px) m += YCC(c); m /= px.Length;
        Vector3 tg = YCC(dryColor);
        var o = new Color[px.Length];
        for (int i = 0; i < px.Length; i++)
        {
            var y = YCC(px[i]);
            var c = RGB(new Vector3((y.x - m.x) * contrast + tg.x, (y.y - m.y) * keepHue + tg.y, (y.z - m.z) * keepHue + tg.z));
            c.a = px[i].a; o[i] = c;
        }
        string texPath = $"{OutDir}/{DryName}_diffuse.png";
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        tex.SetPixels(o); tex.Apply();
        File.WriteAllBytes(texPath, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath);
        var a = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(src)) as TextureImporter;
        var b = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (b) { b.sRGBTexture = true; b.wrapMode = TextureWrapMode.Repeat; if (a) { b.maxTextureSize = a.maxTextureSize; b.filterMode = a.filterMode; b.anisoLevel = a.anisoLevel; } b.SaveAndReimport(); }
        Undo.RecordObject(dry, "시든 바닥 색");
        dry.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        dry.diffuseRemapMin = Vector4.zero; dry.diffuseRemapMax = Vector4.one;
        EditorUtility.SetDirty(dry); AssetDatabase.SaveAssets();
        foreach (var t in Terrain.activeTerrains) t.Flush();
    }

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
    static Vector3 YCC(Color c) => new Vector3(0.299f * c.r + 0.587f * c.g + 0.114f * c.b,
        -0.168736f * c.r - 0.331264f * c.g + 0.5f * c.b, 0.5f * c.r - 0.418688f * c.g - 0.081312f * c.b);
    static Color RGB(Vector3 y) => new Color(Mathf.Clamp01(y.x + 1.402f * y.z),
        Mathf.Clamp01(y.x - 0.344136f * y.y - 0.714136f * y.z), Mathf.Clamp01(y.x + 1.772f * y.y), 1f);
}
