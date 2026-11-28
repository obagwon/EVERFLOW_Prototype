// Assets/EverflowMap/Editor/EFDStoneStairs.cs
// 메뉴: EVERFLOW > D 나무 계단 → 돌계단으로 교체
//
// "D B | Raised timber walk" 의 나무 계단·데크·다리 범위(바운딩)를 재서
// EF_D_StoneStairs.fbx 모델을 같은 범위에 맞춰 넣고, 메시 콜라이더를 붙인 뒤, 옛 나무 조각들을 끕니다(삭제 X).
// 남기는 것: D station 2 wind mark, D station 2 chime frame(종), D chime stone base, D chime tower timber cap
// 다시 누르면 돌계단만 지우고 같은 자리에 새로 놓음 (모델 다시 뽑았을 때).
// 되돌리기: Ctrl+Z  /  "D 돌계단 → 나무 계단 되돌리기" 메뉴
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;

public static class EFDStoneStairs
{
    const string GroupName = "D B | Raised timber walk";
    const string ModelName = "EF_D_StoneStairs";
    static readonly string[] OldPrefixes = { "D stair tread", "D lookout plank", "D stair timber leg", "D raised deck joist", "D raised walk support" };

    [MenuItem("EVERFLOW/D 나무 계단 → 돌계단으로 교체")]
    static void Replace()
    {
        var group = FindAll().FirstOrDefault(t => t.name == GroupName);
        if (!group) { Msg($"'{GroupName}'를 못 찾았어요."); return; }

        var model = AssetDatabase.FindAssets(ModelName + " t:Model").Select(AssetDatabase.GUIDToAssetPath)
            .Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p)).FirstOrDefault(g => g && g.name == ModelName);
        if (!model) { Msg($"{ModelName}.fbx 를 Assets 안에서 못 찾았어요. (예: Assets/EverflowMap/Models/)"); return; }

        var old = group.Cast<Transform>().Where(c => OldPrefixes.Any(p => c.name.StartsWith(p))).ToList();
        var prev = group.Find(ModelName);

        // 1) 맞출 범위
        Bounds bb;
        if (prev) bb = RenderBounds(prev.gameObject);          // 다시 놓기: 이전 돌계단 자리
        else
        {
            if (old.Count == 0 || old.Any(o => !o.gameObject.activeInHierarchy)) { Msg("옛 나무 계단 조각이 꺼져 있거나 없어요. 먼저 '되돌리기'를 눌러 켜 주세요."); return; }
            // 가로·깊이·윗면 = 밟는 판(tread, lookout plank)의 콜라이더, 바닥 = 다리·받침 맨 아래
            bb = new Bounds(); bool first = true; float bottom = float.MaxValue;
            foreach (var o in old)
            {
                if (o.name.StartsWith("D stair tread") || o.name.StartsWith("D lookout plank"))
                    foreach (var c in o.GetComponentsInChildren<Collider>())
                    { if (first) { bb = c.bounds; first = false; } else bb.Encapsulate(c.bounds); }
                foreach (var r in o.GetComponentsInChildren<Renderer>()) bottom = Mathf.Min(bottom, r.bounds.min.y);
            }
            if (first) { Msg("나무 계단 발판 콜라이더를 못 찾았어요."); return; }
            if (bottom < bb.min.y) { var mn = bb.min; mn.y = bottom; bb.SetMinMax(mn, bb.max); }
        }

        Undo.SetCurrentGroupName("D 돌계단 교체");
        if (prev) Undo.DestroyObjectImmediate(prev.gameObject);

        // 2) 모델 넣기
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model, group);
        Undo.RegisterCreatedObjectUndo(go, "D 돌계단");
        go.name = ModelName;
        go.transform.localScale = Vector3.one;
        go.transform.rotation = Quaternion.identity;
        go.transform.position = Vector3.zero;

        // 방향: Front → Back 이 월드 +Z(계단 올라가는 쪽), West 가 -X
        var front = Deep(go.transform, "Front"); var back = Deep(go.transform, "Back"); var west = Deep(go.transform, "West");
        if (front && back)
        {
            Vector3 d = back.position - front.position; d.y = 0;
            go.transform.rotation = Quaternion.FromToRotation(d.normalized, Vector3.forward);
            if (west)
            {
                Vector3 mid = (front.position + back.position) * 0.5f;
                if (west.position.x - mid.x > 0)  // 좌우가 뒤집혀 들어온 경우
                {
                    var s = go.transform.localScale; s.x = -s.x; go.transform.localScale = s;
                }
            }
        }

        // 3) 범위에 맞추기 (가로·깊이는 가운데 맞춤, 바닥은 아래 맞춤)
        var mb = RenderBounds(go);
        Vector3 shift = new Vector3(bb.center.x - mb.center.x, bb.min.y - mb.min.y, bb.center.z - mb.center.z);
        go.transform.position += shift;

        // 4) 재질 = 풍차 돌과 같은 재질
        var mat = StoneMaterial();
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();

        // 5) 콜라이더
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            var mc = mf.GetComponent<MeshCollider>();
            if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
        }
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

        // 6) 옛 나무 조각 끄기
        foreach (var o in old)
        {
            Undo.RecordObject(o.gameObject, "D 돌계단");
            o.gameObject.SetActive(false);
        }

        var nb = RenderBounds(go);
        Selection.activeGameObject = go;
        Msg($"돌계단 배치 완료\n범위(옛 계단): 가로 {bb.size.x:F2} × 깊이 {bb.size.z:F2}, 바닥 {bb.min.y:F2}, 위 {bb.max.y:F2}\n" +
            $"새 모델: 가로 {nb.size.x:F2} × 깊이 {nb.size.z:F2}\n옛 나무 조각 {old.Count}개 꺼짐 (지우지 않음)\n\n" +
            "플레이해서 계단을 올라 wind mark 위에서 2번 종이 쳐지는지 확인해 주세요.");
    }

    [MenuItem("EVERFLOW/D 돌계단 → 나무 계단 되돌리기")]
    static void Revert()
    {
        var group = FindAll().FirstOrDefault(t => t.name == GroupName);
        if (!group) return;
        Undo.SetCurrentGroupName("D 나무 계단 되돌리기");
        var stone = group.Find(ModelName);
        if (stone) Undo.DestroyObjectImmediate(stone.gameObject);
        foreach (Transform c in group)
            if (OldPrefixes.Any(p => c.name.StartsWith(p))) { Undo.RecordObject(c.gameObject, "되돌리기"); c.gameObject.SetActive(true); }
    }

    // ───────── 도우미 ─────────
    static IEnumerable<Transform> FindAll() =>
        Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

    static Transform Deep(Transform t, string n)
    {
        if (t.name == n) return t;
        foreach (Transform c in t) { var r = Deep(c, n); if (r) return r; }
        return null;
    }

    static Bounds RenderBounds(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        var b = rs.Length > 0 ? rs[0].bounds : new Bounds(g.transform.position, Vector3.zero);
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Material StoneMaterial()
    {
        // 풍차가 쓰는 Windmill 텍스처 재질을 그대로 재사용
        var tex = AssetDatabase.FindAssets("Windmill t:Texture2D").Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => System.IO.Path.GetFileNameWithoutExtension(p) == "Windmill")
            .Select(p => AssetDatabase.LoadAssetAtPath<Texture2D>(p)).FirstOrDefault();
        if (tex)
        {
            foreach (var g in AssetDatabase.FindAssets("t:Material"))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (m && m.mainTexture == tex && m.shader && m.shader.name.Contains("Universal")) return m;
            }
        }
        const string path = "Assets/EverflowMap/Materials/EF_D_Stone.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat)
        {
            if (!AssetDatabase.IsValidFolder("Assets/EverflowMap")) AssetDatabase.CreateFolder("Assets", "EverflowMap");
            if (!AssetDatabase.IsValidFolder("Assets/EverflowMap/Materials")) AssetDatabase.CreateFolder("Assets/EverflowMap", "Materials");
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (tex) mat.SetTexture("_BaseMap", tex);
            mat.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    static void Msg(string m) => EditorUtility.DisplayDialog("D 돌계단", m, "확인");
}
