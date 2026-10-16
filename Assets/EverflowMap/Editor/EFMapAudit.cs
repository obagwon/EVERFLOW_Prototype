using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowMap.Editor
{
    public static class EFMapAudit
    {
        public const string ScenePath = "Assets/Scenes/Everflow_Map.unity";
        [MenuItem("Tools/EVERFLOW/Map/Audit Models and Scene")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var report = new StringBuilder();
            foreach (var root in scene.GetRootGameObjects())
            {
                report.AppendLine($"ROOT {root.name} pos={root.transform.position:F3} rot={root.transform.eulerAngles:F3} scale={root.transform.lossyScale:F3}");
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var r = t.GetComponent<Renderer>();
                    report.AppendLine($"  {AnimationUtility.CalculateTransformPath(t, root.transform)} pos={t.position:F3} rot={t.eulerAngles:F2} scale={t.lossyScale:F3}" +
                        (r == null ? "" : $" BOUNDS center={r.bounds.center:F3} size={r.bounds.size:F3} mats={string.Join(",", r.sharedMaterials.Select(m => m == null ? "MISSING" : m.name + ":" + m.shader.name))}"));
                }
            }
            foreach (string path in Directory.GetFiles("Assets/map_ver1/fbx", "*.fbx").OrderBy(p => p))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                report.AppendLine($"ASSET {path} scale={importer.globalScale} fileScale={importer.fileScale} colliders={importer.addCollider} animationType={importer.animationType}");
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")))
                {
                    var curves = AnimationUtility.GetCurveBindings(clip);
                    report.AppendLine($"  CLIP {clip.name} fps={clip.frameRate} length={clip.length} keys={curves.Length} loop={clip.isLooping}");
                    foreach (var binding in curves) report.AppendLine($"    {binding.path} {binding.type.Name} {binding.propertyName}");
                }
            }
            Physics.SyncTransforms();
            var terrain = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "EF_Terrain").gameObject;
            var height = new StringBuilder("x,z,y,normalY\n");
            Vector3 origin = terrain.transform.position;
            for (float z = -70; z <= 50; z += 1)
                for (float x = -60; x <= 75; x += 1)
                {
                    var ray = new Ray(origin + new Vector3(x, 70, z), Vector3.down);
                    if (Physics.Raycast(ray, out var hit, 100) && hit.collider.transform.IsChildOf(terrain.transform))
                        height.AppendLine($"{x},{z},{hit.point.y:F3},{hit.normal.y:F3}");
                }
            Directory.CreateDirectory("docs/map-integration");
            File.WriteAllText("docs/map-integration/terrain-height.csv", height.ToString());
            File.WriteAllText("docs/map-integration/model-audit.txt", report.ToString());
            Debug.Log("MAP AUDIT WRITTEN");
        }
    }
}
