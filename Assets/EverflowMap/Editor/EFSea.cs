// Assets/EverflowMap/Editor/EFSea.cs
// 메뉴: EVERFLOW > 바다 깔기 (섬 둘레)
// 씬의 Terrain을 찾아 그 중앙에 큰 원형 바다 평면을 만듭니다.
// 높이 = 섬 가장자리에서 가장 낮은 지형 + seaOffset (섬 밑둥이 살짝 잠기게)
// 불투명 바다: 물 밑 지형은 안 보임
// 다시 누르면 기존 EF_Sea를 지우고 새로 만듭니다.
using UnityEngine;
using UnityEditor;
using System.IO;

public static class EFSea
{
    const float seaOffset = 0.4f;     // 가장자리 최저 높이보다 얼마나 위에
    const float radiusMul = 4f;       // 섬 크기 대비 바다 반지름 배수
    const int segments = 128;
    const string dir = "Assets/EverflowMap/Sea";

    [MenuItem("EVERFLOW/바다 깔기 (섬 둘레)")]
    static void Make()
    {
        var terrains = Terrain.activeTerrains;
        if (terrains == null || terrains.Length == 0)
        {
            EditorUtility.DisplayDialog("바다 깔기", "씬에 Terrain이 없어요.", "확인");
            return;
        }

        // 1) 섬 범위 + 가장자리 최저 높이
        Bounds b = new Bounds();
        bool first = true;
        float edgeMinWorld = float.MaxValue;
        foreach (var t in terrains)
        {
            var d = t.terrainData;
            Vector3 p = t.transform.position;
            var tb = new Bounds(p + d.size * 0.5f, d.size);
            if (first) { b = tb; first = false; } else b.Encapsulate(tb);

            int n = 64;
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n;
                float h = Mathf.Min(
                    d.GetInterpolatedHeight(u, 0f), d.GetInterpolatedHeight(u, 1f),
                    d.GetInterpolatedHeight(0f, u), d.GetInterpolatedHeight(1f, u));
                edgeMinWorld = Mathf.Min(edgeMinWorld, h + p.y);
            }
        }
        float seaY = edgeMinWorld + seaOffset;

        float radius = Mathf.Max(b.size.x, b.size.z) * radiusMul;
        Vector3 center = new Vector3(b.center.x, seaY, b.center.z);

        // 2) 기존 바다 제거
        var old = GameObject.Find("EF_Sea");
        if (old) Undo.DestroyObjectImmediate(old);

        // 3) 메시 (원판, 중심에서 바깥으로 링)
        if (!AssetDatabase.IsValidFolder("Assets/EverflowMap")) AssetDatabase.CreateFolder("Assets", "EverflowMap");
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/EverflowMap", "Sea");

        Mesh mesh = BuildDisc(radius, segments, 24);
        string meshPath = dir + "/EF_Sea_Mesh.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);

        // 4) 재질 (있으면 재사용 → 직접 바꾼 색 유지, 불투명으로만 맞춤)
        string matPath = dir + "/EF_Sea_Mat.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(sh);
            mat.SetColor("_BaseColor", new Color32(0x2C, 0x8F, 0x9C, 0xFF));   // 청록 바다
            mat.SetFloat("_Smoothness", 0.85f);
            mat.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        MakeOpaque(mat);
        EditorUtility.SetDirty(mat);

        // 5) 오브젝트
        var go = new GameObject("EF_Sea");
        Undo.RegisterCreatedObjectUndo(go, "바다 깔기");
        go.transform.position = center;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // 콜라이더 없음 → 물 위를 걷지 않음

