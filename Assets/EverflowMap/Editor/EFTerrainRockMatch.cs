// Assets/EverflowMap/Editor/EFTerrainRockMatch.cs
// 메뉴
//   EVERFLOW > 지형 돌 레이어 색 맞추기 (풍차·탑 돌)
//   EVERFLOW > 지형 돌 레이어 원래대로
//
// 지형 레이어 layer_ground_rock 의 텍스처 색을
// FantasyEnvironments 바위(Rock1) 텍스처 색에 맞춘 "새 텍스처"를 만들어 바꿔 끼웁니다.
// - 원본 텍스처 파일은 건드리지 않음 (되돌리기 가능)
// - 무늬·요철은 그대로, 색(평균 밝기·대비·색조)만 맞춤
// - Project 창에서 텍스처를 하나 선택하고 실행하면 그 텍스처 전체를 기준 색으로 씀
//   (예: 바위 오브젝트 텍스처에 맞추고 싶을 때)
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class EFTerrainRockMatch : EditorWindow
{
    const string LayerKey = "ground_rock";     // 이름에 이게 들어간 지형 레이어
    static int RefMode = 0;                    // 0 = FE Rock1, 1 = 풍차·탑 돌벽(Windmill.png 돌 부분)
    static readonly string[] RefModes = { "FE 바위 (Rock1)", "풍차·탑 돌벽 (Windmill.png)" };
    static readonly Rect StoneUV = new Rect(0.032f, 0.318f, 0.250f - 0.032f, 0.483f - 0.318f);
    static float Strength = 1.0f;              // 0 = 원본, 1 = 완전히 맞춤
    static float Brightness = 1.0f;            // 결과 밝기 배율
    const string RefTexName = "Rock1";          // FE 바위 텍스처 이름
    const float SkipDark = 0.04f;              // 이보다 어두운 픽셀(UV 바깥 검은 배경)은 기준에서 제외
    const string OutDir = "Assets/EverflowMap/Terrain";

    [MenuItem("EVERFLOW/지형 돌 레이어 색 맞추기 (FE 바위)")]
    static void Open()
    {
        Strength = EditorPrefs.GetFloat("EFTRM_Strength", 1f);
        RefMode = EditorPrefs.GetInt("EFTRM_RefMode", 0);
        Brightness = EditorPrefs.GetFloat("EFTRM_Brightness", 1f);
        var w = GetWindow<EFTerrainRockMatch>(true, "지형 돌 색 맞추기");
        w.minSize = new Vector2(340, 190);
    }

    void OnGUI()
    {
        GUILayout.Space(6);
        EditorGUILayout.LabelField("기준 돌 색에 지형 돌 레이어를 맞춥니다.", EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("(Project 창에서 다른 텍스처를 선택해 두면 그걸 기준으로)", EditorStyles.miniLabel);
        GUILayout.Space(6);
        RefMode = EditorGUILayout.Popup("기준", RefMode, RefModes);
        Strength = EditorGUILayout.Slider(new GUIContent("맞추는 정도", "0 = 원본 그대로, 1 = 완전히 맞춤"), Strength, 0f, 1f);
        Brightness = EditorGUILayout.Slider(new GUIContent("밝기", "1 = 기준과 같은 밝기"), Brightness, 0.6f, 1.8f);
        GUILayout.Space(8);
        if (GUILayout.Button("적용", GUILayout.Height(28)))
        {
            EditorPrefs.SetFloat("EFTRM_Strength", Strength);
            EditorPrefs.SetInt("EFTRM_RefMode", RefMode);
            EditorPrefs.SetFloat("EFTRM_Brightness", Brightness);
            Match(false);
        }
        if (GUILayout.Button("원래대로")) Restore();
        GUILayout.Space(10);
        EditorGUILayout.LabelField("씬에서 바위 등을 선택하고 ↓ (같은 재질 쓰는 것 전부 바뀜)", EditorStyles.miniLabel);
        if (GUILayout.Button("선택한 오브젝트 재질도 같은 기준으로 맞추기", GUILayout.Height(24)))
        {
            EditorPrefs.SetFloat("EFTRM_Strength", Strength);
            EditorPrefs.SetFloat("EFTRM_Brightness", Brightness);
            EditorPrefs.SetInt("EFTRM_RefMode", RefMode);
            MatchSelected();
        }
        if (GUILayout.Button("선택한 오브젝트 재질 원래대로")) RestoreSelected();
    }

    // ───────── 선택한 오브젝트 재질 ─────────
    static bool RefStats(out Vector3 rm, out Vector3 rs, out string refName)
    {
        rm = rs = Vector3.zero; Color[] refPx;
        if (RefMode == 1)
        {
            var wm = FindTex("Windmill");
            if (!wm) { Dialog("Windmill 텍스처를 못 찾았어요."); refName = ""; return false; }
            refPx = ReadPixels(wm, StoneUV); refName = "풍차·탑 돌벽";
        }
        else
        {
            var rk = FindTex(RefTexName);
            if (!rk) { Dialog($"{RefTexName} 텍스처를 못 찾았어요."); refName = ""; return false; }
            refPx = ReadPixels(rk, new Rect(0, 0, 1, 1)); refName = "FE " + rk.name;
        }
        refPx = refPx.Where(c => Lum(c) > SkipDark).ToArray();
        if (refPx.Length == 0) { refName = ""; return false; }
        Stats(refPx, out rm, out rs);
        return true;
    }

    static float Lum(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

    static void MatchSelected()
    {
        if (!RefStats(out Vector3 rm, out Vector3 rs, out string refName)) return;
        var mats = Selection.gameObjects.SelectMany(g => g.GetComponentsInChildren<Renderer>())
                    .SelectMany(r => r.sharedMaterials).Where(m => m).Distinct().ToList();
        if (mats.Count == 0) { Dialog("씬에서 바위 같은 오브젝트를 먼저 선택해 주세요."); return; }
        int done = 0; var sb = new System.Text.StringBuilder();
        foreach (var mat in mats)
        {
            string prop = mat.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
            if (!mat.HasProperty(prop)) continue;
            string mkey = "EFTRM_mat_" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mat)) + "_" + mat.name;
            var cur = mat.GetTexture(prop) as Texture2D;
            string og = EditorPrefs.GetString(mkey, "");
            var src = og != "" ? AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(og)) : cur;
            if (!src) continue;
            if (!AssetDatabase.Contains(mat) || AssetDatabase.GetAssetPath(mat).EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            { sb.AppendLine($"{mat.name}: FBX 안 재질이라 못 바꿈 → FBX의 Materials 탭에서 Extract Materials 먼저"); continue; }

            Color[] px = ReadPixels(src, new Rect(0, 0, 1, 1), out int w, out int h);
            var valid = px.Where(c => Lum(c) > SkipDark).ToArray();
            if (valid.Length == 0) continue;
            Stats(valid, out Vector3 sm, out Vector3 ss);
            var outPx = new Color[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                if (Lum(px[i]) <= SkipDark) { outPx[i] = px[i]; continue; }   // UV 바깥 검은 부분은 그대로
                outPx[i] = Transfer(px[i], sm, ss, rm, rs);
            }
            var matched = Save(src, outPx, w, h);
            if (og == "") EditorPrefs.SetString(mkey, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(src)));
            Undo.RecordObject(mat, "재질 색 맞추기");
            mat.SetTexture(prop, matched);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(mat);
            done++; sb.AppendLine($"{mat.name}: 맞춤 (Base Color는 흰색으로)");
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[EFTerrainRockMatch] 기준 {refName}\n" + sb);
        Dialog($"기준: {refName}\n{done}개 재질 맞춤\n\n" + sb);
    }

    static void RestoreSelected()
    {
        var mats = Selection.gameObjects.SelectMany(g => g.GetComponentsInChildren<Renderer>())
                    .SelectMany(r => r.sharedMaterials).Where(m => m).Distinct().ToList();
        int n = 0;
        foreach (var mat in mats)
        {
            string prop = mat.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
            string mkey = "EFTRM_mat_" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mat)) + "_" + mat.name;
            string og = EditorPrefs.GetString(mkey, "");
            var o = og == "" ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(og));
            if (!o) continue;
            Undo.RecordObject(mat, "재질 원래대로");
            mat.SetTexture(prop, o); EditorUtility.SetDirty(mat); EditorPrefs.DeleteKey(mkey); n++;
        }
        AssetDatabase.SaveAssets();
        Dialog($"{n}개 재질을 원래 텍스처로 되돌렸어요.");
    }

    static Color Transfer(Color px, Vector3 sm, Vector3 ss, Vector3 rm, Vector3 rs)
    {
        Vector3 y = ToYCC(px), t;
        t.x = (y.x - sm.x) * (rs.x / Mathf.Max(ss.x, 1e-4f)) + rm.x;
        t.y = (y.y - sm.y) * Mathf.Min(1.5f, rs.y / Mathf.Max(ss.y, 1e-4f)) + rm.y;
        t.z = (y.z - sm.z) * Mathf.Min(1.5f, rs.z / Mathf.Max(ss.z, 1e-4f)) + rm.z;
        t.x *= Brightness;
        Color c = FromYCC(Vector3.Lerp(y, t, Strength)); c.a = px.a;
        return c;
    }

    static Texture2D Save(Texture2D src, Color[] outPx, int w, int h)
    {
        if (!AssetDatabase.IsValidFolder("Assets/EverflowMap")) AssetDatabase.CreateFolder("Assets", "EverflowMap");
        if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/EverflowMap", "Terrain");
        string outPath = $"{OutDir}/{src.name}_matched.png";
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        tex.SetPixels(outPx); tex.Apply();
        File.WriteAllBytes(outPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(outPath);
        CopyImport(AssetDatabase.GetAssetPath(src), outPath);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
    }

    static void Match(bool dialog)
    {
        var layer = FindLayer();
        if (!layer) { Dialog($"이름에 '{LayerKey}'가 들어간 Terrain Layer를 못 찾았어요."); return; }

        // 원본 텍스처 (이미 맞춘 적 있으면 원본 기준으로 다시)
        string key = PrefKey(layer);
        Texture2D src = layer.diffuseTexture;
        string origGuid = EditorPrefs.GetString(key, "");
        if (origGuid != "")
        {
            var o = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(origGuid));
            if (o) src = o;
        }
        if (!src) { Dialog("레이어에 Diffuse 텍스처가 없어요."); return; }

        // 기준 색
        Color[] refPx; string refName;
        var sel = Selection.activeObject as Texture2D;
        if (sel && sel != src && sel != layer.diffuseTexture)
        {
            refPx = ReadPixels(sel, new Rect(0, 0, 1, 1)); refName = sel.name + " (선택한 텍스처 전체)";
        }
        else if (RefMode == 1)
        {
            var wm = FindTex("Windmill");
            if (!wm) { Dialog("FantasyEnvironments의 Windmill 텍스처를 못 찾았어요."); return; }
            refPx = ReadPixels(wm, StoneUV); refName = "풍차·탑 돌벽 (Windmill.png)";
        }
        else
        {
            var rk = FindTex(RefTexName);
            if (!rk) { Dialog($"FantasyEnvironments의 {RefTexName} 텍스처를 못 찾았어요.\nProject 창에서 기준 텍스처를 선택하고 다시 실행해 주세요."); return; }
            refPx = ReadPixels(rk, new Rect(0, 0, 1, 1)); refName = $"FE {rk.name}";
        }

        // 원본 읽기
        Color[] px = ReadPixels(src, new Rect(0, 0, 1, 1), out int w, out int h);
        Stats(px, out Vector3 sm, out Vector3 ss);
        refPx = refPx.Where(c => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b > SkipDark).ToArray();
        if (refPx.Length == 0) { Dialog("기준 텍스처에 쓸 만한 픽셀이 없어요."); return; }
        Stats(refPx, out Vector3 rm, out Vector3 rs);

        // YCbCr 공간에서 평균·편차 맞춤 (밝기 대비는 따라가고 색조는 평균만)
        var outPx = new Color[px.Length];
        for (int i = 0; i < px.Length; i++)
        {
            Vector3 y = ToYCC(px[i]);
            Vector3 t;
            t.x = (y.x - sm.x) * (rs.x / Mathf.Max(ss.x, 1e-4f)) + rm.x;
            t.y = (y.y - sm.y) * Mathf.Min(1.5f, rs.y / Mathf.Max(ss.y, 1e-4f)) + rm.y;
            t.z = (y.z - sm.z) * Mathf.Min(1.5f, rs.z / Mathf.Max(ss.z, 1e-4f)) + rm.z;
            t.x *= Brightness;
            Vector3 m = Vector3.Lerp(y, t, Strength);
            Color c = FromYCC(m); c.a = px[i].a;            // 알파(스무스니스)는 원본 유지
            outPx[i] = c;
        }

        // 저장
        if (!AssetDatabase.IsValidFolder("Assets/EverflowMap")) AssetDatabase.CreateFolder("Assets", "EverflowMap");
        if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/EverflowMap", "Terrain");
        string outPath = $"{OutDir}/{src.name}_matched.png";
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        tex.SetPixels(outPx); tex.Apply();
        File.WriteAllBytes(outPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(outPath);
        CopyImport(AssetDatabase.GetAssetPath(src), outPath);
        var matched = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);

        // 레이어에 끼우기
        if (origGuid == "") EditorPrefs.SetString(key, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(src)));
        Undo.RecordObject(layer, "지형 돌 색 맞추기");
        layer.diffuseTexture = matched;
        layer.diffuseRemapMin = Vector4.zero;
        layer.diffuseRemapMax = Vector4.one;               // Color Tint 흰색으로 초기화
        EditorUtility.SetDirty(layer);
        AssetDatabase.SaveAssets();
        foreach (var t in Terrain.activeTerrains) t.Flush();

        Stats(outPx, out Vector3 om, out _);
        string msg = $"레이어: {layer.name}\n기준: {refName}\n" +
                     $"원본 평균색 {Hex(FromYCC(sm))} → 결과 {Hex(FromYCC(om))} (기준 {Hex(FromYCC(rm))})\n\n" +
                     $"새 텍스처: {outPath}\n원본 파일은 그대로예요. 되돌리려면 '지형 돌 레이어 원래대로'.";
        Debug.Log("[EFTerrainRockMatch] " + msg);
        if (dialog) Dialog(msg);
        else EditorWindow.focusedWindow?.ShowNotification(new GUIContent($"적용됨 → {Hex(FromYCC(om))}"));
    }

    [MenuItem("EVERFLOW/지형 돌 레이어 원래대로")]
    static void Restore()
    {
        var layer = FindLayer();
        if (!layer) { Dialog("레이어를 못 찾았어요."); return; }
        string g = EditorPrefs.GetString(PrefKey(layer), "");
        var o = g == "" ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g));
        if (!o) { Dialog("저장된 원본 기록이 없어요. (이미 원본이거나 다른 PC에서 실행)"); return; }
        Undo.RecordObject(layer, "지형 돌 원래대로");
        layer.diffuseTexture = o;
        EditorUtility.SetDirty(layer);
        AssetDatabase.SaveAssets();
        EditorPrefs.DeleteKey(PrefKey(layer));
        foreach (var t in Terrain.activeTerrains) t.Flush();
        Dialog($"{layer.name} → {o.name} 로 되돌렸어요.");
    }

    // ───────── 도우미 ─────────
    static TerrainLayer FindLayer()
    {
        foreach (var t in Terrain.activeTerrains)
            foreach (var l in t.terrainData.terrainLayers)
                if (l && l.name.ToLower().Contains(LayerKey)) return l;
        foreach (var g in AssetDatabase.FindAssets("t:TerrainLayer"))
        {
            var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(AssetDatabase.GUIDToAssetPath(g));
            if (l && l.name.ToLower().Contains(LayerKey)) return l;
        }
        return null;
    }

    static Texture2D FindTex(string nm)
    {
        var paths = AssetDatabase.FindAssets(nm + " t:Texture2D").Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => Path.GetFileNameWithoutExtension(p) == nm).ToList();
        var p0 = paths.FirstOrDefault(p => p.Contains("FantasyEnvironments")) ?? paths.FirstOrDefault();
        return p0 == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(p0);
    }

    static string PrefKey(TerrainLayer l) => "EFTerrainRockMatch_" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(l));

    static Color[] ReadPixels(Texture2D t, Rect uv) => ReadPixels(t, uv, out _, out _);
    // Read/Write 설정과 관계없이 GPU로 복사해서 읽음 (sRGB 값 그대로)
    static Color[] ReadPixels(Texture2D t, Rect uv, out int w, out int h)
    {
        int W = t.width, H = t.height;
        var rt = RenderTexture.GetTemporary(W, H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;
        Graphics.Blit(t, rt);
        RenderTexture.active = rt;
        int x0 = Mathf.FloorToInt(uv.x * W), y0 = Mathf.FloorToInt(uv.y * H);
        w = Mathf.Max(1, Mathf.FloorToInt(uv.width * W)); h = Mathf.Max(1, Mathf.FloorToInt(uv.height * H));
        var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        tmp.ReadPixels(new Rect(x0, y0, w, h), 0, 0); tmp.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        var px = tmp.GetPixels(); Object.DestroyImmediate(tmp);
        return px;
    }

    static void Stats(Color[] px, out Vector3 mean, out Vector3 std)
    {
        Vector3 s = Vector3.zero, s2 = Vector3.zero; int n = 0;
        int step = Mathf.Max(1, px.Length / 200000);
        for (int i = 0; i < px.Length; i += step)
        {
            var y = ToYCC(px[i]); s += y; s2 += Vector3.Scale(y, y); n++;
        }
        mean = s / n;
        var v = s2 / n - Vector3.Scale(mean, mean);
        std = new Vector3(Mathf.Sqrt(Mathf.Max(v.x, 0)), Mathf.Sqrt(Mathf.Max(v.y, 0)), Mathf.Sqrt(Mathf.Max(v.z, 0)));
    }

    static Vector3 ToYCC(Color c) => new Vector3(
        0.299f * c.r + 0.587f * c.g + 0.114f * c.b,
        -0.168736f * c.r - 0.331264f * c.g + 0.5f * c.b,
        0.5f * c.r - 0.418688f * c.g - 0.081312f * c.b);

    static Color FromYCC(Vector3 y) => new Color(
        Mathf.Clamp01(y.x + 1.402f * y.z),
        Mathf.Clamp01(y.x - 0.344136f * y.y - 0.714136f * y.z),
        Mathf.Clamp01(y.x + 1.772f * y.y), 1f);

    static void CopyImport(string srcPath, string dstPath)
    {
        var a = AssetImporter.GetAtPath(srcPath) as TextureImporter;
        var b = AssetImporter.GetAtPath(dstPath) as TextureImporter;
        if (!b) return;
        b.textureType = TextureImporterType.Default;
        b.sRGBTexture = true;
        b.alphaSource = TextureImporterAlphaSource.FromInput;
        b.mipmapEnabled = true;
        b.wrapMode = TextureWrapMode.Repeat;
        if (a) { b.maxTextureSize = a.maxTextureSize; b.filterMode = a.filterMode; b.anisoLevel = a.anisoLevel; b.textureCompression = a.textureCompression; }
        b.SaveAndReimport();
    }

    static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    static void Dialog(string m) => EditorUtility.DisplayDialog("지형 돌 색 맞추기", m, "확인");
}
