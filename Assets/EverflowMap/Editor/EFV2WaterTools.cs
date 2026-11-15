using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EverflowMap.Editor
{
    public static class EFV2WaterTools
    {
        public const string Folder="Assets/EverflowWater", Reports="docs/water-validation";
        public const string TestScene="Assets/Scenes/Everflow_Water_Test.unity";
        public static void Audit()
        {
            EditorSceneManager.OpenScene(EFV2PuzzleBuild.Scene);
            Directory.CreateDirectory(Reports);
            var s=new StringBuilder();
            var rp=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            s.AppendLine($"Unity={Application.unityVersion} URP={UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UniversalRenderPipelineAsset).Assembly)?.version} quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} colorspace={QualitySettings.activeColorSpace} device={SystemInfo.graphicsDeviceType}");
            s.AppendLine($"pipeline={AssetDatabase.GetAssetPath(rp)} depth={rp.supportsCameraDepthTexture} opaque={rp.supportsCameraOpaqueTexture} renderer={rp.scriptableRenderer.GetType().Name}");
            foreach(var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {var d=c.GetUniversalAdditionalCameraData();s.AppendLine($"CAMERA {c.name} depth={d.requiresDepthTexture} opaque={d.requiresColorTexture} renderer={d.scriptableRenderer.GetType().Name} near={c.nearClipPlane} far={c.farClipPlane}");}
            var w=UnityEngine.Object.FindFirstObjectByType<EFV2PuzzleWorld>();
            foreach(var m in w.mapRoot.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.transform.parent!=null && (m.name.StartsWith("W_")||m.transform.parent.name=="GRP_Water")))
            {
                var r=m.GetComponent<Renderer>();var mesh=m.sharedMesh;var colors=mesh.colors;
                string cr=colors.Length==0?"none":$"G={colors.Min(c=>c.g):F3}..{colors.Max(c=>c.g):F3} B={colors.Min(c=>c.b):F3}..{colors.Max(c=>c.b):F3} R={colors.Min(c=>c.r):F3}..{colors.Max(c=>c.r):F3}";
                s.AppendLine($"{m.name}\tcenter={r.bounds.center.ToString("F3")}\tsize={r.bounds.size.ToString("F3")}\tscale={m.transform.lossyScale.ToString("F3")}\tverts={mesh.vertexCount}\tcolors={cr}\tmat={string.Join(",",r.sharedMaterials.Select(x=>x!=null?x.name:"null"))}\tcolliders={m.GetComponents<Collider>().Length}");
            }
            File.WriteAllText(Reports+"/unity-water-audit.txt",s.ToString());
        }
        const string Vendor="Assets/EmacEArt/Roadside Tales Backroad Bridges";
        public const string MaterialPath=Folder+"/Materials/EF_CoolWater.mat";
        public static Material AssetsReady()
        {
            foreach(string dir in new[]{Folder,Folder+"/Materials",Folder+"/Textures",Folder+"/Meshes"})Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
            string normal=Folder+"/Textures/EF_WaterNormal.png",caustic=Folder+"/Textures/EF_WaterCaustics.bmp";
            if(!File.Exists(normal)&&!AssetDatabase.CopyAsset(Vendor+"/Textures/WaterNMP.png",normal))throw new Exception("Normal copy failed");
            if(!File.Exists(caustic)&&!AssetDatabase.CopyAsset(Directory.GetFiles(Vendor+"/Textures","CausticsRender_004*.bmp").Single().Replace('\\','/'),caustic))throw new Exception("Caustic copy failed");
            var ti=(TextureImporter)AssetImporter.GetAtPath(normal);
            if(ti.textureType!=TextureImporterType.NormalMap || ti.sRGBTexture)
            {ti.textureType=TextureImporterType.NormalMap;ti.sRGBTexture=false;ti.wrapMode=TextureWrapMode.Repeat;ti.mipmapEnabled=true;ti.SaveAndReimport();}
            bool created=!File.Exists(MaterialPath);
            if(created&&!AssetDatabase.CopyAsset(Vendor+"/Materials/Water_Experimental.mat",MaterialPath))throw new Exception("Material copy failed");
            var mat=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var ownShader=AssetDatabase.LoadAssetAtPath<Shader>(Folder+"/Shaders/EF_CoolWater.shader");
            if(ownShader==null)throw new Exception("Generate the EVERFLOW shader copy first.");
            // Re-running the builder must retain the user's existing material tuning.
            if(created||mat.shader!=ownShader)
            {
                mat.shader=AssetDatabase.LoadAssetAtPath<Shader>(Folder+"/Shaders/EF_CoolWater.shader");
                if(mat.shader==null)throw new Exception("Generate the EVERFLOW shader copy first.");
                mat.shaderKeywords=new[]{"_FOAMSTYLE_CLOUDY"};
                mat.SetTexture("_NormalMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
                mat.SetTexture("_FoamTex",AssetDatabase.LoadAssetAtPath<Texture2D>(caustic));
                mat.SetColor("_ShallowColor",new Color(.12f,.42f,.43f,.72f));mat.SetColor("_DeepColor",new Color(.035f,.17f,.22f,.94f));
                mat.SetColor("_ShoreGlowColor",new Color(.4f,.69f,.64f,1));mat.SetColor("_FoamColor",new Color(.72f,.86f,.79f,.65f));
                mat.SetColor("_CausticsColor",new Color(.55f,.8f,.65f,1));mat.SetColor("_SpecularColor",new Color(.8f,.86f,.75f,1));
                foreach(var kv in new System.Collections.Generic.Dictionary<string,float>{{"_WaterDepth",2.5f},{"_ColorBands",5},{"_ShoreGlowWidth",.12f},{"_NormalTiling",.5f},{"_NormalStrength",.45f},{"_WaveSpeed",.6f},{"_WaveHeight",.018f},{"_FoamStyle",1},{"_FoamTiling",.8f},{"_FoamDepth",.16f},{"_FoamCutoff",.64f},{"_FoamAmount",.24f},{"_CausticsStrength",.12f},{"_CausticsScale",.65f},{"_RefractionStr",.005f},{"_SpecularPower",65},{"_SpecularCutoff",.3f},{"_SpecularScale",.12f},{"_FresnelPower",3}})mat.SetFloat(kv.Key,kv.Value);
            }
            EditorUtility.SetDirty(mat);AssetDatabase.SaveAssets();
            // Remove stale vendor properties, including the unused FoamFlowX/Z controls.
            var serialized=new SerializedObject(mat);
            foreach(string group in new[]{"m_SavedProperties.m_TexEnvs","m_SavedProperties.m_Floats","m_SavedProperties.m_Colors"})
            {
                var props=serialized.FindProperty(group);
                for(int i=props.arraySize-1;i>=0;i--)if(!mat.HasProperty(props.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue))props.DeleteArrayElementAtIndex(i);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();
            File.WriteAllText(Reports+"/shader-import.txt",string.Join("\n",ShaderUtil.GetShaderMessages(mat.shader).Select(m=>$"{m.severity}: {m.message} line {m.line}"))+"\nshaderSupported="+mat.shader.isSupported+" errors="+ShaderUtil.ShaderHasError(mat.shader));
            if(ShaderUtil.ShaderHasError(mat.shader))throw new Exception("Water shader import errors: see shader-import.txt");
            return mat;
        }
        static EFV2WaterSegment Segment(MeshFilter source)
        {
            string path=Folder+"/Meshes/"+source.name+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null)
            {
                // FBX has no vertex colors. Explicit white stops G clipping and R foam flooding.
                // New readable asset leaves the source FBX importer and mesh entirely untouched.
                var original=source.sharedMesh;
                mesh=new Mesh{name=source.name+" - EVERFLOW color-safe copy",indexFormat=original.indexFormat};
                mesh.vertices=original.vertices;mesh.normals=original.normals;mesh.tangents=original.tangents;mesh.uv=original.uv;
                mesh.colors=Enumerable.Repeat(Color.white,mesh.vertexCount).ToArray();
                mesh.subMeshCount=original.subMeshCount;
                for(int i=0;i<mesh.subMeshCount;i++)mesh.SetTriangles(original.GetTriangles(i),i);
                mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);
            }
            var r=source.GetComponent<MeshRenderer>();
            var s=new EFV2WaterSegment{label=source.name,renderer=r,filter=source,originalMesh=source.sharedMesh,originalMaterials=r.sharedMaterials,preparedMesh=mesh};
            var b=r.bounds;string n=source.name;
            s.kind=n.EndsWith("_Foam")?EFV2WaterKind.Foam:b.size.y>Mathf.Max(b.size.x,b.size.z)*.8f?EFV2WaterKind.Waterfall:EFV2WaterKind.Surface;
            s.direction=s.kind==EFV2WaterKind.Waterfall?Vector3.down:Vector3.forward;
            s.speed=s.kind==EFV2WaterKind.Waterfall?1.2f:.55f;
            s.foam=s.kind==EFV2WaterKind.Foam?.65f:s.kind==EFV2WaterKind.Waterfall?.38f:.24f;
            if(s.kind!=EFV2WaterKind.Surface)s.waveHeight=0;
            if(n=="W_A_Pond"||n=="W_H_Pond"){s.speed=0;s.direction=Vector3.zero;s.foam=.07f;}
            if(n=="W_G_ToWheel"||n=="W_Leak_S2"||n=="W_D2_Dock"||n=="W_D2_Low"||n=="W_D5_SpillPool")s.direction=Vector3.right;
            if(n=="W_Bypass_1"||n=="W_D2_Wheel"||n=="W_D2_Exit"||n=="W_Exit_Tunnel")s.direction=Vector3.left;
            if(n=="W_D2_Basin"){s.ice=EFV2WaterIce.D2Basin;s.foam=.12f;s.speed=.38f;}
            if(n=="W_D5_Fixed")s.ice=EFV2WaterIce.D5Internal;
            if(n.Contains("Fall_Foam")||n=="W_R_WheelPool")s.direction=Vector3.forward;
            if(n=="W_D5_Fall"||n=="W_Bypass_2")s.arrivalDelay=.08f;
            if(n=="W_D5_ToD2"||n=="W_Bypass_Fall")s.arrivalDelay=.16f;
            if(n=="W_Gorge")s.arrivalDelay=.08f;
            if(n=="W_R_WheelFeed")s.arrivalDelay=.16f;
            return s;
        }
        [MenuItem("Tools/EVERFLOW/Water/1 Build Isolated Test Scene")]
        public static void BuildTest()
        {
            Directory.CreateDirectory(Reports);
            var mat=AssetsReady();
            EditorSceneManager.OpenScene(EFV2PuzzleBuild.Scene);
            var w=UnityEngine.Object.FindFirstObjectByType<EFV2PuzzleWorld>();
            var source=w.mapRoot.GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="W_D2_Exit");
            var src=Segment(source);var bounds=src.renderer.bounds;
            var go=new GameObject("Short canal - copied W_D2_Exit geometry");
            go.transform.SetPositionAndRotation(source.transform.position-bounds.center,source.transform.rotation);go.transform.localScale=source.transform.lossyScale;
            var mf=go.AddComponent<MeshFilter>();mf.sharedMesh=src.preparedMesh;
            var mr=go.AddComponent<MeshRenderer>();mr.sharedMaterial=mat;
            var test=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,test);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(test);
            var ctrl=new GameObject("Water Test Controls").AddComponent<EFV2WaterIntegration>();ctrl.waterMaterial=mat;
            src.renderer=mr;src.filter=mf;src.label="TEST - world negative X downstream";src.direction=Vector3.left;src.arrivalDelay=0;
            ctrl.segments=new[]{src};
            ctrl.gameObject.AddComponent<EFV2WaterPreview>().water=ctrl;
            var groundMat=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.24f,.29f,.24f)};
            string groundPath=Folder+"/Materials/EF_TestStone.mat";
            if(!File.Exists(groundPath))AssetDatabase.CreateAsset(groundMat,groundPath);else {UnityEngine.Object.DestroyImmediate(groundMat);groundMat=AssetDatabase.LoadAssetAtPath<Material>(groundPath);}
            foreach(var spec in new[]{new Vector4(0,-.8f,0,0),new Vector4(0,-.08f,1.8f,1),new Vector4(0,-.08f,-1.8f,1)})
            {var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name="Test canal stone";box.transform.position=new Vector3(spec.x,spec.y,spec.z);box.transform.localScale=spec.w==0?new Vector3(15,.3f,5):new Vector3(13.6f,.8f,.6f);box.GetComponent<Renderer>().sharedMaterial=groundMat;}
            var camera=new GameObject("Water Test Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(7,8,-10);camera.transform.LookAt(Vector3.zero);camera.nearClipPlane=.1f;camera.farClipPlane=120;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.11f,.12f);camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().requiresColorTexture=true;
            var sun=new GameObject("Water Test Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.2f;sun.transform.rotation=Quaternion.Euler(45,-30,0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.45f,.5f,.47f);
            EditorSceneManager.SaveScene(test,TestScene);
            EditorSceneManager.OpenScene(TestScene);
            File.WriteAllText(Reports+"/test-scene-built.txt","Independent short canal copied from W_D2_Exit; original local mesh shape/scale preserved, translated to origin. New mesh asset; no FBX edits.\nGUID="+AssetDatabase.AssetPathToGUID(TestScene));
        }
        [MenuItem("Tools/EVERFLOW/Water/2 Integrate Into Puzzle Scene")]
        public static void Integrate()
        {
            if(!File.Exists(Reports+"/runtime-isolated.txt")||!File.ReadAllText(Reports+"/runtime-isolated.txt").Contains("0 failures"))throw new Exception("Pass isolated Play checks before integration.");
            var mat=AssetsReady();EditorSceneManager.OpenScene(EFV2PuzzleBuild.Scene);
            var w=UnityEngine.Object.FindFirstObjectByType<EFV2PuzzleWorld>();
            var existing=UnityEngine.Object.FindFirstObjectByType<EFV2WaterIntegration>();
            var ctrl=existing!=null?existing:new GameObject("EVERFLOW Water Presentation").AddComponent<EFV2WaterIntegration>();ctrl.world=w;ctrl.waterMaterial=mat;
            var additions=w.mapRoot.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.transform.parent!=null&&m.transform.parent.name.StartsWith("GRP_Water")&&!ctrl.segments.Any(s=>s.filter==m)).Select(Segment);
            ctrl.segments=ctrl.segments.Concat(additions).ToArray();
            foreach(var s in ctrl.segments){s.filter.sharedMesh=s.preparedMesh;s.renderer.sharedMaterial=mat;}
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            File.WriteAllText(Reports+"/integrated-bindings.txt",string.Join("\n",ctrl.segments.Select(s=>$"{s.label}\t{s.kind}\tworldDirection={s.direction}\tspeed={s.speed} m/s\twave={s.waveHeight}m\tice={s.ice}")));
        }
        [MenuItem("Tools/EVERFLOW/Water/Restore Original Water In Puzzle Scene")]
        public static void Restore()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play first.");
            var ctrl=UnityEngine.Object.FindFirstObjectByType<EFV2WaterIntegration>();if(ctrl==null)return;
            Undo.RegisterFullObjectHierarchyUndo(ctrl.gameObject,"Restore original water");
            foreach(var s in ctrl.segments){Undo.RecordObjects(new UnityEngine.Object[]{s.filter,s.renderer},"Restore water renderer");s.filter.sharedMesh=s.originalMesh;s.renderer.sharedMaterials=s.originalMaterials;}
            Undo.DestroyObjectImmediate(ctrl.gameObject);EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        public static void Test(bool integrated){EFV2WaterPlayChecks.Run(integrated);}
        [MenuItem("Tools/EVERFLOW/Water/Validate Saved Integration")]
        public static void Verify()
        {
            EditorSceneManager.OpenScene(EFV2PuzzleBuild.Scene);
            var water=UnityEngine.Object.FindFirstObjectByType<EFV2WaterIntegration>();
            var text=new StringBuilder(DateTime.UtcNow.ToString("O")+"\nUnity asset/reference verification; not a Play test.\n");
            void Require(bool condition,string label){if(!condition)throw new Exception(label);text.AppendLine("PASS "+label);}
            Require(water!=null&&water.world!=null&&water.segments.Length==39,"39 scene-local water bindings and world reference");
            foreach(var s in water.segments)
            {
                Require(s.renderer!=null&&s.filter!=null&&s.originalMesh!=null&&s.preparedMesh!=null&&s.originalMaterials.All(m=>m!=null),s.label+" references and rollback data");
                Require(s.originalMesh.vertices.SequenceEqual(s.preparedMesh.vertices)&&s.originalMesh.triangles.SequenceEqual(s.preparedMesh.triangles)&&s.originalMesh.normals.SequenceEqual(s.preparedMesh.normals)&&s.originalMesh.uv.SequenceEqual(s.preparedMesh.uv)&&Vector3.Distance(s.originalMesh.bounds.center,s.preparedMesh.bounds.center)<.0001f&&Vector3.Distance(s.originalMesh.bounds.size,s.preparedMesh.bounds.size)<.0001f,s.label+" original geometry/normals/UV/bounds retained (bounds tolerance 0.1mm)");
                Require(s.preparedMesh.colors.All(c=>c==Color.white)&&s.preparedMesh.colors.Length==s.preparedMesh.vertexCount,s.label+" explicit white vertex colors");
                Require(s.renderer.GetComponents<Collider>().Length==0,s.label+" no water collider");
            }
            Require(!ShaderUtil.ShaderHasError(water.waterMaterial.shader)&&water.waterMaterial.shader.isSupported,"Shader imported and supported");
            var normal=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(water.waterMaterial.GetTexture("_NormalMap")));
            Require(normal.textureType==TextureImporterType.NormalMap&&!normal.sRGBTexture&&normal.wrapMode==TextureWrapMode.Repeat,"Dedicated normal importer, linear, repeat");
            var rp=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            text.AppendLine($"Active pipeline={AssetDatabase.GetAssetPath(rp)}, depth={rp.supportsCameraDepthTexture}, opaque={rp.supportsCameraOpaqueTexture}, colorSpace={QualitySettings.activeColorSpace}");
            text.AppendLine("Shader messages: "+string.Join(";",ShaderUtil.GetShaderMessages(water.waterMaterial.shader).Select(x=>x.message)));
            Require(AssetDatabase.AssetPathToGUID(TestScene)!=AssetDatabase.AssetPathToGUID(EFV2PuzzleBuild.Source),"Test scene has independent GUID");
            var material=water.waterMaterial;
            EditorSceneManager.OpenScene(EFV2PuzzleBuild.Source);
            Require(UnityEngine.Object.FindFirstObjectByType<EFV2WaterIntegration>()==null,"Original map has no water integration component");
            Require(!UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(r=>r.sharedMaterials.Contains(material)),"Original map retains original water materials");
            EditorSceneManager.OpenScene(EFV2PuzzleBuild.Scene);EFV2PuzzleBuild.Validate();
            // Background pumping in Editor Play changes this serialized Player setting.
            // Revert only our exact one-field change; preserve any unrelated user edits.
            string settings="ProjectSettings/ProjectSettings.asset";
            string candidate=File.ReadAllText(settings).Replace("  runInBackground: 1","  runInBackground: 0");
            using(var sha=System.Security.Cryptography.SHA256.Create())
            {
                string hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(candidate))).Replace("-","").ToLowerInvariant();
                string baseline=File.ReadAllText(Reports+"/baseline-sha256.json");
                if(baseline.Contains("\"ProjectSettings/ProjectSettings.asset\": \""+hash+"\""))
                {
                    PlayerSettings.runInBackground=false;AssetDatabase.SaveAssets();
                    // PlayerSettings can defer its disk flush. Synchronize the exact
                    // baseline-matching one-field result as well as the native value.
                    File.WriteAllText(settings,candidate,new UTF8Encoding(false));
                    text.AppendLine("Restored task-start runInBackground=0 in memory and on disk after testing.");
                }
            }
            File.WriteAllText(Reports+"/unity-asset-verification.txt",text.ToString());
        }
    }
}