        // 6) 카메라가 바다 끝을 자르지 않게
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (cam.farClipPlane < radius * 1.2f)
            {
                Undo.RecordObject(cam, "바다 깔기");
                cam.farClipPlane = radius * 1.2f;
            }
        }

        AssetDatabase.SaveAssets();
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        Debug.Log($"[EFSea] 바다 생성: 높이 {seaY:F2}, 반지름 {radius:F0}m. 높이는 EF_Sea의 Position Y로 조절하세요.");
    }

    static Mesh BuildDisc(float radius, int seg, int rings)
    {
        var verts = new Vector3[1 + seg * rings];
        var uvs = new Vector2[verts.Length];
        var tris = new int[seg * 3 + seg * (rings - 1) * 6];
        verts[0] = Vector3.zero; uvs[0] = Vector2.zero;
        for (int r = 0; r < rings; r++)
        {
            // 안쪽은 촘촘, 바깥은 성기게
            float k = (r + 1) / (float)rings;
            float rad = radius * k * k;
            for (int s = 0; s < seg; s++)
            {
                float a = s / (float)seg * Mathf.PI * 2f;
                int i = 1 + r * seg + s;
                verts[i] = new Vector3(Mathf.Cos(a) * rad, 0, Mathf.Sin(a) * rad);
                uvs[i] = new Vector2(verts[i].x, verts[i].z) * 0.05f;
            }
        }
        int t = 0;
        for (int s = 0; s < seg; s++)
        {
            int a = 1 + s, c = 1 + (s + 1) % seg;
            tris[t++] = 0; tris[t++] = c; tris[t++] = a;   // 위를 향하게
        }
        for (int r = 0; r < rings - 1; r++)
            for (int s = 0; s < seg; s++)
            {
                int a = 1 + r * seg + s, b2 = 1 + r * seg + (s + 1) % seg;
                int c = a + seg, d = b2 + seg;
                tris[t++] = a; tris[t++] = b2; tris[t++] = c;
                tris[t++] = b2; tris[t++] = d; tris[t++] = c;
            }
        var m = new Mesh { name = "EF_Sea_Mesh" };
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.vertices = verts; m.uv = uvs; m.triangles = tris;
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // 불투명: 바다 밑 지형이 안 보이게
    static void MakeOpaque(Material m)
    {
        m.SetFloat("_Surface", 0);
        m.SetFloat("_Blend", 0);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        m.SetFloat("_ZWrite", 1);
        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        m.SetOverrideTag("RenderType", "Opaque");
        var c = m.GetColor("_BaseColor"); c.a = 1f; m.SetColor("_BaseColor", c);
    }

    // ───────── 바다 높이 점검 ─────────
    // 섬 발치(가장자리 최저) ~ 물 위에 남아야 할 지점들 중 가장 낮은 곳 사이에서
    // 시안 기준(절벽 절반)에 맞는 추천 높이를 계산하고, 확인 후 EF_Sea에 적용합니다.
    static readonly string[] keepDry = {
        "EFMapPlayer - 1123",      // 시작 석탑 출발점
        "TWR_Ka_Landing",          // 석탑 → 계곡 착지
        "C05 Island",              // 바깥섬
        "C one-way cliff gate",
        "B garden pond",
        "B_Wall_E_Outlet",         // 수조 출수구
        "G01 Windmill approach",   // 풍차 성문 앞
    };

    [MenuItem("EVERFLOW/바다 높이 점검 (추천 높이 적용)")]
    static void Check()
    {
        var terrains = Terrain.activeTerrains;
        if (terrains == null || terrains.Length == 0) { Debug.LogWarning("[EFSea] Terrain 없음"); return; }
        float foot = float.MaxValue;
        foreach (var t in terrains)
        {
            var d = t.terrainData; float py = t.transform.position.y;
            for (int i = 0; i <= 64; i++)
            {
                float u = i / 64f;
                foot = Mathf.Min(foot, py + Mathf.Min(
                    d.GetInterpolatedHeight(u, 0f), d.GetInterpolatedHeight(u, 1f),
                    d.GetInterpolatedHeight(0f, u), d.GetInterpolatedHeight(1f, u)));
            }
        }

        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"섬 발치(가장자리 최저): {foot:F1}");
        float lowest = float.MaxValue; string lowName = "";
        foreach (var n in keepDry)
        {
            Transform hit = null;
            foreach (var tr in all) if (tr.name == n) { hit = tr; break; }
            if (!hit) { sb.AppendLine($"  {n}: (없음)"); continue; }
            float y = hit.position.y;
            sb.AppendLine($"  {n}: {y:F1}");
            if (y < lowest) { lowest = y; lowName = n; }
        }
        if (lowest == float.MaxValue) lowest = foot + 20f;

        float half = (foot + lowest) * 0.5f;          // 발치 ~ 가장 낮은 플레이 지점의 중간
        float rec = Mathf.Min(half, lowest - 1.5f);   // 최소 1.5m 여유
        sb.AppendLine($"물 위에 남아야 할 가장 낮은 곳: {lowName} ({lowest:F1})");
        sb.AppendLine($"추천 바다 높이: {rec:F1}  (최대 {lowest - 1.5f:F1})");

        var sea = GameObject.Find("EF_Sea");
        if (sea) sb.AppendLine($"현재 EF_Sea 높이: {sea.transform.position.y:F1}");
        Debug.Log("[EFSea] " + sb);

        if (!sea) { EditorUtility.DisplayDialog("바다 높이 점검", sb + "\nEF_Sea가 없어요. 먼저 '바다 깔기'를 눌러 주세요.", "확인"); return; }
        if (EditorUtility.DisplayDialog("바다 높이 점검", sb.ToString(), $"{rec:F1}로 적용", "그대로 두기"))
        {
            Undo.RecordObject(sea.transform, "바다 높이");
            var p = sea.transform.position; p.y = rec; sea.transform.position = p;
        }
    }
}
