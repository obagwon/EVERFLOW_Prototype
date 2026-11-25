#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EverflowMap.EditorTools
{
    // W 코스 고치기 + D 압력발판
    // 메뉴: Tools > EVERFLOW > W 고치기 (오류·덩굴·프리즈 다리·옮기기)
    //  1) 지워진 W02 Operator deck 다시 만들고 '정비 풍차의 날개' Station / 체크포인트에 연결
    //  2) 정비 풍차 조준 구체를 풍차 축 옆으로 (원래 간격대로)
    //  3) 수문 덩굴(덩굴·조준 구체·W01 발판)을 수문 앞에 원래 간격대로 → 덩굴이 문을 들어 올리는 그림
    //  4) 도개교: FLOW 로 내려온 뒤 경첩에서 위아래로 흔들림 → 알맞은 경사일 때 FREEZE 로 멈춤
    //     (새 조준 구체 '도개교 경첩' + 흔들림 장치 'Bridge swing' 추가, W 별은 이 단계까지 해야 열림)
    //  5) 씬에 'W_Sluice_Target' 이라는 오브젝트가 있으면 → 수문이 그 위치에 오도록 W 퍼즐 전체를 같은 만큼 옮김
    //     (수차는 안 움직임. 수문 틀 장식 등 W 그룹 밖 오브젝트는 아래 Extras 이름 목록으로 같이 옮김)
    //  6) D 낙엽 더미 밑에 압력발판 (모양만, 스크립트 없음)
    // Ctrl+Z 한 번이면 전부 되돌아가요.
    public static class EFFixW
    {
        // W 그룹 밖에 있지만 W 와 같이 옮길 오브젝트 이름 (필요하면 추가)
        static readonly string[] Extras = { "Wall_entrance (1)" };

        // 원래 그레이박스에서의 간격 (그대로 되살림)
        static readonly Vector3 VineFromSluice = new Vector3(-0.62f, -1.26f, -0.18f);
        static readonly Vector3 VineTargetFromSluice = new Vector3(-0.96f, 0.21f, -0.59f);
        static readonly Vector3 W01FromSluice = new Vector3(-0.02f, -1.46f, -1.28f);
        static readonly Vector3 WingTargetFromSpindle = new Vector3(0.01f, -0.2f, -0.47f);
        static readonly Vector3 W02FromBridge = new Vector3(0.99f, 0f, 0f);
        const float SwayDeg = 18f, SwayTolerance = 4f, SwayPeriod = 3.2f;

        [MenuItem("Tools/EVERFLOW/W 고치기 (오류·덩굴·프리즈 다리·옮기기)")]
        public static void Run()
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("W 고치기");
            var log = new List<string>();
            try
            {
                var world = UnityEngine.Object.FindObjectsByType<EF1123World>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
                var W = Find("W | Sluice and drawbridge");
                if (world == null || W == null) throw new Exception("EF1123World 또는 'W | Sluice and drawbridge' 를 못 찾음");

                var wing = Target("정비 풍차의 날개");
                var spindle = Under(W, "Service windmill spindle");
                var bridge = Under(W, "Opened by the windmill rope");
                var sluice = Under(W, "Maintenance sluice");

                // 1) W02 Operator deck
                var w02 = Under(W, "W02 Operator deck");
                if (w02 == null && bridge != null)
                {
                    var go = new GameObject("W02 Operator deck");
                    Undo.RegisterCreatedObjectUndo(go, "w02");
                    go.transform.SetParent(W, false);
                    go.transform.position = bridge.position + W.TransformVector(W02FromBridge);
                    go.transform.rotation = W.rotation * Quaternion.Euler(0, 270, 0);
                    w02 = go.transform;
                    log.Add("1) W02 Operator deck 다시 만듦 (도개교 경첩 옆)");
                }
                if (wing != null && w02 != null && wing.station == null)
                {
                    Undo.RecordObject(wing, "station"); wing.station = w02;
                    log.Add("   '정비 풍차의 날개' Station 연결");
                }
                var wso = new SerializedObject(world);
                var cps = wso.FindProperty("checkpoints");
                for (int i = 0; i < cps.arraySize; i++)
                {
                    var e = cps.GetArrayElementAtIndex(i);
                    if (e.FindPropertyRelative("surface").objectReferenceValue == null && w02 != null)
                    {
                        e.FindPropertyRelative("surface").objectReferenceValue = w02;
                        log.Add("   빈 체크포인트(" + e.FindPropertyRelative("course").intValue + "/" + e.FindPropertyRelative("stage").intValue + ") 에 W02 연결");
                    }
                }
                wso.ApplyModifiedProperties();

                // 2) 정비 풍차 조준 구체 → 풍차 축 옆
                if (wing != null && spindle != null)
                {
                    Undo.RecordObject(wing.transform, "wing");
                    wing.transform.position = spindle.position + W.TransformVector(WingTargetFromSpindle);
                    log.Add("2) '정비 풍차의 날개' 조준 구체를 풍차 축 옆으로");
                }

                // 3) 수문 덩굴 정렬
                var vine = Under(W, "Sluice vine");
                var vineTarget = Target("수문 덩굴");
                var w01 = Under(W, "W01 Sluice approach");
                if (sluice != null)
                {
                    Vector3 c = sluice.position;
                    if (vine != null) { Undo.RecordObject(vine, "v"); vine.position = c + W.TransformVector(VineFromSluice); }
                    if (vineTarget != null) { Undo.RecordObject(vineTarget.transform, "v"); vineTarget.transform.position = c + W.TransformVector(VineTargetFromSluice); }
                    if (w01 != null) { Undo.RecordObject(w01, "v"); w01.position = c + W.TransformVector(W01FromSluice); }
                    log.Add("3) 수문 덩굴·조준 구체·W01 발판을 수문 앞에 원래 간격대로");
                }

                // 4) 프리즈 다리
                if (bridge != null && wing != null) FreezeBridge(world, W, bridge, wing, w02, log);

                // 5) 마커가 있으면 W 전체 옮기기
                var marker = Find("W_Sluice_Target");
                if (marker != null && sluice != null)
                {
                    Vector3 delta = marker.position - sluice.position;
                    Undo.RecordObject(W, "move W"); W.position += delta;
                    foreach (var n in Extras)
                    {
                        var x = Find(n);
                        if (x != null && !x.IsChildOf(W)) { Undo.RecordObject(x, "move"); x.position += delta; }
                    }
                    log.Add("5) W 퍼즐 전체를 (" + delta.x.ToString("0.00") + ", " + delta.y.ToString("0.00") + ", " + delta.z.ToString("0.00") + ") 만큼 옮김 → 수문이 W_Sluice_Target 위치에");
                }
                else log.Add("5) 'W_Sluice_Target' 오브젝트가 없어 W 전체 옮기기는 건너뜀");

                // 6) D 압력발판
                PressurePlate(log);
            }
            catch (Exception e) { Debug.LogException(e); log.Add("오류: " + e.Message); }

            Undo.CollapseUndoOperations(group);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            var msg = string.Join("\n", log);
            Debug.Log("[W 고치기]\n" + msg);
            EditorUtility.DisplayDialog("W 고치기", msg + "\n\n마음에 안 들면 Ctrl+Z 한 번으로 전부 되돌려져요.", "확인");
        }

        static void FreezeBridge(EF1123World world, Transform W, Transform bridge, EF1123Target wing, Transform w02, List<string> log)
        {
            if (Find("도개교 경첩") != null) { log.Add("4) 프리즈 다리: 이미 있음"); return; }
            var bm = bridge.GetComponent<EF1123Motion>();
            if (bm == null) { log.Add("4) 도개교에 EF1123Motion 이 없음"); return; }

            // 경첩 축 (다리 장치의 부모 기준): 장치가 도는 축
            Vector3 turnLocal = bm.turn.sqrMagnitude > 0 ? bm.turn.normalized : Vector3.forward;
            Vector3 hingeAxisParent = bridge.localRotation * turnLocal;
            Vector3 swayAxis = new Vector3(Mathf.Round(hingeAxisParent.x), Mathf.Round(hingeAxisParent.y), Mathf.Round(hingeAxisParent.z));

            // 흔들림 장치: 경첩 위치의 빈 오브젝트를 다리의 부모로 끼움
            var swing = new GameObject("Bridge swing");
            Undo.RegisterCreatedObjectUndo(swing, "swing");
            swing.transform.SetParent(bridge.parent, false);
            swing.transform.localPosition = bridge.localPosition;
            swing.transform.localRotation = Quaternion.identity;
            Undo.SetTransformParent(bridge, swing.transform, "swing");
            var sm = Undo.AddComponent<EF1123Motion>(swing);
            sm.seconds = 0.4f; sm.period = SwayPeriod; sm.alignmentTolerance = SwayTolerance;
            var gate = Undo.AddComponent<EFSwayGate>(swing);
            gate.gate = bm; gate.sway = swayAxis * SwayDeg;

            // 새 조준 구체 '도개교 경첩' (FREEZE) — 회전 잎의 축을 복제해서 설정
            var proto = Target("회전 잎의 축");
            GameObject tgo = proto != null ? UnityEngine.Object.Instantiate(proto.gameObject, W) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(tgo, "target");
            tgo.name = "도개교 경첩";
            tgo.transform.SetParent(W, true);
            var col = bridge.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Bridge planks collision");
            // 다리가 내려왔을 때 판 가운데쯤 위
            Quaternion openLocal = bridge.localRotation * Quaternion.Euler(bm.turn);
            Vector3 colLocal = col != null ? Vector3.Scale(bridge.localScale, col.localPosition) : Vector3.zero;
            Vector3 midOpen = swing.transform.TransformPoint(openLocal * colLocal * 0.6f);
            tgo.transform.position = midOpen + Vector3.up * 0.9f;
            tgo.transform.localScale = proto != null ? proto.transform.localScale : Vector3.one * 0.77f;

            var t = tgo.GetComponent<EF1123Target>() ?? tgo.AddComponent<EF1123Target>();
            var so = new SerializedObject(t);
            so.FindProperty("ability").intValue = 1;                       // FREEZE
            so.FindProperty("title").stringValue = "도개교 경첩";
            so.FindProperty("solution").stringValue = "다리를 알맞은 경사에서 멈췄습니다. 물레방아 발코니로 건너가세요.";
            so.FindProperty("course").intValue = wing.course;
            so.FindProperty("order").intValue = wing.order + 1;
            so.FindProperty("station").objectReferenceValue = w02;
            so.FindProperty("stationRadius").floatValue = Mathf.Max(wing.stationRadius, 2.5f);
            so.FindProperty("timedPlatform").objectReferenceValue = sm;
            so.FindProperty("requiredMotion").objectReferenceValue = bm;     // 다리가 다 내려온 뒤에만
            so.FindProperty("grabFirst").boolValue = false;
            so.FindProperty("flyTo").objectReferenceValue = null;
            so.FindProperty("tutorial").boolValue = false;
            so.FindProperty("hide").arraySize = 0;
            so.FindProperty("reveal").arraySize = 0;

            // 날개 FLOW 가 열던 것 중 풍차·다리 말고 나머지(발코니 문 등)는 FREEZE 단계로 넘김
            var spindleMotion = Under(W, "Service windmill spindle")?.GetComponent<EF1123Motion>();
            var keep = new List<EF1123Motion>(); var moved = new List<EF1123Motion> { sm };
            foreach (var m in wing.motions)
                if (m == bm || m == spindleMotion) keep.Add(m); else if (m != null) moved.Add(m);
            var mp = so.FindProperty("motions"); mp.arraySize = moved.Count;
            for (int i = 0; i < moved.Count; i++) mp.GetArrayElementAtIndex(i).objectReferenceValue = moved[i];
            so.ApplyModifiedProperties();
            Undo.RecordObject(wing, "wing motions"); wing.motions = keep.ToArray();

            // World: 흔들림 장치 등록 + W 코스 단계에 추가
            var wso = new SerializedObject(world);
            var motions = wso.FindProperty("motions");
            motions.arraySize++; motions.GetArrayElementAtIndex(motions.arraySize - 1).objectReferenceValue = sm;
            var courses = wso.FindProperty("courses");
            var steps = courses.GetArrayElementAtIndex(wing.course).FindPropertyRelative("steps");
            steps.arraySize++; steps.GetArrayElementAtIndex(steps.arraySize - 1).objectReferenceValue = t;
            wso.ApplyModifiedProperties();

            log.Add("4) 도개교: FLOW 로 내려온 뒤 경첩에서 ±" + SwayDeg + "° 흔들림 → '도개교 경첩'에 FREEZE 로 멈춤 (W 별은 이 단계 뒤에 열림)");
        }

        static void PressurePlate(List<string> log)
        {
            var pollen = Find("D dancing pollen");
            if (pollen == null) { log.Add("6) D dancing pollen 없음"); return; }
            if (Find("D hidden pressure plate") != null) { log.Add("6) 압력발판: 이미 있음"); return; }
            bool has = false; var b = new Bounds();
            foreach (var r in pollen.GetComponentsInChildren<Renderer>(true))
            { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
            if (!has) { log.Add("6) 낙엽 범위를 못 잼"); return; }

            GameObject plate;
            var guid = AssetDatabase.FindAssets("Rock1 t:Prefab").Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => System.IO.Path.GetFileNameWithoutExtension(p) == "Rock1" && p.Contains("FantasyEnvironments"));
            if (guid != null) plate = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(guid), pollen.parent);
            else { plate = GameObject.CreatePrimitive(PrimitiveType.Cylinder); plate.transform.SetParent(pollen.parent, true); }
            Undo.RegisterCreatedObjectUndo(plate, "plate");
            plate.name = "D hidden pressure plate";
            // 납작한 돌판, 지름 약 1.4m, 두께 0.15m
            var t = plate.transform; t.rotation = Quaternion.identity; t.localScale = Vector3.one;
            var rb = plate.GetComponentInChildren<Renderer>().bounds;
            t.localScale = new Vector3(1.4f / Mathf.Max(rb.size.x, 0.01f), 0.15f / Mathf.Max(rb.size.y, 0.01f), 1.4f / Mathf.Max(rb.size.z, 0.01f));
            rb = plate.GetComponentInChildren<Renderer>().bounds;
            t.position += new Vector3(b.center.x - rb.center.x, b.min.y - rb.min.y - 0.05f, b.center.z - rb.center.z);
            foreach (var c in plate.GetComponentsInChildren<Collider>()) Undo.DestroyObjectImmediate(c);   // 밟아도 아무 일 없음 (스크립트 나중에)
            log.Add("6) D 낙엽 더미 밑에 숨겨진 압력발판(돌판) 배치. 아직 동작 스크립트 없음");
        }

        // ------------------------------------------------------------------ 도우미
        static Transform Find(string name) =>
            UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == name);

        static Transform Under(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        static EF1123Target Target(string title) =>
            UnityEngine.Object.FindObjectsByType<EF1123Target>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.title == title || t.name == title);
    }
}
#endif
