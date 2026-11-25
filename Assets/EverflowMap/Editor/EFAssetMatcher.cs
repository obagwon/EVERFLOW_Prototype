// EVERFLOW - 그레이박스 ↔ 에셋 매칭 창
// 위치: Assets/EverflowMap/Editor/ 아래 (Editor 폴더 안)
// 메뉴: Tools > EVERFLOW > 에셋 매칭 창
//
// 왼쪽: 씬의 그레이박스 목록 (구역 · 종류 · 퍼즐에서 하는 일). 계단·꽃가루처럼 여러 조각은 묶음 하나로 표시
// 오른쪽: Swamp · Stylized Foliage · FantasyEnvironments 프리팹 썸네일
// 사용: 왼쪽에서 오브젝트 선택 → 오른쪽에서 프리팹 클릭 → [적용]
//  - 원래 오브젝트는 그대로 두고 렌더러만 끈 뒤, 모델을 자식 "Asset_…"으로 넣음 → 퍼즐 스크립트·콜라이더 유지
//  - 움직이는 장치도 모델이 자식이라 같이 움직임
//  - 모든 작업은 Ctrl+Z로 되돌릴 수 있음

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowMap.EditorTools
{
    public sealed class EFAssetMatcher : EditorWindow
    {
        const string CourseRoot = "EVERFLOW | Six Collectible Courses";
        const string OldRoot = "EVERFLOW_Greybox_v25";
        const string Prefix = "Asset_";
        static readonly string[] OldPrefixes = { "Asset_", "Swamp_", "Visual_" };

        static readonly string[] PackNames = { "전체", "Swamp", "Stylized Foliage", "FantasyEnvironments" };
        static readonly string[] PackRoots = { "", "SwampArch", "Stylized Foliage Pack", "FantasyEnvironments" };

        enum Fit { 원래크기, 비율유지, 늘려서맞춤, 데크 }

        sealed class Slot
        {
            public GameObject go;
            public string zone, kind, role;
            public bool group;
        }

        List<Slot> slots = new List<Slot>();
        List<GameObject> prefabs = new List<GameObject>();
        Slot selected;
        GameObject picked;
        Vector2 leftScroll, rightScroll;
        string slotSearch = "", prefabSearch = "";
        int pack, zoneFilter;
        string[] zones = { "전체" };
        Fit fit = Fit.비율유지;
        bool autoRotate = true, hideDone;
        float thumb = 96;
        GUIStyle wrap, small;

        [MenuItem("Tools/EVERFLOW/에셋 매칭 창")]
        static void Open() => GetWindow<EFAssetMatcher>("에셋 매칭").minSize = new Vector2(900, 500);

        void OnEnable()
        {
            AssetPreview.SetPreviewTextureCacheSize(2048);
            Refresh();
        }

        void OnHierarchyChange() => Repaint();

        void Refresh()
        {
            BuildSlots();
            LoadPrefabs();
            zones = new[] { "전체" }.Concat(slots.Select(s => s.zone).Distinct()).ToArray();
            zoneFilter = Mathf.Clamp(zoneFilter, 0, zones.Length - 1);
        }

        // ───────────────────────── 화면 ─────────────────────────
        void OnGUI()
        {
            wrap ??= new GUIStyle(EditorStyles.label) { wordWrap = true };
            small ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, alignment = TextAnchor.UpperCenter };

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(70))) Refresh();
                GUILayout.Label("구역", GUILayout.Width(30));
                zoneFilter = EditorGUILayout.Popup(zoneFilter, zones, EditorStyles.toolbarPopup, GUILayout.Width(110));
                hideDone = GUILayout.Toggle(hideDone, "교체한 것 숨기기", EditorStyles.toolbarButton, GUILayout.Width(110));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("매칭 목록 Console에 출력", EditorStyles.toolbarButton)) PrintMatches();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSlots(position.width * 0.42f);
                DrawPrefabs();
            }
            DrawActionBar();

            if (AssetPreview.IsLoadingAssetPreviews()) Repaint();
        }

        void DrawSlots(float width)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(width)))
            {
                slotSearch = EditorGUILayout.TextField("오브젝트 검색", slotSearch);
                leftScroll = EditorGUILayout.BeginScrollView(leftScroll);
                string lastZone = null;
                foreach (var s in slots)
                {
                    if (s.go == null) continue;
                    if (zoneFilter > 0 && s.zone != zones[zoneFilter]) continue;
                    bool done = CurrentAsset(s) != null;
                    if (hideDone && done) continue;
                    if (slotSearch.Length > 0 && !(s.go.name + s.role + s.kind).ToLower().Contains(slotSearch.ToLower())) continue;

                    if (s.zone != lastZone) { EditorGUILayout.LabelField(s.zone, EditorStyles.boldLabel); lastZone = s.zone; }

                    var bg = GUI.backgroundColor;
                    if (s == selected) GUI.backgroundColor = new Color(0.45f, 0.7f, 1f);
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        GUI.backgroundColor = bg;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button((done ? "✔ " : "") + s.go.name + (s.group ? "  (묶음)" : ""), EditorStyles.label))
                            {
                                selected = s;
                                Selection.activeGameObject = s.go;
                                EditorGUIUtility.PingObject(s.go);
                            }
                            GUILayout.FlexibleSpace();
                            GUILayout.Label(s.kind, EditorStyles.miniBoldLabel);
                        }
                        if (!string.IsNullOrEmpty(s.role)) GUILayout.Label(s.role, wrap);
                        var a = CurrentAsset(s);
                        if (a != null) GUILayout.Label("→ " + a.name.Substring(Prefix.Length), EditorStyles.miniLabel);
                    }
                    GUI.backgroundColor = bg;
                }
                EditorGUILayout.EndScrollView();
            }
        }

        void DrawPrefabs()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    int p = GUILayout.Toolbar(pack, PackNames);
                    if (p != pack) { pack = p; LoadPrefabs(); }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    string q = EditorGUILayout.TextField("프리팹 검색", prefabSearch);
                    if (q != prefabSearch) { prefabSearch = q; }
                    thumb = GUILayout.HorizontalSlider(thumb, 64, 160, GUILayout.Width(100));
                }

                var list = prefabs.Where(x => x != null && (prefabSearch.Length == 0 ||
                    (x.name + AssetDatabase.GetAssetPath(x)).ToLower().Contains(prefabSearch.ToLower()))).ToList();
                GUILayout.Label($"프리팹 {list.Count}개", EditorStyles.miniLabel);

                rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
                float w = position.width * 0.58f - 30;
                int cols = Mathf.Max(1, (int)(w / (thumb + 8)));
                for (int i = 0; i < list.Count; i += cols)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        for (int j = i; j < Mathf.Min(i + cols, list.Count); j++)
                        {
                            var pf = list[j];
                            var bg = GUI.backgroundColor;
                            if (pf == picked) GUI.backgroundColor = new Color(1f, 0.8f, 0.3f);
                            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(thumb + 4)))
                            {
                                GUI.backgroundColor = bg;
                                var tex = AssetPreview.GetAssetPreview(pf) ?? AssetPreview.GetMiniThumbnail(pf);
                                if (GUILayout.Button(tex, GUILayout.Width(thumb), GUILayout.Height(thumb)))
                                {
                                    picked = pf;
                                    if (Event.current.clickCount > 1 && selected != null) Apply(selected, picked);
                                }
                                GUILayout.Label(Short(pf.name), small, GUILayout.Width(thumb));
                            }
                            GUI.backgroundColor = bg;
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        void DrawActionBar()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("선택: " + (selected?.go != null ? selected.go.name : "(왼쪽에서 오브젝트 선택)") +
                                    "   ←   " + (picked != null ? picked.name : "(오른쪽에서 프리팹 선택)"), EditorStyles.boldLabel);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    fit = (Fit)EditorGUILayout.EnumPopup("크기 맞추기", fit, GUILayout.Width(260));
                    autoRotate = GUILayout.Toggle(autoRotate, "긴 방향 맞춰 90° 회전", GUILayout.Width(150));
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(selected?.go == null || picked == null))
                        if (GUILayout.Button("적용", GUILayout.Width(90), GUILayout.Height(24))) Apply(selected, picked);
                    using (new EditorGUI.DisabledScope(selected?.go == null))
                    {
                        if (GUILayout.Button("원래대로", GUILayout.Width(80), GUILayout.Height(24))) Revert(selected);
                        if (GUILayout.Button("화면 이동", GUILayout.Width(80), GUILayout.Height(24)))
                        {
                            Selection.activeGameObject = CurrentAsset(selected) ?? selected.go;
                            SceneView.lastActiveSceneView?.FrameSelected();
                        }
                    }
                }
                EditorGUILayout.LabelField("크기 맞추기: 원래크기 = 배율 그대로 / 비율유지 = 찌그러뜨리지 않고 큐브 안에 / 늘려서맞춤 = 큐브 크기에 꼭 맞게 (벽·문) / 데크 = 가로세로만 맞추고 윗면을 큐브 윗면에 (바닥·다리).  프리팹 더블클릭 = 바로 적용.  원래대로 = 넣은 모델을 지우고 그 오브젝트의 회색 렌더러를 다시 켬", EditorStyles.wordWrappedMiniLabel);
            }
        }

        static string Short(string n) => n.Replace("EA_Bld_", "").Replace("EA03_", "").Replace("EA_", "").Replace("_PRE", "");

        // ───────────────────────── 목록 만들기 ─────────────────────────
        void BuildSlots()
        {
            slots.Clear();
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == CourseRoot) Walk(t, "", true);
                    else if (t.name == OldRoot) Walk(t, "", false);
                }
                if (root.name == "EVERFLOW_Assets_v3") Walk(root.transform, "", false);
            }
        }

        void Walk(Transform node, string zone, bool includeInactive)
        {
            foreach (Transform c in node)
            {
                if (HasOldPrefix(c.name)) continue;
                if (!includeInactive && !c.gameObject.activeInHierarchy) continue;
                if (c.name.StartsWith("EFMapPlayer") || c.name.Contains("Camera") || c.name.StartsWith("Ability orb")) continue;

                string z = zone;
                if (node.name == CourseRoot) z = ZoneOf(c.name);
                if (node.name == OldRoot || node.parent != null && node.parent.name == OldRoot) z = "예전 그레이박스";
                if (node.name == "EVERFLOW_Assets_v3") z = "배경 (바위·풍차)";
                if (string.IsNullOrEmpty(z)) z = "기타";

                bool hasScript = HasScript(c.gameObject);
                bool hasRenderer = c.GetComponent<MeshRenderer>() != null;
                bool collapsible = IsCollapsible(c);

                if (collapsible)
                {
                    Add(c, z, true);
                    continue;
                }
                if (hasRenderer || hasScript) Add(c, z, false);
                Walk(c, z, includeInactive);
            }
        }

        // 하나로 묶어서 보여 줄 것
        //  - 배치된 프리팹(바위·풍차 등)은 통째로 하나
        //  - 자식이 전부 조각(더 아래 자식 없음)이고, 스크립트가 없고, 모양을 가진 조각이 2개 이상 (계단·꽃가루·꽃씨 등)
        static bool IsCollapsible(Transform t)
        {
            if (t.GetComponent("EF1123Target") != null) return false;
            var desc = t.GetComponentsInChildren<Transform>(true).Where(d => d != t && !InsideAsset(d, t)).ToList();
            if (desc.Any(d => HasScript(d.gameObject))) return false;
            if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                return desc.Any(d => d.GetComponent<MeshRenderer>() != null) || t.GetComponent<MeshRenderer>() != null;
            if (t.childCount < 2 || t.GetComponent<MeshRenderer>() != null) return false;
            foreach (Transform c in t)
                if (!HasOldPrefix(c.name) && c.childCount > 0 && !c.Cast<Transform>().All(g => HasOldPrefix(g.name))) return false;
            return desc.Count(d => d.GetComponent<MeshRenderer>() != null) >= 2;
        }

        static bool HasScript(GameObject g) =>
            g.GetComponent("EF1123Target") != null || g.GetComponent("EF1123Motion") != null || g.GetComponent("EF1123World") != null;

        void Add(Transform t, string zone, bool group)
        {
            var s = new Slot { go = t.gameObject, zone = zone, group = group };
            if (Roles.TryGetValue(t.name, out var r)) { s.kind = r.kind; s.role = r.role; }
            else
            {
                s.kind = t.GetComponent("EF1123Target") != null ? "조준 표식"
                       : t.GetComponent("EF1123Motion") != null ? "움직이는 장치"
                       : !t.gameObject.activeSelf ? "나타남·사라짐"
                       : group && t.Cast<Transform>().Any(c => c.name.StartsWith("Step")) ? "계단 묶음"
                       : group ? "묶음"
                       : t.GetComponent<Collider>() != null ? "벽·발판" : "장식";
                s.role = KindHint(s.kind, t);
            }
            if (group && t.Cast<Transform>().Count(c => c.name.StartsWith("Step")) > 0)
                s.role = (s.role ?? "") + $"  [{t.Cast<Transform>().Count(c => c.name.StartsWith("Step"))}칸]";
            slots.Add(s);
        }

        static string KindHint(string kind, Transform t) => kind switch
        {
            "조준 표식" => "능력을 쓰는 대상. 구 모양은 바꿔도 되지만 조준 판정(SphereCollider)은 유지",
            "움직이는 장치" => "퍼즐을 풀면 움직임. 모델은 꼭 이 오브젝트의 자식으로",
            "나타남·사라짐" => "처음엔 꺼져 있다가 퍼즐에 따라 켜지거나 꺼짐. 켜지 말고 자식으로만 넣기",
            "계단 묶음" => "여러 칸이 모여 계단 하나. 묶음 전체를 계단 모델 하나로 덮을 수 있음",
            "묶음" => "여러 조각이 모여 하나를 이룸",
            "벽·발판" => "밟거나 막는 판정이 있음. 모델과 판정 크기를 맞추기",
            _ => "보이기만 하는 모양",
        };

        static string ZoneOf(string n)
        {
            if (n.StartsWith("E |")) return "E 착지턱";
            if (n.StartsWith("S and")) return "S 시작·공용 길";
            if (n.StartsWith("West |")) return "F 채광실";
            if (n.StartsWith("B |")) return "B 수조";
            if (n.StartsWith("C |")) return "C 바깥섬";
            if (n.StartsWith("D |")) return "D 골짜기";
            if (n.StartsWith("W |")) return "W 발코니";
            if (n.StartsWith("G |")) return "G 풍차";
            return "기타";
        }

        void LoadPrefabs()
        {
            prefabs.Clear();
            var folders = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                bool inPack = pack == 0
                    ? PackRoots.Skip(1).Any(r => path.Contains(r))
                    : path.Contains(PackRoots[pack]);
                if (!inPack) continue;
                if (path.Contains("/Demo") || path.Contains("Scenes/")) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && go.GetComponentInChildren<Renderer>(true) != null) prefabs.Add(go);
            }
            prefabs = prefabs.OrderBy(p => AssetDatabase.GetAssetPath(p)).ToList();
        }

        // ───────────────────────── 교체 ─────────────────────────
        static bool HasOldPrefix(string n) => OldPrefixes.Any(p => n.StartsWith(p));

        static GameObject CurrentAsset(Slot s)
        {
            if (s?.go == null) return null;
            foreach (Transform c in s.go.transform) if (HasOldPrefix(c.name)) return c.gameObject;
            return null;
        }

        static IEnumerable<MeshRenderer> GreyRenderers(Transform root)
        {
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
                if (!InsideAsset(mr.transform, root)) yield return mr;
        }

        static bool InsideAsset(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent) if (HasOldPrefix(t.name)) return true;
            return false;
        }

        void Apply(Slot s, GameObject prefab)
        {
            Transform parent = s.go.transform;
            Undo.SetCurrentGroupName("에셋 매칭 " + parent.name);
            int group = Undo.GetCurrentGroup();

            // 그레이박스 크기 (자기 + 자식들, 부모 로컬 기준)
            bool hasGrey = FrameBounds(GreyRenderers(parent).Select(r => r.GetComponent<MeshFilter>()), parent, out Bounds grey);

            foreach (Transform c in parent.Cast<Transform>().Where(c => HasOldPrefix(c.name)).ToList())
                Undo.DestroyObjectImmediate(c.gameObject);
            foreach (var mr in GreyRenderers(parent))
            {
                Undo.RecordObject(mr, "Hide greybox");
                mr.enabled = false;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(inst, "Add asset");
            inst.name = Prefix + prefab.name;
            var t = inst.transform;
            Undo.RecordObject(t, "Fit asset");
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;

            Vector3 ps = parent.lossyScale, aps = Abs(ps);
            t.localScale = ScaleFor(Vector3.one, ps, false);

            if (hasGrey && FrameBounds(inst.GetComponentsInChildren<MeshFilter>(true), parent, out Bounds model))
            {
                bool rotated = false;
                if (autoRotate && fit != Fit.원래크기 && fit != Fit.비율유지 &&
                    (grey.size.x * aps.x >= grey.size.z * aps.z) != (model.size.x * aps.x >= model.size.z * aps.z))
                {
                    rotated = true;
                    t.localRotation = Quaternion.Euler(0, 90, 0);
                    t.localScale = ScaleFor(Vector3.one, ps, true);
                    FrameBounds(inst.GetComponentsInChildren<MeshFilter>(true), parent, out model);
                }

                Vector3 gw = Vector3.Scale(grey.size, aps), mw = Vector3.Scale(model.size, aps);
                float rx = Ratio(gw.x, mw.x), ry = Ratio(gw.y, mw.y), rz = Ratio(gw.z, mw.z);
                Vector3 r = fit switch
                {
                    Fit.늘려서맞춤 => new Vector3(rx, ry, rz),
                    Fit.데크 => new Vector3(rx, (rx + rz) * 0.5f, rz),
                    Fit.비율유지 => Vector3.one * Mathf.Min(rx, ry, rz),
                    _ => Vector3.one,
                };
                t.localScale = ScaleFor(r, ps, rotated);

                FrameBounds(inst.GetComponentsInChildren<MeshFilter>(true), parent, out model);
                float ty = fit == Fit.데크 ? grey.max.y : grey.min.y;
                float my = fit == Fit.데크 ? model.max.y : model.min.y;
                t.localPosition += new Vector3(grey.center.x - model.center.x, ty - my, grey.center.z - model.center.z);
            }

            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(parent.gameObject.scene);
            Selection.activeGameObject = inst;
        }

        void Revert(Slot s)
        {
            Transform parent = s.go.transform;
            Undo.SetCurrentGroupName("에셋 매칭 되돌리기 " + parent.name);
            int group = Undo.GetCurrentGroup();
            foreach (Transform c in parent.Cast<Transform>().Where(c => HasOldPrefix(c.name)).ToList())
                Undo.DestroyObjectImmediate(c.gameObject);
            foreach (var mr in GreyRenderers(parent))
            {
                Undo.RecordObject(mr, "Show greybox");
                mr.enabled = true;
            }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(parent.gameObject.scene);
        }

        void PrintMatches()
        {
            var sb = new System.Text.StringBuilder("[에셋 매칭] 현재 매칭 목록\n");
            foreach (var s in slots)
            {
                var a = CurrentAsset(s);
                if (a != null) sb.AppendLine($"{s.zone}\t{s.go.name}\t→ {a.name.Substring(a.name.IndexOf('_') + 1)}");
            }
            Debug.Log(sb.ToString());
        }

        static bool FrameBounds(IEnumerable<MeshFilter> filters, Transform frame, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            Matrix4x4 toFrame = frame.worldToLocalMatrix;
            foreach (var mf in filters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                Matrix4x4 m = toFrame * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(c);
                    if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return found;
        }

        static Vector3 ScaleFor(Vector3 r, Vector3 ps, bool rotated) => rotated
            ? new Vector3(r.z * SafeInv(ps.z), r.y * SafeInv(ps.y), r.x * SafeInv(ps.x))
            : new Vector3(r.x * SafeInv(ps.x), r.y * SafeInv(ps.y), r.z * SafeInv(ps.z));

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        static float SafeInv(float v) => Mathf.Abs(v) < 1e-5f ? 1f : 1f / v;
        static float Ratio(float want, float have) => have < 1e-4f ? 1f : want / have;

        // ───────────────────────── 역할 설명 (Hierarchy 역할 시트와 같은 내용) ─────────────────────────
        static readonly Dictionary<string, (string kind, string role)> Roles = new Dictionary<string, (string, string)>
        {
            ["Windmill"] = ("보이는 모델", "풍차 건물 외형 (최종 목적지). 문 판정은 G 구역이 따로 담당"),
            ["S_A1"] = ("발판·계단", "시작 전망대 탑. 플레이어가 TWR_Top 위에서 시작 (예전 그룹이지만 사용 중)"),
            ["B_Watertank"] = ("벽·판정", "B 수조 둑. 물 표면을 둘러싼 벽 (예전 그룹이지만 사용 중)"),
            ["A2"] = ("예전 그레이박스", "예전 A2 바람 다리 집. 퍼즐에 안 씀"),
            ["W_Watermill"] = ("예전 그레이박스", "예전 수차·발코니. 퍼즐에 안 씀"),
            ["Growing root descent"] = ("나타남·사라짐", "전망대 뿌리에 BLOOM하면 켜지는 뿌리 계단 (탑 → 계곡 바닥)"),
            ["Shared switchback - lower"] = ("계단 묶음", "계곡 바닥 → 상층 갈림길 큰 꺾인 돌계단 아래쪽"),
            ["Shared switchback - upper"] = ("계단 묶음", "계곡 바닥 → 상층 갈림길 큰 꺾인 돌계단 위쪽"),
            ["Shared switchback rest"] = ("벽·발판", "큰 돌계단 중간 쉼터"),
            ["East gateway approach"] = ("계단 묶음", "동쪽(B·C 방향)으로 이어지는 계단"),
            ["East gateway approach (1)"] = ("계단 묶음", "동쪽(B·C 방향)으로 이어지는 계단"),
            ["Upper access"] = ("벽·발판", "상층 진입 발판"),
            ["Leaf catchment"] = ("움직이는 장치", "B ① 잎 물받이. BLOOM하면 2.7배 커지고 기울어짐"),
            ["B catchment source"] = ("장식", "위에서 떨어지는 물줄기"),
            ["Spout pier"] = ("벽·발판", "물줄기 옆 기둥"),
            ["Root bulb"] = ("장식", "잎 물받이 밑 덩이뿌리"),
            ["B open leak"] = ("나타남·사라짐", "새는 물. 잎 물받이 후 켜지고, 누수구를 얼리면 꺼짐"),
            ["Ice over the leak"] = ("나타남·사라짐", "누수구를 막는 얼음. FREEZE 후 켜짐"),
            ["B diverted stream"] = ("나타남·사라짐", "잎이 수조로 돌린 물줄기"),
            ["B spill toward garden"] = ("나타남·사라짐", "정원으로 넘치는 물"),
            ["B fill level"] = ("움직이는 장치", "수조 물 표면. 누수구를 얼리면 0.8m 올라감"),
            ["B garden pond"] = ("움직이는 장치", "정원 연못 물. 3.8m 올라감"),
            ["Floating leaf group"] = ("움직이는 장치", "물과 함께 떠오르는 잎 발판 3장 (밟는 판정 포함)"),
            ["B dock service steps"] = ("계단 묶음", "수조 옆 → 선착장 계단"),
            ["B shore dock"] = ("벽·발판", "잎 발판이 시작되는 선착장"),
            ["B return to shore"] = ("계단 묶음", "별 뒤 출구에서 내려오는 복귀 계단"),
            ["B exit turning deck"] = ("벽·발판", "복귀로 꺾이는 데크"),
            ["B return toward C"] = ("벽·발판", "C로 이어지는 둑길"),
            ["PH_E_Slab - growing stone"] = ("움직이는 장치", "E 석판. 1.4m 올라가며 29° 기울어 경사로가 됨 (위를 걸음)"),
            ["Slab root support"] = ("움직이는 장치", "석판 밑 뿌리. 세로로 6배 자람"),
            ["E middle ledge"] = ("벽·발판", "석판을 올라가서 서는 중간 선반"),
            ["Leaf hinge"] = ("움직이는 장치", "흔들리는 회전 잎. FREEZE로 고정하면 다리가 됨"),
            ["Leaf pin"] = ("장식", "회전 잎 축 장식"),
            ["E shelf return"] = ("계단 묶음", "별 뒤 복귀 계단"),
            ["E return descent"] = ("계단 묶음", "외곽을 돌아 내려오는 복귀 계단"),
            ["D gate path"] = ("계단 묶음", "계곡 본길 → D 문 앞 계단"),
            ["PH_D_Gate - working gate"] = ("움직이는 장치", "D 문. FLOW 후 옆으로 3.3m 밀려 열림"),
            ["D dancing pollen"] = ("나타남·사라짐", "문 앞에 떠 있는 꽃가루. FLOW 후 사라짐"),
            ["Uncovered wind latch"] = ("나타남·사라짐", "꽃가루가 걷히면 드러나는 걸쇠"),
            ["Dungeon arch face"] = ("장식", "D 문틀"),
            ["D eastern masonry"] = ("벽·발판", "D 문 옆 벽"),
            ["D western masonry"] = ("벽·발판", "D 문 옆 벽"),
            ["D seed ledge"] = ("벽·발판", "씨앗 받침대가 있는 턱"),
            ["Decorative seed pedestal"] = ("장식", "씨앗 받침대"),
            ["Seed from the pedestal"] = ("움직이는 장치", "씨앗. FLOW 후 앞으로 4.4m 날아가 흙받이에 떨어짐"),
            ["Soil pocket on the opposite shelf"] = ("장식", "흙받이 표시 (터레인 흙 텍스처로 대체 예정)"),
            ["D leaf stairs"] = ("나타남·사라짐", "도착한 씨앗에 BLOOM하면 켜지는 잎 계단"),
            ["D upper return descent"] = ("계단 묶음", "별 뒤 복귀 계단"),
            ["D return toward upper slope"] = ("계단 묶음", "상층 공용 계단 아래로 이어지는 복귀 계단"),
            ["Basin trail to west dock"] = ("벽·발판", "둑길 → 서쪽 선착장 길"),
            ["C low departure access"] = ("계단 묶음", "서쪽 선착장으로 내려가는 계단"),
            ["C departure dock"] = ("벽·발판", "나룻배 출발 부두"),
            ["Leaf ferry"] = ("움직이는 장치", "잎 나룻배 (타는 판정). FLOW 후 6.2m 이동"),
            ["C ferry basin water"] = ("장식", "연못 물"),
            ["Imported dock planks"] = ("장식", "판자 부두 모양"),
            ["Imported lake landing"] = ("장식", "판자 착지 모양"),
            ["C lake landing"] = ("벽·발판", "반대편 정박턱"),
            ["C low to high causeway"] = ("계단 묶음", "정박턱 → 바람막이 잎 앞 나무 계단"),
            ["C windbreak access"] = ("벽·발판", "바람막이 잎 앞 발판"),
            ["Folded windbreak plant"] = ("움직이는 장치", "접힌 바람막이 잎. BLOOM 후 3.1배 커짐"),
            ["Impassable crosswind"] = ("나타남·사라짐", "길을 막는 횡풍. 바람막이 잎이 자라면 사라짐"),
            ["C stairs to glider"] = ("계단 묶음", "발사턱으로 오르는 계단"),
            ["C glider launch deck"] = ("벽·발판", "꽃씨 발사턱"),
            ["Dandelion parachute"] = ("보이는 모델", "민들레 꽃씨. E로 잡으면 머리 위에 붙고 FLOW로 섬까지 활공"),
            ["C wide landing"] = ("벽·발판", "섬의 넓은 착지대"),
            ["C landing contrast"] = ("장식", "착지대 표시"),
            ["C restored return bridge"] = ("움직이는 장치", "복귀 다리. 별을 얻으면 7.9m 올라와 섬과 발사턱을 이음"),
            ["C one-way cliff gate"] = ("움직이는 장치", "옆길 문. 별을 얻으면 5.3m 올라가 열림"),
            ["C cliff-edge rail"] = ("벽·발판", "절벽 끝 난간"),
            ["Maintenance sluice"] = ("움직이는 장치", "W 수문. 덩굴이 자라면 3.4m 올라감"),
            ["Sluice vine"] = ("움직이는 장치", "수문 덩굴. BLOOM 후 자람"),
            ["W maintenance spill"] = ("나타남·사라짐", "수문이 열리면 흘러나오는 물"),
            ["W left quay wall"] = ("벽·발판", "수문 옆 벽"),
            ["W right quay wall"] = ("벽·발판", "수문 옆 벽"),
            ["W service stairs"] = ("계단 묶음", "수문 안 꺾인 계단 1구간"),
            ["W upper service stairs"] = ("계단 묶음", "수문 안 꺾인 계단 2구간"),
            ["W operator deck"] = ("벽·발판", "정비 풍차 조작 발판"),
            ["Service windmill spindle"] = ("움직이는 장치", "작은 풍차 날개. FLOW 후 세 바퀴 돔"),
            ["Service windmill bearing"] = ("벽·발판", "작은 풍차 축받이 기둥"),
            ["Opened by the windmill rope"] = ("움직이는 장치", "도개교. FLOW 후 86° 내려와 건널 수 있음"),
            ["W return staircase"] = ("계단 묶음", "별 뒤 복귀 계단"),
            ["West approach from upper road"] = ("계단 묶음", "상층 → 도약 꽃 샛길"),
            ["West trail to flower"] = ("계단 묶음", "도약 꽃 앞 짧은 길"),
            ["Flower pad growth"] = ("움직이는 장치", "도약 꽃. BLOOM 후 1.8배 자람, 꽃 위에서 Space로 도약"),
            ["Flower pad foundation"] = ("벽·발판", "꽃 받침 바닥"),
            ["F southern left window pier"] = ("벽·발판", "도약해 넘는 창 기둥"),
            ["F southern right window pier"] = ("벽·발판", "도약해 넘는 창 기둥"),
            ["F window lintel"] = ("벽·발판", "창 위 상인방"),
            ["Dungeon window arch"] = ("장식", "창 아치"),
            ["F window and lens chamber floor"] = ("벽·발판", "렌즈 방 바닥"),
            ["F east side wall"] = ("벽·발판", "렌즈 방 벽"),
            ["F west side wall"] = ("벽·발판", "렌즈 방 벽"),
            ["F lens room roof"] = ("벽·발판", "렌즈 방 지붕"),
            ["Lens plinth"] = ("벽·발판", "렌즈 받침대"),
            ["Iron lens rim"] = ("장식", "쇠 렌즈 테"),
            ["Liquid in the mould"] = ("나타남·사라짐", "렌즈 틀 안의 물. FREEZE 후 사라짐"),
            ["Solid blue lens"] = ("나타남·사라짐", "얼음 렌즈. FREEZE 후 나타남"),
            ["Powered light receiver"] = ("나타남·사라짐", "켜진 수신기. FREEZE 후 나타남"),
            ["Dormant light receiver"] = ("장식", "꺼진 수신기"),
            ["Focused light"] = ("나타남·사라짐", "모인 빛줄기. FREEZE 후 나타남"),
            ["F short interior stair"] = ("계단 묶음", "렌즈 방 안 짧은 계단"),
            ["F one-way return"] = ("계단 묶음", "옆 출구 → 상층 능선 복귀 계단"),
            ["G threshold stair"] = ("계단 묶음", "풍차 입구 앞 돌계단"),
            ["G entry landing"] = ("벽·발판", "풍차 입구 앞 발판"),
            ["G windmill threshold"] = ("벽·발판", "풍차 문턱"),
            ["Windmill_doorL - functional hinge"] = ("움직이는 장치", "풍차 왼쪽 문. 별 6개면 96° 열림"),
            ["Windmill_doorR - functional hinge"] = ("움직이는 장치", "풍차 오른쪽 문. 별 6개면 96° 열림"),
            ["Windmill corridor collision"] = ("벽·발판", "보이지 않는 통로 벽 (렌더러만 끄기)"),
            ["Windmill facade collision"] = ("벽·발판", "보이지 않는 앞면 벽 (렌더러만 끄기)"),
            ["Windmill back collision"] = ("벽·발판", "보이지 않는 뒷면 벽 (렌더러만 끄기)"),
            ["Threshold and entry floor"] = ("벽·발판", "풍차 입구 바닥. 안으로 걸어 들어가면 클리어"),
            ["Puzzle access gate"] = ("움직이는 장치", "보상실 입구 문. 코스 마지막 장치를 풀면 3m 올라감"),
            ["Collection return gate"] = ("움직이는 장치", "보상실 뒤 출구 문. 별을 얻으면 3m 올라감"),
            ["Collection side gate"] = ("움직이는 장치", "채광실 옆 출구 문. 별을 얻으면 3m 올라감"),
            ["Reward room floor"] = ("벽·발판", "보상실 바닥"),
            ["Reward room canopy"] = ("벽·발판", "보상실 지붕"),
            ["Reward west wall"] = ("벽·발판", "보상실 벽"),
            ["Reward east wall"] = ("벽·발판", "보상실 벽"),
            ["Front lintel"] = ("벽·발판", "보상실 입구 위 상인방"),
            ["Back lintel"] = ("벽·발판", "보상실 출구 위 상인방"),
            ["Trail sign"] = ("장식", "길 안내 표지판 (삭제 예정, 보류)"),
        };
    }
}
