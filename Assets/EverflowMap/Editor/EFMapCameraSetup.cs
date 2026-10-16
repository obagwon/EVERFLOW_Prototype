using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowMap.Editor
{
    public static class EFMapCameraSetup
    {
        [MenuItem("Tools/EVERFLOW/Map/Apply Third Person Camera")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play mode first.");
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var scene=EditorSceneManager.OpenScene(EFMapAudit.ScenePath);
            var world=UnityEngine.Object.FindFirstObjectByType<EFMapWorld>();
            if(world==null||world.player==null||world.player.follow==null)throw new Exception("Map camera/player references missing.");
            var follow=world.player.follow;var camera=world.player.view;
            if(follow.GetComponent<Camera>()!=camera)throw new Exception("Follow controller must own the gameplay Camera.");
            if(follow.GetComponents<EFMapCamera>().Length!=1||follow.GetComponent<EverflowGraybox.EFCamera>()!=null||follow.GetComponent<EverflowMVP.EFCamera>()!=null)
                throw new Exception("Multiple camera controllers; inspect before applying.");
            Directory.CreateDirectory("docs/camera");
            if(!File.Exists("docs/camera/Everflow_Map.before-camera.unity.txt"))
                File.Copy(EFMapAudit.ScenePath,"docs/camera/Everflow_Map.before-camera.unity.txt");
            if(follow.setupVersion<1)
            {
                follow.distance=7;follow.lookHeight=.7f;follow.shoulderOffset=.6f;
                follow.sensitivity=.14f;follow.verticalSensitivity=.85f;follow.minPitch=-35;follow.maxPitch=70;
                follow.followSmoothTime=.1f;follow.verticalSmoothTime=.14f;follow.maxFollowLag=1.5f;
                follow.collisionRadius=.25f;follow.collisionPadding=.12f;follow.collisionReturnSmoothTime=.25f;
                follow.collisionMask=~(1<<2);follow.collisionIgnoreRoots=new[]{world.player.transform};
                follow.assistRides=true;follow.rideAssistDelay=1.25f;follow.rideTurnSpeed=90;follow.rideLookAhead=1.5f;
                // Start faces the first flower at model Z32 from the existing spawn at Z34.
                world.stages[0].spawn.rotation=Quaternion.Euler(0,180,0);
                world.player.transform.rotation=world.stages[0].spawn.rotation;
                follow.yaw=180;follow.pitch=follow.recenterPitch=18;follow.setupVersion=1;
                camera.nearClipPlane=.1f;
                follow.enabled=camera.enabled=true;
                if(world.openingCamera!=null)world.openingCamera.enabled=false;
                if(world.endingCamera!=null)world.endingCamera.enabled=false;
                follow.Snap();
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new Exception("Could not save Map camera changes.");
                const string path="Assets/EverflowMap/Prefabs/EFMapPlayer.prefab";
                var prefab=PrefabUtility.LoadPrefabContents(path);
                try{prefab.GetComponent<EFMapPlayer>().turnSharpness=14;PrefabUtility.SaveAsPrefabAsset(prefab,path);}
                finally{PrefabUtility.UnloadPrefabContents(prefab);}
                AssetDatabase.SaveAssets();
            }
            EFMapSetup.Validate();
            Debug.Log("THIRD PERSON CAMERA SAVED: one EFMapCamera owner; mouse free look, center aim, C recenter, Esc pause. No package changes.");
        }
    }
}
