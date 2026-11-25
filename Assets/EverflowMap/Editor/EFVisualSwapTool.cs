// EVERFLOW - 그레이박스 → 에셋 교체 도구
// 위치: Assets/EverflowMap/Editor/ 아래에 넣기 (Editor 폴더 안이어야 함)
// 메뉴: Tools > EVERFLOW > Visual Swap
//
// 하는 일
//  - 선택한 그레이박스 오브젝트의 MeshRenderer만 끔 (콜라이더, EF1123Motion 등 스크립트는 그대로)
//  - 지정한 프리팹을 그 오브젝트의 "자식"으로 넣고, 그레이박스 크기에 맞춰 배치
//  - 자식으로 넣기 때문에 문·잎처럼 움직이는 오브젝트도 연출이 그대로 따라감
//  - 모든 작업은 Ctrl+Z로 되돌릴 수 있음

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EverflowMap.EditorTools
{
    public sealed class EFVisualSwapTool : EditorWindow
    {
        enum FitMode { 크기그대로, 바닥면에맞춤, 전체크기에맞춤 }
        enum AlignMode { 바닥에맞춤, 가운데에맞춤 }

        GameObject prefab;
        FitMode fit = FitMode.바닥면에맞춤;
        AlignMode align = AlignMode.바닥에맞춤;
        float extraScale = 1f;
        bool replaceOldVisual = true;

        const string VisualPrefix = "Visual_";

        [MenuItem("Tools/EVERFLOW/Visual Swap")]
        static void Open() => GetWindow<EFVisualSwapTool>("Visual Swap");

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "1) Hierarchy에서 그레이박스 오브젝트를 선택 (여러 개 가능)\n" +
                "2) 넣을 프리팹 지정\n" +
                "3) [모델 넣기] 클릭\n\n" +
                "그레이박스의 렌더러만 꺼지고 콜라이더·스크립트는 남습니다.",
                MessageType.Info);

            prefab = (GameObject)EditorGUILayout.ObjectField("넣을 프리팹", prefab, typeof(GameObject), false);
            fit = (FitMode)EditorGUILayout.EnumPopup("크기 맞추기", fit);
            align = (AlignMode)EditorGUILayout.EnumPopup("높이 맞추기", align);
            extraScale = EditorGUILayout.Slider("추가 배율", extraScale, 0.2f, 3f);
            replaceOldVisual = EditorGUILayout.Toggle("이전 Visual_ 지우고 교체", replaceOldVisual);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("선택된 오브젝트: " + Selection.gameObjects.Length + "개");

            using (new EditorGUI.DisabledScope(prefab == null || Selection.gameObjects.Length == 0))
            {
                if (GUILayout.Button("모델 넣기", GUILayout.Height(32)))
                    foreach (var go in Selection.gameObjects) Swap(go);
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(Selection.gameObjects.Length == 0))
            {
                if (GUILayout.Button("렌더러만 끄기 (보이지 않는 벽·판정용 큐브)"))
                    foreach (var go in Selection.gameObjects) SetGreyboxVisible(go, false);
                if (GUILayout.Button("그레이박스 다시 보이기"))
                    foreach (var go in Selection.gameObjects) SetGreyboxVisible(go, true);
            }
        }

        void OnSelectionChange() => Repaint();

        static void SetGreyboxVisible(GameObject go, bool visible)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;
            Undo.RecordObject(mr, visible ? "Show greybox" : "Hide greybox");
            mr.enabled = visible;
        }

        void Swap(GameObject target)
        {
            Transform parent = target.transform;
            var greyRenderer = target.GetComponent<MeshRenderer>();

            // 그레이박스 크기 (렌더러가 꺼져 있어도 bounds는 읽힘)
            Bounds grey;
            if (greyRenderer != null) grey = greyRenderer.bounds;
            else if (!TryGetBounds(target, out grey, excludeVisuals: true))
            {
                Debug.LogWarning($"[Visual Swap] {target.name}: 크기를 알 수 없어 건너뜀", target);
                return;
            }

            Undo.SetCurrentGroupName("Visual Swap " + target.name);
            int group = Undo.GetCurrentGroup();

            if (replaceOldVisual)
            {
                var old = new List<GameObject>();
                foreach (Transform child in parent)
                    if (child.name.StartsWith(VisualPrefix)) old.Add(child.gameObject);
                foreach (var o in old) Undo.DestroyObjectImmediate(o);
            }

            if (greyRenderer != null)
            {
                Undo.RecordObject(greyRenderer, "Hide greybox");
                greyRenderer.enabled = false;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, target.scene);
            Undo.RegisterCreatedObjectUndo(instance, "Create visual");
            instance.name = VisualPrefix + prefab.name;
            Undo.SetTransformParent(instance.transform, parent, "Parent visual");

            var t = instance.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;

            // 부모(그레이박스)가 3×0.3×1처럼 찌그러진 크기여도 모델은 찌그러지지 않게 보정
            Vector3 ps = parent.lossyScale;
            Vector3 inv = new Vector3(SafeInv(ps.x), SafeInv(ps.y), SafeInv(ps.z));
            t.localScale = inv;

            if (!TryGetBounds(instance, out Bounds model, excludeVisuals: false))
            {
                Debug.LogWarning($"[Visual Swap] {prefab.name}: 렌더러가 없는 프리팹", prefab);
                Undo.CollapseUndoOperations(group);
                return;
            }

            float f = 1f;
            if (fit == FitMode.바닥면에맞춤)
                f = Mathf.Min(Ratio(grey.size.x, model.size.x), Ratio(grey.size.z, model.size.z));
            else if (fit == FitMode.전체크기에맞춤)
                f = Mathf.Min(Ratio(grey.size.x, model.size.x), Ratio(grey.size.y, model.size.y),
                              Ratio(grey.size.z, model.size.z));
            f *= extraScale;
            t.localScale = inv * f;

            TryGetBounds(instance, out model, excludeVisuals: false);
            Vector3 targetPoint = new Vector3(grey.center.x,
                align == AlignMode.바닥에맞춤 ? grey.min.y : grey.center.y, grey.center.z);
            Vector3 modelPoint = new Vector3(model.center.x,
                align == AlignMode.바닥에맞춤 ? model.min.y : model.center.y, model.center.z);
            t.position += targetPoint - modelPoint;

            Undo.CollapseUndoOperations(group);
        }

        static bool IsInsideVisual(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
                if (t.name.StartsWith(VisualPrefix)) return true;
            return false;
        }

        static float SafeInv(float v) => Mathf.Abs(v) < 1e-5f ? 1f : 1f / v;
        static float Ratio(float want, float have) => have < 1e-4f ? 1f : want / have;

        static bool TryGetBounds(GameObject root, out Bounds bounds, bool excludeVisuals)
        {
            bounds = default;
            bool found = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                if (excludeVisuals && IsInsideVisual(r.transform, root.transform)) continue;
                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return found;
        }
    }
}
