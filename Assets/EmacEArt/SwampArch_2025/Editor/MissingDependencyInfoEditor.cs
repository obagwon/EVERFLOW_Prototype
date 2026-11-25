using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MissingDependencyInfo))]
public class MissingDependencyInfoEditor : Editor
{
    private Texture2D coverImage;
    private GUIStyle headlineStyle;
    private GUIStyle bodyTextStyle;

    private void OnEnable()
    {
        string pathPNG = "Assets/EmacEArt/SwampArch_2025/Scripts/Swamp_Cover.png";
        string pathJPG = "Assets/EmacEArt/SwampArch_2025/Scripts/Swamp_Cover.jpg";

        coverImage = AssetDatabase.LoadAssetAtPath<Texture2D>(pathPNG);
        
        if (coverImage == null)
        {
            coverImage = AssetDatabase.LoadAssetAtPath<Texture2D>(pathJPG);
        }
    }

    public override void OnInspectorGUI()
    {
        MissingDependencyInfo info = (MissingDependencyInfo)target;

        // Inicjalizacja styli
        if (headlineStyle == null)
        {
            headlineStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                richText = true,
                margin = new RectOffset(5, 5, 5, 5)
            };
        }
        if (bodyTextStyle == null)
        {
            bodyTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                richText = true,
                margin = new RectOffset(5, 5, 5, 5)
            };
        }

        EditorGUILayout.Space(15);

        // 1. Skalowana i klikalna grafika
        if (coverImage != null)
        {
            float imageAspect = (float)coverImage.width / coverImage.height;
            float maxWidth = Mathf.Min(EditorGUIUtility.currentViewWidth - 45f, coverImage.width);
            float calculatedHeight = maxWidth / imageAspect;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            Rect buttonRect = GUILayoutUtility.GetRect(maxWidth, calculatedHeight, GUILayout.Width(maxWidth), GUILayout.Height(calculatedHeight));
            GUI.DrawTexture(buttonRect, coverImage, ScaleMode.ScaleToFit);

            if (GUI.Button(buttonRect, GUIContent.none, GUIStyle.none))
            {
                Application.OpenURL(info.assetUrl);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            
            EditorGUILayout.Space(20);
        }

        // 2. Treść Marketingowa
        GUILayout.Label("<b>The Synergy of ModulaCraft Series</b>", headlineStyle);
        EditorGUILayout.Space(10);

        GUILayout.Label("Thank you for choosing our architectural package! This pack is a fully independent, complete set containing all the high-quality building models.", bodyTextStyle);
        EditorGUILayout.Space(10);

        GUILayout.Label("To showcase the <b>maximum potential</b> of these structures, the demo scene was pre-configured to work in harmony with our natural environment assets. All flora elements are already placed in the scene but currently remain hidden.", bodyTextStyle);
        EditorGUILayout.Space(10);

        GUILayout.Label("Simply import the package below, and both systems will merge, <b>automatically bringing the scene to life</b> with lush, dense vegetation.", bodyTextStyle);
        EditorGUILayout.Space(15);

        // 3. Sekcja UPGRADE
        GUILayout.Label("<b>PRO TIP:</b> As an owner of this architectural pack, you are eligible for a <b><color=#FF5555>65% DISCOUNT</color> Upgrade</b> on the Asset Store. Complete your set with the nature environment at a <b>significantly discounted price!</b>", bodyTextStyle);
        
        EditorGUILayout.Space(20);

        // 4. Przycisk Akcji
        GUIStyle mainButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontStyle = FontStyle.Bold,
            fixedHeight = 45,
            wordWrap = true,
            fontSize = 14
        };

        if (GUILayout.Button("Get ModulaCraft Swamp Builder PRO - Nature Mega Pack\n(65% OFF Upgrade)", mainButtonStyle))
        {
            Application.OpenURL(info.assetUrl);
        }

        EditorGUILayout.Space(15);
    }
}