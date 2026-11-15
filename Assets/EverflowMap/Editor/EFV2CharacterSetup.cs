using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowMap.Editor
{
    public static class EFV2CharacterSetup
    {
        public const string Folder="Assets/EverflowMap/Character";
        const string Model="Assets/character_1/model/character_fixed.fbx";
        const string Animations="Assets/character_1/animation/";
        static ModelImporter ConfigureModel()
        {
            var model=(ModelImporter)AssetImporter.GetAtPath(Model);
            if(model==null)throw new Exception("Character model not imported: "+Model);
            model.animationType=ModelImporterAnimationType.Human;
            model.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            model.importCameras=model.importLights=false;
            // Reuse the validated animation bone mapping (including LeftHand), with this FBX's own skeleton.
            var reference=(ModelImporter)AssetImporter.GetAtPath(Animations+"character_walk.fbx");
            var mapping=reference.humanDescription.human;
            var names=AssetDatabase.LoadAssetAtPath<GameObject>(Model).GetComponentsInChildren<Transform>(true).Select(t=>t.name).ToHashSet();
            bool compatible=mapping.Length>0&&mapping.All(h=>names.Contains(h.boneName));
            if(compatible)
            {var human=model.humanDescription;human.human=mapping;model.humanDescription=human;}
            model.SaveAndReimport();
            // Unity's first Generic -> Human import can replace the explicit map with auto-mapping.
            // Apply the validated map after that conversion if it was replaced (notably LeftHand).
            if(compatible&&!mapping.All(expected=>model.humanDescription.human.Any(h=>h.humanName==expected.humanName&&h.boneName==expected.boneName)))
            {var human=model.humanDescription;human.human=mapping;model.humanDescription=human;model.SaveAndReimport();}
            if(!AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Avatar>().Any(a=>a.isHuman&&a.isValid))
                throw new Exception("New character does not have a valid Humanoid Avatar.");
            return model;
        }

        [MenuItem("Tools/EVERFLOW/V2 Puzzles/Replace Player Model With Character Fixed")]
        public static void ReplaceModel()
        {
            if(Application.isPlaying)throw new Exception("Stop Play before replacing the model.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=EFV2PuzzleBuild.Scene)throw new Exception("Open the V2 puzzle scene first.");
            var world=UnityEngine.Object.FindFirstObjectByType<EFV2PuzzleWorld>();var player=world.player;
            var bridge=player.GetComponent<EFV2CharacterAnimator>();
            var previous=player.transform.Find("Character Visual");
            if(previous==null||bridge==null||bridge.animator==null)throw new Exception("Existing animated player is required.");
            var controller=bridge.animator.runtimeAnimatorController;
            var materials=previous.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials;
            ConfigureModel();
            var visual=new GameObject("Character Visual - replacement").transform;visual.SetParent(player.transform,false);
            try
            {
                var body=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model),visual);
                body.name="Character 1 - animated body";
                body.transform.localPosition=Vector3.zero;body.transform.localRotation=Quaternion.identity;body.transform.localScale=Vector3.one;
                var renderers=body.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if(renderers.Length==0)throw new Exception("Fixed model has no skinned mesh.");
                var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                float scale=2/Mathf.Max(.1f,bounds.size.y);
                visual.localScale=Vector3.one*scale;
                visual.localPosition=new Vector3(0,-1-(bounds.min.y-player.transform.position.y)*scale,0);
                foreach(var r in renderers)
                {r.sharedMaterials=Enumerable.Range(0,r.sharedMaterials.Length).Select(i=>materials[Mathf.Min(i,materials.Length-1)]).ToArray();r.updateWhenOffscreen=true;}
                foreach(var t in body.GetComponentsInChildren<Transform>(true))t.gameObject.layer=player.gameObject.layer;
                var animator=body.GetComponent<Animator>()??body.AddComponent<Animator>();
                animator.avatar=AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Avatar>().First(a=>a.isHuman&&a.isValid);
                animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
                animator.updateMode=AnimatorUpdateMode.Normal;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                bridge.animator=animator;EditorUtility.SetDirty(bridge);
                UnityEngine.Object.DestroyImmediate(previous.gameObject);visual.name="Character Visual";
                EditorSceneManager.MarkSceneDirty(player.gameObject.scene);EditorSceneManager.SaveScene(player.gameObject.scene);
            }
            catch {UnityEngine.Object.DestroyImmediate(visual.gameObject);throw;}
            Debug.Log("Player model replaced with character_fixed; existing controller, clips, material and motor retained.");
        }
        static ModelImporterClipAnimation Clip(ModelImporter importer,string name,float first,float last,bool loop)
        {
            var c=new ModelImporterClipAnimation{takeName=importer.defaultClipAnimations.First().takeName,name=name,firstFrame=first,lastFrame=last,
                loopTime=loop,loopPose=loop,lockRootRotation=true,keepOriginalOrientation=true,
                lockRootPositionXZ=true,keepOriginalPositionXZ=true,lockRootHeightY=false,keepOriginalPositionY=false,heightFromFeet=true};
            return c;
        }
        public static void ConfigureImports()
        {
            var model=ConfigureModel();
            var mapping=model.humanDescription.human;
            foreach(string stem in new[]{"walk","run","jump","landing"})
            {
                var i=(ModelImporter)AssetImporter.GetAtPath(Animations+"character_"+stem+".fbx");
                i.animationType=ModelImporterAnimationType.Human;i.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
                var human=i.humanDescription;human.human=mapping;i.humanDescription=human;
                i.importCameras=i.importLights=false;
                // Source landing reaches the ground around frame 11; omit its airborne prefix.
                // Frames 45..63 provide a stable temporary idle from the supplied recovery pose.
                i.clipAnimations=stem=="landing"?new[]{Clip(i,"Land",11,36,false),Clip(i,"Idle",45,63,true)}:
                    stem=="jump"?new[]{Clip(i,"Jump",0,12,false),Clip(i,"Fall",14,16,true)}:
                    new[]{Clip(i,stem=="run"?"Run":"Walk",0,stem=="run"?21:29,true)};
                i.SaveAndReimport();
                var avatar=AssetDatabase.LoadAllAssetsAtPath(i.assetPath).OfType<Avatar>().FirstOrDefault();
                if(avatar==null||!avatar.isHuman||!avatar.isValid)throw new Exception("Invalid character avatar: "+i.assetPath);
            }
        }
        static AnimationClip Motion(string name)
            =>new[]{"walk","run","jump","landing"}.SelectMany(s=>AssetDatabase.LoadAllAssetsAtPath(Animations+"character_"+s+".fbx")).OfType<AnimationClip>().First(c=>c.name==name);
        [MenuItem("Tools/EVERFLOW/V2 Puzzles/Install Character 1 Player")]
        public static void Install()
        {
            if(Application.isPlaying)throw new Exception("Stop Play before character installation.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=EFV2PuzzleBuild.Scene)throw new Exception("Open the V2 puzzle scene first.");
            ConfigureImports();Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            string controllerPath=Folder+"/Character1.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            foreach(var parameter in controller.parameters)controller.RemoveParameter(parameter);
            var machine=controller.layers[0].stateMachine;
            foreach(var state in machine.states)machine.RemoveState(state.state);
            controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
            var locomotion=machine.AddState("Locomotion");machine.defaultState=locomotion;
            var tree=new BlendTree{name="Idle Walk Run - self motion",blendType=BlendTreeType.Simple1D,blendParameter="Speed",useAutomaticThresholds=false};
            tree.AddChild(Motion("Idle"),0);tree.AddChild(Motion("Walk"),2.2f);tree.AddChild(Motion("Run"),6);
            AssetDatabase.AddObjectToAsset(tree,controller);locomotion.motion=tree;locomotion.writeDefaultValues=false;
            foreach(string name in new[]{"Jump","Fall","Land"})
            {var state=machine.AddState(name);state.motion=Motion(name);state.speed=name=="Fall"?.25f:1;state.writeDefaultValues=false;}
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Character1.mat");
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Folder+"/Character1.mat");}
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.12f);
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/character_1/model/character_noTex.fbm/tripo_rgb_d2b5edfd-09b8-4ac9-b06f-35e1366cf3cb.jpg"));
            var world=UnityEngine.Object.FindFirstObjectByType<EFV2PuzzleWorld>();var player=world.player;
            var previous=player.transform.Find("Character Visual");if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var visual=new GameObject("Character Visual").transform;visual.SetParent(player.transform,false);
            var body=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            body.name="Character 1 - animated body";body.transform.SetParent(visual,false);
            body.transform.localPosition=Vector3.zero;body.transform.localRotation=Quaternion.identity;body.transform.localScale=Vector3.one;
            var renderers=body.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            float scale=2/Mathf.Max(.1f,bounds.size.y);visual.localScale=Vector3.one*scale;
            float footLocalY=bounds.min.y-player.transform.position.y;
            visual.localPosition=new Vector3(0,-1-footLocalY*scale,0);
            foreach(var r in renderers){r.sharedMaterials=Enumerable.Repeat(material,r.sharedMaterials.Length).ToArray();r.updateWhenOffscreen=true;}
            foreach(var t in body.GetComponentsInChildren<Transform>(true))t.gameObject.layer=player.gameObject.layer;
            var animator=body.GetComponent<Animator>()??body.AddComponent<Animator>();
            animator.avatar=AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Avatar>().First(a=>a.isHuman&&a.isValid);
            animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
            animator.updateMode=AnimatorUpdateMode.Normal;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var bridge=player.GetComponent<EFV2CharacterAnimator>()??player.gameObject.AddComponent<EFV2CharacterAnimator>();
            bridge.player=player;bridge.world=world;bridge.animator=animator;
            player.GetComponent<MeshRenderer>().enabled=false;
            EditorUtility.SetDirty(controller);EditorUtility.SetDirty(material);EditorUtility.SetDirty(player);EditorUtility.SetDirty(bridge);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(player.gameObject.scene);EditorSceneManager.SaveScene(player.gameObject.scene);
            Debug.Log("Character 1 installed with existing 2m collider, code-driven movement and Cinemachine target unchanged.");
        }
    }
}
