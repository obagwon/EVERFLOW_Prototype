using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EverflowMap.Editor
{
    public static class EFMvpSetup
    {
        public const string ScenePath="Assets/Scenes/Everflow_Map_MVP.unity";
        public const string Evidence="docs/everflow_mvp/evidence/integration-20261017";
        static Transform root;
        static Vector3 origin;
        static Material stone,green,ice;

        public static string Hash(string path)
        {
            using(var sha=SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
        }
        public static string RuntimeFingerprint()
        {
            return string.Join("|",Directory.GetFiles("Assets/EverflowMap/Runtime","*.cs").OrderBy(p=>p,StringComparer.Ordinal).Select(p=>p+":"+Hash(p)));
        }
        public static void Guard()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Stop Play and wait for compilation/import.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene edits preserved; save or review before this command.");
        }
        [MenuItem("Tools/EVERFLOW/MVP Integration/Create or Open MVP")]
        public static void Create()
        {
            Guard(); Directory.CreateDirectory(Evidence);
            if(File.Exists(ScenePath))
            {
                var existing=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
                var current=World(existing);
                Validate(current);
                if(RepairLabels(current,false))
                {
                    File.Copy(ScenePath,Evidence+"/mvp-before-label-repair-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")+".unity",false);
                    RepairLabels(current);
                    EditorSceneManager.MarkSceneDirty(existing);
                    if(!EditorSceneManager.SaveScene(existing)) throw new IOException("Could not save repaired MVP labels.");
                }
                Debug.Log("Existing MVP preserved; legacy label migration is limited, backed up and idempotent. User-edited label text is retained.");
                return;
            }
            var passed=Directory.GetFiles("docs/everflow_mvp/evidence","p3-checks-*.txt")
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if(passed==null || !File.ReadAllText(passed).Contains("overall PASS."))
                throw new InvalidOperationException("Latest P3 checks must PASS before creating MVP.");
            string original=Hash(EFMapAudit.ScenePath), previous=Hash(EFCanalExperimentSetup.ScenePath);
            var scene=EditorSceneManager.OpenScene(EFCanalExperimentSetup.ScenePath,OpenSceneMode.Single);
            if(!EditorSceneManager.SaveScene(scene,ScenePath,true)) throw new IOException("Could not create MVP copy.");
            scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var world=World(scene);
            Build(world);
            RepairLabels(world);
            Validate(world);
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save MVP.");
            if(original!=Hash(EFMapAudit.ScenePath) || previous!=Hash(EFCanalExperimentSetup.ScenePath))
                throw new IOException("Protected scene changed; inspect external activity. No automatic rollback/deletion.");
            File.WriteAllText(Evidence+"/scene-created.txt",DateTime.UtcNow.ToString("O")+"\n"+ScenePath+"\nsha256 "+Hash(ScenePath)+"\nOriginal and P3 copy unchanged.\n");
            Debug.Log("MVP saved. Run MVP Checks before publishing title/build settings.");
        }
        static EFMapWorld World(Scene scene) => scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EFMapWorld>(true)).Single();
        static EFMapNode Node(EFMapWorld w,int stage,MapKind kind) => w.stages[stage].nodes.Single(n=>n.kind==kind);
        static bool RepairLabels(EFMapWorld w,bool apply=true)
        {
            bool changed=false;
            var o=w.GetComponentInChildren<EFCanalExperiment>().originalTerrain.position;
            foreach(var label in w.GetComponentsInChildren<TextMesh>(true))
            {
                if(label.text!=label.name) continue; // Never overwrite a user's edited label.
                if(label.name=="A stays available. PULSE source, FREEZE B, cross west.")
                { changed=true; if(apply) { label.text="A SAFE / B FREEZE"; label.characterSize=.045f; label.transform.position=o+new Vector3(29.8f,10.5f,-39); } }
                else if(label.name=="BLOOM roots -> maintenance PULSE -> FREEZE -> CROSS -> final PULSE")
                { changed=true; if(apply) { label.text="P6: BLOOM"; label.characterSize=.045f; label.transform.position=o+new Vector3(-8.1f,17.8f,2.5f); } }
                else if(label.name=="FINAL CONTROL : PULSE (3)")
                { changed=true; if(apply) { label.text="FINAL PULSE [3]"; label.characterSize=.045f; } }
            }
            return changed;
        }
        static void Build(EFMapWorld w)
        {
            origin=w.GetComponentInChildren<EFCanalExperiment>().originalTerrain.position;
            root=new GameObject("MVP Integration - editable scene objects").transform;
            root.SetParent(w.transform,false);
            var canal=w.GetComponentInChildren<EFCanalExperiment>();
            stone=canal.departureFloor.GetComponent<Renderer>().sharedMaterial;
            ice=canal.water.iceMaterial;
            green=Node(w,4,MapKind.Root).motion.GetComponentsInChildren<Renderer>(true).First().sharedMaterial;

            // Planning rotary P5 / UI P3 / index2: fixed A and one rotating B.
            // The original bridge has a single Frozen/Angle state; separate
            // A/B latches would replace its mechanic. Fixed A preserves access.
            var rotary=Node(w,2,MapKind.Bridge);
            rotary.tolerance=40; rotary.bidirectionalAlignment=true;
            var fixedA=Box("P3 UI - fixed connection A",new Vector3(36,7.4f,-42),new Vector3(12,.5f,3.5f),stone);
            var island=Box("P3 UI - middle safe judgement deck",new Vector3(29.8f,7.4f,-42),new Vector3(3.6f,.5f,5),stone);
            var cp=Mark("Rotary middle checkpoint",new Vector3(29.8f,8.77f,-42));
            var rotaryState=root.gameObject.AddComponent<EFMvpRotary>();
            rotaryState.world=w; rotaryState.grounding=canal; rotaryState.middle=island;
            rotaryState.checkpoint=cp; rotaryState.source=Node(w,2,MapKind.Source);
            var bTarget=Box("Rotary B FREEZE marker",new Vector3(26,8.4f,-42),Vector3.one*.7f,ice);
            bTarget.isTrigger=true;
            bTarget.gameObject.AddComponent<EFMapAbilityTarget>().receiver=rotary;
            Sign("A stays available. PULSE source, FREEZE B, cross west.",new Vector3(30,9.2f,-39),.12f);

            // Planning/UI P6, index5. Keep model animation ownership; add broad
            // route surfaces above the existing channel and terrain.
            var finale=root.gameObject.AddComponent<EFMvpFinale>();
            finale.world=w; finale.grounding=canal;
            finale.growth=Node(w,5,MapKind.Repair);
            finale.machine=Node(w,5,MapKind.Source);
            finale.connection=Node(w,5,MapKind.Slate);
            finale.gate=Node(w,5,MapKind.Gate);
            finale.connection.tolerance=22;
            Stairs("P6 approach stairs",new Vector3(-6,15.1f,2),new Vector3(-6,19.3f,-5),20);
            finale.bloomRoute=Box("BLOOM root approach",new Vector3(-6,19.05f,-9),new Vector3(3,.5f,8),green);
            finale.nearDeck=Box("Maintenance safe deck",new Vector3(-3,19.05f,-13.5f),new Vector3(8,.5f,4),stone);
            finale.frozenRoute=Box("FREEZE safe channel crossing",new Vector3(0,19.05f,-8.5f),new Vector3(3,.5f,8),ice);
            finale.frozenRoute.gameObject.AddComponent<EFMapAbilityTarget>().receiver=finale.connection;
            finale.farDeck=Box("Final PULSE deck",new Vector3(0,19.05f,-2),new Vector3(7,.5f,5),stone);
            finale.nearCheckpoint=Mark("Maintenance checkpoint",new Vector3(-3,20.42f,-13.5f));
            finale.farCheckpoint=Mark("Final control checkpoint",new Vector3(0,20.42f,-2));
            finale.recoveryBounds=new Bounds(origin+new Vector3(-1,15.5f,-8.5f),new Vector3(14,5,18));

            var rootVisual=UnityEngine.Object.Instantiate(Node(w,4,MapKind.Root).motion.gameObject,root,true);
            rootVisual.name="BLOOM approach roots - reused model";
            rootVisual.transform.position=origin+new Vector3(-8.1f,15.3f,2.5f);
            foreach(var behavior in rootVisual.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(behavior);
            foreach(var collider in rootVisual.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            var bloomTarget=rootVisual.AddComponent<EFMapAbilityTarget>(); bloomTarget.receiver=finale.growth;
            var bloomHit=rootVisual.AddComponent<BoxCollider>(); bloomHit.isTrigger=true; bloomHit.size=new Vector3(2,2,2);

            finale.machine.motion.position=origin+new Vector3(-5.5f,19.3f,-14.3f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(finale.machine.motion);
            var control=UnityEngine.Object.Instantiate(finale.machine.motion.gameObject,root,true);
            control.name="Final control - last PULSE";
            control.transform.position=origin+new Vector3(2,19.3f,-1);
            foreach(var target in control.GetComponentsInChildren<EFMapAbilityTarget>(true)) UnityEngine.Object.DestroyImmediate(target);
            finale.finalControl=control.AddComponent<EFMapNode>();
            finale.finalControl.world=w; finale.finalControl.stage=5; finale.finalControl.kind=MapKind.Source;
            finale.finalControl.label="FINAL CONTROL - PULSE after crossing";
            finale.finalControl.motion=control.transform;
            finale.finalControl.indicators=control.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.Contains("Crystal")).ToArray();
            var aim=control.AddComponent<EFMapAbilityTarget>(); aim.receiver=finale.finalControl;
            finale.gate.supply=finale.finalControl;
            finale.gate.enabled=false;
            w.stages[5].nodes=new[]{finale.growth,finale.machine,finale.connection,finale.finalControl,finale.gate};
            w.stages[5].requirements=new[]{finale.growth,finale.machine,finale.connection};
            w.stages[5].hint="P6: BLOOM entry roots. Cross to maintenance and PULSE. FREEZE the last channel, walk across, then PULSE the FINAL CONTROL. Arrival never starts the ending.";
            w.mvpFinale=finale;
            var cameraGo=new GameObject("Final device reaction camera",typeof(Camera)); cameraGo.transform.SetParent(root,false);
            finale.reactionCamera=cameraGo.GetComponent<Camera>(); finale.reactionCamera.enabled=false;
            cameraGo.transform.position=origin+new Vector3(7,25,7);
            cameraGo.transform.LookAt(origin+new Vector3(0,19,-12));
            finale.reactionCamera.fieldOfView=58;
            var lightGo=new GameObject("Restoration warm light",typeof(Light)); lightGo.transform.SetParent(root,false);
            lightGo.transform.position=origin+new Vector3(0,23,-7);
            finale.restoredLight=lightGo.GetComponent<Light>(); finale.restoredLight.type=LightType.Point;
            finale.restoredLight.range=30; finale.restoredLight.color=new Color(1,.83f,.48f); finale.restoredLight.intensity=0;
            finale.feedback=root.gameObject.AddComponent<AudioSource>(); finale.feedback.playOnAwake=false; finale.feedback.spatialBlend=0;
            Sign("BLOOM roots -> maintenance PULSE -> FREEZE -> CROSS -> final PULSE",new Vector3(-6,18,2),.1f);
            Sign("FINAL CONTROL : PULSE (3)",new Vector3(2,22,-1),.12f);
        }
        static void Stairs(string name,Vector3 a,Vector3 b,int count)
        {
            var flat=Vector3.ProjectOnPlane(b-a,Vector3.up);
            for(int i=0;i<count;i++)
            {
                var p=Vector3.Lerp(a,b,(i+.5f)/count); p.y=Mathf.Lerp(a.y,b.y,(i+1f)/count)-.25f;
                var step=Box(name+" "+i,p,new Vector3(3,.5f,flat.magnitude/count+.08f),stone);
                step.transform.rotation=Quaternion.LookRotation(flat);
            }
        }
        static BoxCollider Box(string name,Vector3 p,Vector3 size,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(root,true);
            go.transform.position=origin+p; go.transform.localScale=size; go.GetComponent<Renderer>().sharedMaterial=mat;
            return go.GetComponent<BoxCollider>();
        }
        static Transform Mark(string name,Vector3 p)
        { var t=new GameObject(name).transform; t.SetParent(root,true); t.position=origin+p; return t; }
        static void Sign(string text,Vector3 p,float scale)
        {
            var t=Mark(text,p); var mesh=t.gameObject.AddComponent<TextMesh>(); mesh.text=text;
            mesh.fontSize=36; mesh.characterSize=scale; mesh.anchor=TextAnchor.MiddleCenter; mesh.color=Color.white;
            t.rotation=Quaternion.Euler(0,180,0);
        }
        public static void Validate(EFMapWorld w)
        {
            if(w.gameObject.scene.path!=ScenePath) throw new InvalidOperationException("Expected final editable MVP scene.");
            EverflowMVP.Editor.EFReleaseSetup.ValidateMapReferences(new[]{w},new[]{w.player});
            var f=w.mvpFinale;
            if(f==null || f.world!=w)
                throw new InvalidOperationException("MVP finale/world reference missing. The final PULSE controller is required.");
            if(new[]{f.growth,f.machine,f.connection,f.gate,f.finalControl}.Any(n=>n==null || n.stage!=5 || n.world!=w))
                throw new InvalidOperationException("P6 references incomplete.");
            if(f.finalControl==f.machine || f.gate.supply!=f.finalControl ||
               w.stages[5].requirements.Contains(f.gate) || w.stages[5].requirements.Contains(f.finalControl))
                throw new InvalidOperationException("Final PULSE/gate references or circular preparation requirements.");
            if(f.bloomRoute==null || f.frozenRoute==null || f.nearDeck==null || f.farDeck==null ||
               f.nearCheckpoint==null || f.farCheckpoint==null || f.grounding==null || f.grounding.world!=w ||
               f.reactionCamera==null || f.bloomRoute.GetComponent<Renderer>()==null || f.frozenRoute.GetComponent<Renderer>()==null)
                throw new InvalidOperationException("Missing physical route/camera references.");
            var objects=w.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            if(objects.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))!=0)
                throw new InvalidOperationException("Missing script in MVP.");
        }
        [MenuItem("Tools/EVERFLOW/MVP Integration/Publish Verified Title Route")]
        public static void Publish()
        {
            Guard();
            var result=JsonUtility.FromJson<MvpVerificationStamp>(File.ReadAllText(Evidence+"/verified.json"));
            if(result==null || !result.pass || result.sceneHash!=Hash(ScenePath) || result.sourceHash!=RuntimeFingerprint())
                throw new InvalidOperationException("Current saved MVP and runtime source must pass checks before publication.");
            var enabled=new[]{EverflowMVP.Editor.EFReleaseSetup.TitleScene,ScenePath};
            var scenes=enabled.Select(p=>new EditorBuildSettingsScene(p,true)).Concat(EditorBuildSettings.globalScenes.Where(s=>!enabled.Contains(s.path)).Select(s=>new EditorBuildSettingsScene(s.path,false))).ToArray();
            var settings="ProjectSettings/EditorBuildSettings.asset";
            string backup=Evidence+"/build-settings-before-publish-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")+".txt";
            File.Copy(settings,backup,false);
            EditorBuildSettings.globalScenes=scenes;
            var profile=BuildProfile.GetActiveBuildProfile();
            if(profile!=null && profile.overrideGlobalScenes) { profile.scenes=scenes; EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile); }
            AssetDatabase.SaveAssets();
            if(!EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).SequenceEqual(enabled))
                throw new InvalidOperationException("Active build profile did not retain title/MVP scene registration.");
            File.WriteAllText(Evidence+"/published.txt",DateTime.UtcNow.ToString("O")+"\nTitle and MVP registered; preserve original map disabled.\n");
        }
        [Serializable] public sealed class MvpVerificationStamp { public bool pass; public string sceneHash,sourceHash; }
        public static void Build()
        {
            Guard();
            DateTime before=DateTime.UtcNow;
            EverflowMVP.Editor.EFReleaseBuild.BuildWindows();
            var result=Directory.GetFiles("Build","build-result.txt",SearchOption.AllDirectories)
                .Where(p=>File.GetLastWriteTimeUtc(p)>=before).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if(result==null || !File.ReadAllText(result).StartsWith("Succeeded",StringComparison.Ordinal))
                throw new InvalidOperationException("No successful new build result; inspect build evidence.");
            File.WriteAllText(Evidence+"/latest-build.txt",result+"\n"+File.ReadAllText(result));
        }
    }
}
