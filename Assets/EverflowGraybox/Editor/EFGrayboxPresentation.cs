using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowGraybox.Editor
{
    public static partial class EFGrayboxBuilder
    {
        // Additive upgrade: preserves every existing puzzle, reference and authored transform.
        static void Polish(EFWorld world)
        {
            if (GameObject.Find("GrayboxPresentation_v1") != null) return;
            var root = new GameObject("GrayboxPresentation_v1").transform;
            root.SetParent(GameObject.Find("Terrain").transform);
            var stone = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Stone.mat");
            var wall = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Wall.mat");
            var exit = world.environment.exitPaths[5].transform;
            var sections = exit.GetComponentsInChildren<Transform>(true).Where(t => t.name == "SafeConnection").ToArray();
            if (sections.Length == 3)
            {
                // Upgrade the initial generated scene: end the slope before the wide corner pad.
                Vector3 a = new Vector3(-12,1,-24), b = new Vector3(-12,3,-10);
                var slope = sections[1];
                slope.position = (a+b)*.5f-Vector3.up*.35f;
                slope.rotation = Quaternion.LookRotation((b-a).normalized,Vector3.up);
                slope.localScale = new Vector3(6,.7f,Vector3.Distance(a,b)+.15f);
                var runout = GameObject.CreatePrimitive(PrimitiveType.Cube); runout.name = "UpperRampRunout";
                runout.transform.SetParent(exit); runout.transform.position = new Vector3(-12,2.65f,-8);
                runout.transform.localScale = new Vector3(6,.7f,4.15f);
                runout.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Floor.mat");
            }
            GameObject Block(string name, Vector3 position, Vector3 scale, Quaternion rotation, Material mat)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
                go.transform.SetParent(root); go.transform.SetPositionAndRotation(position,rotation); go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = mat;
                // Facades never create extra footholds underneath the puzzle gaps.
                Object.DestroyImmediate(go.GetComponent<Collider>());
                return go;
            }
            foreach (var floor in GameObject.Find("Puzzles").GetComponentsInChildren<Transform>().Where(t =>
                t.GetComponent<Renderer>() != null && (t.name.EndsWith("_Start") || t.name.EndsWith("_Landing") ||
                t.name.EndsWith("_UpperLedge") || t.name.EndsWith("_Middle") || t.name.EndsWith("_FinalPlatform"))))
            {
                Vector3 size = floor.localScale;
                Block(floor.name + "_Cliff", floor.position - Vector3.up * 3.2f,
                    new Vector3(size.x*.97f,6,size.z*.97f),floor.rotation,stone);
                Block(floor.name + "_LowerRock",floor.position - Vector3.up * 7,
                    new Vector3(size.x*.76f,3.5f,size.z*.8f),floor.rotation,wall);
                if (floor.name.StartsWith("P2_") || floor.name.StartsWith("P3_"))
                    for (int side=-1;side<=1;side+=2)
                        Block("RuinPier",floor.position + floor.right*side*(size.x*.5f-1)+Vector3.up*1.5f,
                            new Vector3(.7f,3,.7f),floor.rotation,stone);
            }
            Block("StartRock",new Vector3(0,-5,-55),new Vector3(23,6,19),Quaternion.identity,wall);
            Block("PlazaRock",new Vector3(0,-3,3),new Vector3(27,8,13),Quaternion.identity,wall);
            // The wheel is in XY: its fixed axle runs along Z, independently of the rotating spokes.
            GameObject.Find("FixedAxle").transform.localScale = new Vector3(1.2f,1.2f,8);
            // Simple neutral chevrons on safe surfaces communicate the loop without debug labels.
            foreach (var stage in world.stages)
            {
                Transform t = stage.spawn.parent;
                float y = stage.spawn.localPosition.y - 1.1f;
                for (int j=0;j<2;j++)
                    for (int side=-1;side<=1;side+=2)
                        Block("RouteChevron",t.TransformPoint(new Vector3(side*.35f,y,-1+j*1.2f)),
                            new Vector3(.12f,.06f,1),t.rotation * Quaternion.Euler(0,side*-42,0),stone);
            }
            // First turn is at the left edge of the start, within the initial camera view.
            for (int j=0;j<4;j++)
                Block("StartRoute",new Vector3(-3-j*2,.04f,-58),new Vector3(1,.07f,.35f),Quaternion.identity,stone);
        }

        [MenuItem("Tools/EVERFLOW/Graybox/Upgrade World Presentation")]
        public static void UpgradeAndValidate()
        {
            if (EditorApplication.isPlaying) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            Polish(Object.FindFirstObjectByType<EFWorld>());
            EditorSceneManager.SaveScene(scene);
            Validate();
            Debug.Log("GRAYBOX PRESENTATION UPGRADE PASS");
        }
    }
}
