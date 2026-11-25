using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class MissingDependencyInfo : MonoBehaviour
{
    [HideInInspector]
    public string assetUrl = "https://assetstore.unity.com/packages/3d/environments/landscapes/modulacraft-swamp-builder-pro-nature-mega-pack-123765";

#if UNITY_EDITOR
    private void OnEnable()
    {
        if (Application.isPlaying) return;

        EditorApplication.delayCall += () =>
        {
            if (this != null && gameObject != null)
            {
                Selection.activeGameObject = gameObject;

                System.Type inspectorType = System.Type.GetType("UnityEditor.InspectorWindow,UnityEditor");
                if (inspectorType != null)
                {
                    EditorWindow.GetWindow(inspectorType, false, "Inspector", true);
                }
            }
        };
    }
#endif
}