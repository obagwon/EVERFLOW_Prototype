using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowMap.Editor
{
    public static class EFDemoFeedbackTools
    {
        [MenuItem("Tools/EVERFLOW/Demo/Install Feedback")]
        public static void Install()
        {
            if(Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=EFV2PuzzleBuild.Scene)throw new Exception("Open puzzle scene outside Play.");
            Directory.CreateDirectory("docs/demo-feedback");
            if(!File.Exists("docs/demo-feedback/before.unity.snapshot"))File.Copy(EFV2PuzzleBuild.Scene,"docs/demo-feedback/before.unity.snapshot");
            var world=UnityEngine.Object.FindFirstObjectByType<EFV2PuzzleWorld>();
            var component=world.GetComponent<EFDemoFeedback>();if(component==null)component=Undo.AddComponent<EFDemoFeedback>(world.gameObject);
            component.world=world;component.particleMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/EverflowWater/Network/ImpactSpray.mat");
            string folder="Assets/EverflowWater/Feedback";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var mat=AssetDatabase.LoadAssetAtPath<Material>(folder+"/FeedbackLine.mat");
            if(mat==null){mat=new Material(Shader.Find("Sprites/Default"));AssetDatabase.CreateAsset(mat,folder+"/FeedbackLine.mat");}
            component.lineMaterial=mat;EditorUtility.SetDirty(component);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(world.gameObject.scene);EditorSceneManager.SaveScene(world.gameObject.scene);
        }
    }
}
