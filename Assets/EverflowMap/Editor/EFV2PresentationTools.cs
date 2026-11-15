using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowMap.Editor
{
    public static class EFV2PresentationTools
    {
        public const string Reports="docs/ability-intro-validation";
        [MenuItem("Tools/EVERFLOW/V2 Puzzles/Configure Ability Images And Opening")]
        public static void Configure()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play first.");
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene edits preserved.");
            // Read the actual reference scene and reuse its imported sprites. Never run
            // the old setup/upgrade: it also edits the title, original map and importers.
            EditorSceneManager.OpenScene("Assets/Scenes/Everflow_Map.unity");
            var source=UnityEngine.Object.FindFirstObjectByType<EFMapWorld>();
            var hud=source.GetComponentInChildren<EFMapAbilityHUD>(true);
            var icons=hud.abilities.Select(v=>v.icon.sprite).ToArray();
            if(icons.Length!=3||icons.Any(s=>s==null)||source.openingCamera==null)throw new Exception("Reference presentation missing.");
            float fov=source.openingCamera.fieldOfView;
            var uv=icons.Select(VisibleUV).ToArray();
            Directory.CreateDirectory(Reports);
            File.WriteAllText(Reports+"/reference.txt","Read-only source: Assets/Scenes/Everflow_Map.unity\n"+
                "Overview: "+source.openingCamera.transform.position+", FOV="+fov+", original hold=3 seconds.\n"+
                string.Join("\n",icons.Select(s=>AssetDatabase.GetAssetPath(s)))+"\nThird PULSE image reused as FLOW art; v2 rules/text retained.\n");
            var scene=EditorSceneManager.OpenScene(EFV2PuzzleBuild.Scene);
            var w=UnityEngine.Object.FindFirstObjectByType<EFV2PuzzleWorld>();
            var p=w.GetComponent<EFV2ScenePresentation>();if(p==null)p=w.gameObject.AddComponent<EFV2ScenePresentation>();
            p.world=w;p.abilityIcons=icons;p.iconUV=uv;p.overviewFov=fov;w.presentation=p;
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new Exception("Puzzle presentation scene save failed.");
            Debug.Log("V2 ability art and opening saved. Reference scene, sprite importers and gameplay camera unchanged.");
        }
        static Rect VisibleUV(Sprite sprite)
        {
            var t=new Texture2D(2,2);t.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
            var pixels=t.GetPixels32();int x0=t.width,y0=t.height,x1=0,y1=0;
            for(int y=0;y<t.height;y++)for(int x=0;x<t.width;x++)
                if(pixels[y*t.width+x].a>=80){x0=Mathf.Min(x0,x);y0=Mathf.Min(y0,y);x1=Mathf.Max(x1,x);y1=Mathf.Max(y1,y);}
            // A square crop preserves image proportions while removing excessive transparency.
            float size=Mathf.Max(x1-x0+1,y1-y0+1),cx=(x0+x1+1)*.5f,cy=(y0+y1+1)*.5f;
            var uv=new Rect((cx-size*.5f)/t.width,(cy-size*.5f)/t.height,size/t.width,size/t.height);
            UnityEngine.Object.DestroyImmediate(t);return uv;
        }
    }
}
