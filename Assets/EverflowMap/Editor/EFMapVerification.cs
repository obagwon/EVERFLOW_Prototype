using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowMap.Editor
{
    public static class EFMapVerification
    {
        [MenuItem("Tools/EVERFLOW/Map/Run Saved Scene and Regression Checks")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play mode first.");
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var report=new StringBuilder("Unity Edit Mode checks; separate from physical Play Mode tests.\n");
            void Check(bool condition,string message)
            {report.AppendLine((condition?"PASS ":"FAIL ")+message);if(!condition)throw new Exception(message);}
            try
            {
                var scene=EditorSceneManager.OpenScene(EFMapAudit.ScenePath);
                byte[] before=File.ReadAllBytes(EFMapAudit.ScenePath);
                int Count()=>scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Transform>(true).Length);
                int count=Count();EFMapSetup.Apply();scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                EFMapSetup.Apply();scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                Check(Count()==count&&before.SequenceEqual(File.ReadAllBytes(EFMapAudit.ScenePath)),"Apply twice changes neither scene bytes nor object count");
                EFMapCameraSetup.Apply();EFMapCameraSetup.Apply();scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                Check(Count()==count&&before.SequenceEqual(File.ReadAllBytes(EFMapAudit.ScenePath)),"Camera Apply twice preserves object count, saved framing and scene bytes");
                EFMapAbilityTests.VerifySaved();scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                report.AppendLine("PASS Ability repair twice preserves scene/prefab bytes and explicit collider references");
                var w=UnityEngine.Object.FindFirstObjectByType<EFMapWorld>();
                Check(UnityEngine.Object.FindObjectsByType<EFMapCamera>(FindObjectsSortMode.None).Length==1&&w.player.follow.GetComponent<Camera>()==w.player.view,
                    "Exactly one free-look controller owns the gameplay camera");
                Check(w.player.follow.minPitch<w.player.follow.maxPitch&&w.player.follow.followSmoothTime>0&&w.player.follow.collisionReturnSmoothTime>0,
                    "Saved camera limits and smoothing settings are valid");
                Check(UnityEngine.Object.FindObjectsByType<EFMapWorld>(FindObjectsSortMode.None).Length==1,"Exactly one map manager");
                Check(w.stages.SelectMany(s=>s.nodes).All(n=>n.world==w)&&w.stages.SelectMany(s=>s.nodes).Distinct().Count()==w.stages.Sum(s=>s.nodes.Length),"Unique node registrations and manager references");
                var actions=UnityEngine.Object.FindObjectsByType<EFMapAnimation>(FindObjectsSortMode.None);
                Check(actions.Length==13&&actions.All(a=>a.clip!=null&&a.clip.frameRate==24&&!a.clip.isLooping&&a.takeFirstFrame==1),"13 actual clips: imported 24 FPS, take starts at frame 1, loop off");
                Check(w.stages[2].restoration.Length==0,"No invented Restore_P3 asset or requirement");
                foreach(var n in w.stages.SelectMany(s=>s.nodes))
                    Check(n.stage>=0&&n.stage<6&&w.stages[n.stage].nodes.Contains(n),"Stage membership "+n.name);
                EverflowMVP.Editor.EFReleaseSetup.ValidateScenes();report.AppendLine("PASS Original title UI/logo/event/input and new map references via release validator");
                EverflowMVP.Editor.EFReleaseTests.Run();report.AppendLine("PASS Existing release version transaction tests; no version increment");
                EverflowMVP.Editor.EFStateTests.Run();report.AppendLine("PASS Existing MVP ability/power state tests");
                EditorSceneManager.OpenScene(EverflowGraybox.Editor.EFGrayboxBuilder.ScenePath);
                EverflowGraybox.Editor.EFGrayboxBuilder.Validate();report.AppendLine("PASS Existing graybox scene reference validator (no save)");
                EditorSceneManager.OpenScene("Assets/EverflowMVP/Scenes/Everflow_6Puzzles.unity");
                var old=UnityEngine.Object.FindFirstObjectByType<EverflowMVP.EFWorld>();
                Check(old!=null&&old.stages.Length==6&&old.player!=null,"Original six-puzzle scene still loads with its original player and world");
                EditorSceneManager.OpenScene(EFMapAudit.ScenePath);
                report.AppendLine("OVERALL PASS. No standalone player build performed.");
            }
            catch(Exception e){report.AppendLine("ERROR "+e);throw;}
            finally{Directory.CreateDirectory(EFMapAbilityTests.Evidence);File.WriteAllText(EFMapAbilityTests.Evidence+"/editor-test-results.txt",report.ToString());}
            Debug.Log(report.ToString());
        }
    }
}
