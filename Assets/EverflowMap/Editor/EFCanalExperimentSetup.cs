using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EverflowMap.Editor
{
    public static class EFCanalExperimentSetup
    {
        public const string ScenePath = "Assets/Scenes/Everflow_Map_P3_DesignTest_20261016.unity";
        const string ExperimentName = "EF_P3_Canal_Experiment_20261016";
        static Transform root;
        static Vector3 origin;
        static Material stone, flow;

        [MenuItem("Tools/EVERFLOW/MVP Experiment/Create P3 Copy")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before creating the copy.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save your current scene edits first. No scene was changed.");
            if (File.Exists(ScenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("Experiment already exists; open it for review. This command never overwrites an existing copy: " + ScenePath);
            string sourceHash = Sha256(EFMapAudit.ScenePath);
            bool copied = false;
            try
            {
                var original = EditorSceneManager.OpenScene(EFMapAudit.ScenePath, OpenSceneMode.Single);
                var baseline = FindWorld(original);
                ValidateSource(baseline);
                // saveAsCopy leaves the original scene path and its asset untouched.
                if (!EditorSceneManager.SaveScene(original, ScenePath, true)) throw new IOException("Could not create the experiment scene copy.");
                copied = true;
                var copy = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var world = FindWorld(copy);
                Build(world);
                Validate(world);
                EditorSceneManager.MarkSceneDirty(copy);
                if (!EditorSceneManager.SaveScene(copy)) throw new IOException("Could not save the experiment copy.");
                if (Sha256(EFMapAudit.ScenePath) != sourceHash) throw new IOException("Original map SHA256 changed during copy creation. Inspect external/editor activity; no automatic restoration was attempted.");
                Debug.Log("P3 EXPERIMENT CREATED: " + ScenePath + ". Original map SHA256 unchanged: " + sourceHash + ". Shared prefabs and build scene order were not saved or replaced.");
            }
            catch (Exception error)
            {
                Debug.LogError("P3 experiment creation failed: " + error.Message +
                    ". Partial copy on disk: " + File.Exists(ScenePath) +
                    ". The copy and any dirty experimental scene are preserved for inspection. No file was automatically deleted. " +
                    "If a copy exists, Create P3 Copy will refuse to overwrite it; inspect the failed copy before intentionally choosing a new experiment path.");
                WriteFailure(error, sourceHash, copied);
                throw;
            }
        }

        [Serializable]
        sealed class CreationFailure
        {
            public string utc, message, stackTrace, sourceBeforeSha256, sourceAfterSha256, activeScenePath;
            public bool copiedThisAttempt, partialCopyOnDisk, activeSceneDirty, originalSceneUnchanged;
        }
        static string Sha256(string path)
        {
            using (var hash = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        static void WriteFailure(Exception error, string beforeHash, bool copied)
        {
            try
            {
                string utc = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
                string afterHash = Sha256(EFMapAudit.ScenePath);
                var active = SceneManager.GetActiveScene();
                var evidence = new CreationFailure {
                    utc=utc, message=error.Message, stackTrace=error.ToString(), sourceBeforeSha256=beforeHash,
                    sourceAfterSha256=afterHash, originalSceneUnchanged=beforeHash==afterHash,
                    copiedThisAttempt=copied, partialCopyOnDisk=File.Exists(ScenePath),
                    activeScenePath=active.path, activeSceneDirty=active.isDirty
                };
                const string directory="docs/everflow_mvp/evidence";
                Directory.CreateDirectory(directory);
                string path=directory+"/p3-creation-failure-"+utc+"-"+Guid.NewGuid().ToString("N").Substring(0,8)+".json";
                File.WriteAllText(path,JsonUtility.ToJson(evidence,true));
                Debug.LogError("P3 creation failure evidence saved: "+path+". Original SHA256 unchanged: "+evidence.originalSceneUnchanged);
            }
            catch(Exception evidenceError)
            { Debug.LogError("Could not write P3 creation failure evidence: "+evidenceError.Message+". The original failure is still propagated."); }
        }

        static EFMapWorld FindWorld(Scene scene) => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EFMapWorld>(true)).Single();
        static void ValidateSource(EFMapWorld world)
        {
            if (world.stages == null || world.stages.Length != 6 || world.player == null) throw new InvalidOperationException("Expected the existing six-stage Map integration.");
            var nodes = world.stages[3].nodes;
            if (nodes.Count(n => n != null && n.kind == MapKind.Source) != 1 || nodes.Count(n => n != null && n.kind == MapKind.Water) != 1 || world.gate4 == null)
                throw new InvalidOperationException("Canal node structure differs; inspect before creating an experiment.");
            var water = nodes.Single(n => n.kind == MapKind.Water);
            if (water.waterSurface == null || water.rideVisual == null || water.ridePath.Length < 5 || water.supply != world.gate4 || world.sideWater == null)
                throw new InvalidOperationException("Missing existing water, gate, leaf or ride path references.");
            if (world.gameObject.scene.GetRootGameObjects().Any(g => g.GetComponentsInChildren<EFCanalExperiment>(true).Length > 0))
                throw new InvalidOperationException("Source already contains an experiment; refusing duplicate generation.");
        }

        static void Build(EFMapWorld world)
        {
            ValidateSource(world);
            var transforms = world.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            origin = transforms.Single(t => t.name == "EF_Terrain").position;
            root = new GameObject(ExperimentName).transform;
            root.SetParent(world.transform, true);
            var source = world.stages[3].nodes.Single(n => n.kind == MapKind.Source);
            var water = world.stages[3].nodes.Single(n => n.kind == MapKind.Water);
            world.requireGroundedArrivals = true;
            world.stabilizeCompletedStages = true;
            stone = source.GetComponentsInChildren<Renderer>().First(r => r.name.EndsWith("_Base")).sharedMaterial;
            flow = AssetDatabase.LoadAssetAtPath<Material>("Assets/EverflowMap/Materials/Flow.mat");
            if (stone == null || flow == null) throw new InvalidOperationException("Existing stone/flow material missing.");

            // Scene instance overrides only. No ApplyPrefabInstance/asset editing.
            RaiseAndCompress(water.transform);
            RaiseAndCompress(world.gate4.transform);
            var sideRoot = transforms.Single(t => t.name == "SM_SideChannel" && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject));
            RaiseAndCompress(sideRoot);
            source.motion.position = origin + new Vector3(-9.7f, 22.85f, -50.3f);
            RecordInstanceOverride(source.motion);
            source.label = "Canal pump — PULSE to send water through the pipe";
            water.label = "Canal water — FREEZE water, then E beside the leaf";
            water.rideVisual.position = origin + new Vector3(-22,31,-46.5f);
            RecordInstanceOverride(water.rideVisual);

            var yard = Box("Low maintenance courtyard", new Vector3(-10, 22.55f, -50), new Vector3(6, .5f, 7));
            Stairs("Existing ridge to courtyard",new Vector3(2,14.6f,-48),new Vector3(-7,22.8f,-48),25,3.2f);
            // Reach full height outside the east edge of the deck before
            // walking onto it, rather than climbing under its solid underside.
            Stairs("Short side access", new Vector3(-6.9f, 22.8f, -52), new Vector3(-15.9f, 31, -47), 25, 3.2f);
            var dock = Box("Raised departure deck", new Vector3(-20, 30.75f, -47), new Vector3(7, .5f, 5));
            var checkpoint = Marker("Departure checkpoint after actual grounded arrival", new Vector3(-19.6f, 32.12f, -47));
            // Reuse the five existing path markers and leaf transport behavior.
            Vector3[] path = {
                new Vector3(-22,32.05f,-46.5f), new Vector3(-24,32.1f,-42),
                new Vector3(-24,26.2f,-6.2f), new Vector3(-24,28.2f,0), new Vector3(-26,27.12f,8)
            };
            for (int i = 0; i < path.Length; i++)
            {
                water.ridePath[i].position = origin + path[i];
                RecordInstanceOverride(water.ridePath[i]);
            }
            var exit = Box("High far-side exit deck", new Vector3(-26, 25.7f, 9), new Vector3(9, .6f, 8));
            // The end of the ride is before the arrival line: walk across the deck.
            world.stages[3].arrival.position = origin + new Vector3(-26, 27.15f, 11);
            world.stages[3].arrival.rotation = Quaternion.identity;
            RecordInstanceOverride(world.stages[3].arrival);
            world.stages[3].arrivalSize = new Vector3(8, 2.1f, 2);
            // Keep P5's original low checkpoint. Raising it would also raise
            // World.FallFloor and incorrectly respawn a player descending to P5.
            world.stages[3].hint = "Canal experiment (map P4): observe the raised exit and gap. PULSE the low courtyard pump, follow its pipe, FREEZE the water. Use the short side stairs; E beside the leaf. Walk to the far deck marker. Falling preserves prepared water/ice; R intentionally resets this puzzle.";

            // Downstream safety route reconnects the elevated experiment to the
            // original P5 plateau. Two broad flights avoid a lethal 16 m drop.
            Stairs("Exit descent upper", new Vector3(-27.5f,26,13), new Vector3(-27.5f,18,25), 20, 3);
            Box("Descent turn landing", new Vector3(-25.25f,17.75f,25), new Vector3(7.5f,.5f,3));
            Stairs("Exit descent lower", new Vector3(-22.5f,18,25), new Vector3(-22.5f,10.1f,13), 20, 3);
            Box("Original plateau reconnection",new Vector3(-22.5f,9.85f,12.5f),new Vector3(4,.5f,3));

            // A physical pipe communicates supply; its small flow strips are
            // visual only and never create an alternate traversable bridge.
            var indicators = new List<Renderer>();
            // The low collection basin and high trough are the SAME water
            // node. Either viewpoint can freeze it; no hidden visit condition.
            var basin=Box("Visible low water collection basin",new Vector3(-9.3f,22.87f,-52.5f),new Vector3(2.5f,.14f,1.3f));
            basin.gameObject.AddComponent<EFMapAbilityTarget>().receiver=water;
            var basinWater=Beam("Low basin water or ice",new Vector3(-10.3f,23,-52.5f),new Vector3(-8.3f,23,-52.5f),.35f,water.waterMaterial);
            basinWater.transform.rotation=Quaternion.identity;
            basinWater.transform.localScale=new Vector3(2.2f,.08f,1);
            basinWater.enabled=false;
            var lowTarget=Marker("Ability target - low basin water",new Vector3(-9.3f,23.15f,-52.5f));
            var lowBox=lowTarget.gameObject.AddComponent<BoxCollider>(); lowBox.size=new Vector3(2.3f,.5f,1.1f); lowBox.isTrigger=true;
            lowTarget.gameObject.AddComponent<EFMapAbilityTarget>().receiver=water;
            Vector3[] pipe = {new Vector3(-10,23.2f,-51.7f),new Vector3(-15,23.2f,-51.7f),new Vector3(-15,31.5f,-51.7f),new Vector3(-24,31.5f,-51.7f),new Vector3(-24,31.5f,-44)};
            for (int i=1;i<pipe.Length;i++) indicators.Add(Beam("Pump supply pipe " + i,pipe[i-1],pipe[i],.22f,stone));
            var localFlow = new List<GameObject>();
            for (int i=1;i<pipe.Length;i++) localFlow.Add(Beam("Water travelling in supply pipe " + i,pipe[i-1]+Vector3.up*.16f,pipe[i]+Vector3.up*.16f,.08f,flow).gameObject);
            // Tall visible goal frame makes the far landing legible from below.
            Box("Far exit marker left",new Vector3(-30,28,11),new Vector3(.35f,4,.35f),flow);
            Box("Far exit marker right",new Vector3(-22,28,11),new Vector3(.35f,4,.35f),flow);
            Box("Far exit marker lintel",new Vector3(-26,30,11),new Vector3(8.35f,.35f,.35f),flow);
            var experiment = root.gameObject.AddComponent<EFCanalExperiment>();
            experiment.world=world; experiment.source=source; experiment.gate=world.gate4; experiment.water=water;
            experiment.departureFloor=dock; experiment.departureCheckpoint=checkpoint; experiment.arrivalFloor=exit;
            experiment.fallRecovery=new Bounds(origin+new Vector3(-25,8.5f,-8),new Vector3(22,28,72));
            experiment.departureRecovery=new Bounds(origin+new Vector3(-20,25.5f,-47),new Vector3(9,9.4f,7));
            experiment.originalTerrain=transforms.Single(t=>t.name=="EF_Terrain");
            experiment.feedIndicators=indicators.ToArray(); experiment.flowingWater=localFlow.ToArray();
            experiment.lowWaterSurfaces=new[]{basinWater};
            foreach(var display in experiment.flowingWater) display.SetActive(false);
            // Prevent obsolete scene checkpoint triggers from pre-saving a high
            // location. Local behavior above requires actual stable deck contact.
            foreach (var old in world.GetComponentsInChildren<EFMapCheckpoint>(true)) if (old.stage==3)
            { old.enabled=false; RecordInstanceOverride(old); }
            EditorUtility.SetDirty(world);
            EditorUtility.SetDirty(source);
            EditorUtility.SetDirty(water);
            RecordInstanceOverride(source);
            RecordInstanceOverride(water);
            Physics.SyncTransforms();
        }

        static void RaiseAndCompress(Transform modelRoot)
        {
            modelRoot.position += Vector3.up * 19;
            var scale=modelRoot.localScale; scale.y *= .5f; modelRoot.localScale=scale;
            RecordInstanceOverride(modelRoot);
        }
        static void RecordInstanceOverride(UnityEngine.Object target)
        {
            if (target != null && PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
        static Transform Marker(string name, Vector3 p)
        { var t=new GameObject(name).transform; t.SetParent(root,true); t.position=origin+p; return t; }
        static BoxCollider Box(string name, Vector3 p, Vector3 size, Material material=null)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(root,true);
            go.transform.position=origin+p; go.transform.localScale=size; go.GetComponent<Renderer>().sharedMaterial=material==null?stone:material;
            return go.GetComponent<BoxCollider>();
        }
        static Renderer Beam(string name, Vector3 a, Vector3 b, float width, Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(root,true);
            go.transform.position=origin+(a+b)*.5f; go.transform.rotation=Quaternion.LookRotation(b-a);
            go.transform.localScale=new Vector3(width,width,Vector3.Distance(a,b)); go.GetComponent<Renderer>().sharedMaterial=material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go.GetComponent<Renderer>();
        }
        static void Stairs(string name,Vector3 a,Vector3 b,int count,float width)
        {
            Vector3 flat=Vector3.ProjectOnPlane(b-a,Vector3.up); float tread=flat.magnitude/count;
            for(int i=0;i<count;i++)
            {
                float t=(i+.5f)/count; Vector3 p=Vector3.Lerp(a,b,t);
                float top=Mathf.Lerp(a.y,b.y,(float)(i+1)/count);
                var body=Box(name+" "+(i+1),new Vector3(p.x,top-.25f,p.z),new Vector3(width,.5f,tread+.06f));
                body.transform.rotation=Quaternion.LookRotation(flat);
            }
        }

        [Serializable]
        sealed class AccessRepairEvidence
        {
            public string utc, scene, backup, beforeSha256, afterSha256, message;
            public bool saved, protectedAssetsUnchanged;
            public int changedSteps;
        }

        [MenuItem("Tools/EVERFLOW/MVP Experiment/Repair P3 Access")]
        public static void RepairAccess()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Stop Play and wait for import/compilation before access repair.");
            for (int i=0; i<SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Current scene has unsaved changes; repair never saves unrelated edits.");
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Existing P3 copy required; repair does not recreate it.",ScenePath);

            string stamp=DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
            const string directory="docs/everflow_mvp/evidence/resume-20261017";
            Directory.CreateDirectory(directory);
            var evidence=new AccessRepairEvidence {
                utc=DateTime.UtcNow.ToString("O"), scene=ScenePath, beforeSha256=Sha256(ScenePath),
                backup=directory+"/p3-before-access-repair-"+stamp+".unity"
            };
            string protectedBefore=EFCanalExperimentTests.ProtectedSnapshot();
            try
            {
                // A byte-for-byte, uniquely named backup preserves the existing
                // experimental scene; no shared prefab asset is applied/saved.
                File.Copy(ScenePath,evidence.backup,false);
                var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
                var world=FindWorld(scene);
                var e=world.GetComponentsInChildren<EFCanalExperiment>(true).Single();
                var steps=e.GetComponentsInChildren<Transform>(true);
                var terrain=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="EF_Terrain");
                Vector3 offset=terrain.position;
                var edits=new List<Transform>();
                for(int i=1;i<=25;i++)
                {
                    edits.Add(steps.Single(t=>t.name=="Existing ridge to courtyard "+i));
                    edits.Add(steps.Single(t=>t.name=="Short side access "+i));
                }
                // Reject unexpected user edits instead of silently replacing
                // arbitrary geometry. Accept only our old/new generated profiles.
                foreach(var step in edits)
                {
                    bool ridge=step.name.StartsWith("Existing ridge",StringComparison.Ordinal);
                    int index=int.Parse(step.name.Substring(step.name.LastIndexOf(' ')+1));
                    Vector3 oldA=ridge?new Vector3(2,15.3f,-48):new Vector3(-11,22.8f,-52);
                    Vector3 oldB=ridge?new Vector3(-7,22.8f,-48):new Vector3(-20,31,-47);
                    Vector3 newA=ridge?new Vector3(2,14.6f,-48):new Vector3(-6.9f,22.8f,-52);
                    Vector3 newB=ridge?oldB:new Vector3(-15.9f,31,-47);
                    bool original=StepMatches(step,offset,oldA,oldB,index);
                    bool repaired=StepMatches(step,offset,newA,newB,index);
                    if(!original && !repaired)
                        throw new InvalidOperationException("Unexpected edited access geometry; preserved for manual review: "+step.name);
                }
                Undo.IncrementCurrentGroup();
                int undo=Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Repair experimental P3 stair access");
                Undo.RecordObjects(edits.Cast<UnityEngine.Object>().ToArray(),"Repair experimental P3 stair access");
                foreach(var step in edits)
                {
                    bool ridge=step.name.StartsWith("Existing ridge",StringComparison.Ordinal);
                    int index=int.Parse(step.name.Substring(step.name.LastIndexOf(' ')+1));
                    Vector3 a=ridge?new Vector3(2,14.6f,-48):new Vector3(-6.9f,22.8f,-52);
                    Vector3 b=ridge?new Vector3(-7,22.8f,-48):new Vector3(-15.9f,31,-47);
                    if(StepMatches(step,offset,a,b,index)) continue;
                    SetStepProfile(step,offset,a,b,index);
                    evidence.changedSteps++;
                }
                Undo.CollapseUndoOperations(undo);
                Physics.SyncTransforms();
                Validate(world);
                if(evidence.changedSteps>0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save repaired P3 copy.");
                }
                evidence.saved=true;
                evidence.afterSha256=Sha256(ScenePath);
                evidence.protectedAssetsUnchanged=protectedBefore==EFCanalExperimentTests.ProtectedSnapshot();
                if(!evidence.protectedAssetsUnchanged) throw new IOException("Protected original/shared assets changed; inspect external/editor activity. No automatic restoration attempted.");
                evidence.message="Saved experimental access repair only. Actual approach/aim/ride checks must be rerun; this is not a physical traversal PASS.";
                Debug.Log("P3 ACCESS REPAIR: "+evidence.changedSteps+" steps; backup="+evidence.backup+". "+evidence.message);
            }
            catch(Exception error)
            {
                evidence.message=error.ToString();
                evidence.afterSha256=Sha256(ScenePath);
                evidence.protectedAssetsUnchanged=protectedBefore==EFCanalExperimentTests.ProtectedSnapshot();
                Debug.LogError("P3 access repair failed. Existing backup and any dirty experimental scene preserved; use Undo/review before saving. "+error.Message);
                throw;
            }
            finally
            {
                File.WriteAllText(directory+"/p3-access-repair-"+stamp+".json",JsonUtility.ToJson(evidence,true));
            }
        }

        static Vector3 StepPosition(Vector3 a,Vector3 b,int index)
        {
            var p=Vector3.Lerp(a,b,(index-.5f)/25);
            p.y=Mathf.Lerp(a.y,b.y,index/25f)-.25f;
            return p;
        }
        static bool StepMatches(Transform step,Vector3 offset,Vector3 a,Vector3 b,int index)
        {
            Vector3 flat=Vector3.ProjectOnPlane(b-a,Vector3.up);
            Vector3 size=new Vector3(3.2f,.5f,flat.magnitude/25+.06f);
            var box=step.GetComponent<BoxCollider>();
            return box!=null && box.enabled && !box.isTrigger && box.center.sqrMagnitude<.000001f &&
                Vector3.Distance(box.size,Vector3.one)<.001f &&
                Vector3.Distance(step.position,offset+StepPosition(a,b,index))<.015f &&
                Vector3.Distance(step.lossyScale,size)<.015f &&
                Quaternion.Angle(step.rotation,Quaternion.LookRotation(flat))<.1f;
        }
        static void SetStepProfile(Transform step,Vector3 offset,Vector3 a,Vector3 b,int index)
        {
            Vector3 flat=Vector3.ProjectOnPlane(b-a,Vector3.up);
            step.position=offset+StepPosition(a,b,index);
            step.rotation=Quaternion.LookRotation(flat);
            step.localScale=new Vector3(3.2f,.5f,flat.magnitude/25+.06f);
            EditorUtility.SetDirty(step);
        }

        public static void Validate(EFMapWorld world)
        {
            if(world.gameObject.scene.path!=ScenePath) throw new InvalidOperationException("Validation requires the experiment scene.");
            origin=world.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="EF_Terrain").position;
            var experiments=world.GetComponentsInChildren<EFCanalExperiment>(true);
            if(experiments.Length!=1) throw new InvalidOperationException("Expected exactly one canal experiment.");
            var e=experiments[0];
            if(e.departureFloor==null||e.arrivalFloor==null||e.departureCheckpoint==null||e.water.ridePath.Any(t=>t==null)) throw new InvalidOperationException("Incomplete canal references.");
            if(e.arrivalFloor.bounds.max.y<=origin.y+22.8f+2) throw new InvalidOperationException("Exit must visibly rise above the low courtyard.");
            if(e.water.supply!=e.gate||e.gate.supply!=e.source) throw new InvalidOperationException("Pump/gate/water causality disconnected.");
            RequireCapsuleClear(e.departureCheckpoint.position,"Departure checkpoint");
            RequireCapsuleClear(e.water.ridePath[e.water.ridePath.Length-1].position,"Leaf far-side landing");
            RequireCapsuleClear(origin+new Vector3(-11,23.92f,-49),"Low courtyard walking space");
            if(e.fallRecovery.Contains(world.stages[3].spawn.position)||e.fallRecovery.Contains(e.departureCheckpoint.position)) throw new InvalidOperationException("Fall recovery overlaps an active spawn.");
            if(!e.source.motion.GetComponentsInChildren<Collider>(true).Any(c=>EFMapAbilityTarget.Resolve(c)==e.source)) throw new InvalidOperationException("Moved pump motion has no ability collision receiver.");
            var basinTarget=e.GetComponentsInChildren<EFMapAbilityTarget>(true).Single(t=>t.name=="Ability target - low basin water");
            if(basinTarget.receiver!=e.water||Vector3.Distance(basinTarget.transform.position,origin+new Vector3(-10,23.92f,-50))>world.player.castRange) throw new InvalidOperationException("Low basin FREEZE target is disconnected or out of range.");
            foreach(var stage in world.stages) if(stage.spawn==null||stage.arrival==null||stage.nodes.Any(n=>n==null)||stage.requirements.Any(n=>n==null)) throw new InvalidOperationException("Missing stage reference.");
            Debug.Log("P3 EXPERIMENT STATIC REFERENCES PASS. This is not an Editor Play or physical traversal test.");
        }
        static void RequireCapsuleClear(Vector3 center,string name)
        {
            // Same 2 m / .4 m capsule as the preserved player prefab; triggers
            // are aim/arrival sensors and cannot block movement.
            var blocked=Physics.OverlapCapsule(center-Vector3.up*.6f,center+Vector3.up*.6f,.39f,
                ~(1<<2),QueryTriggerInteraction.Ignore).Where(c=>c.GetComponentInParent<EFMapPlayer>()==null).ToArray();
            if(blocked.Length>0) throw new InvalidOperationException(name+" capsule blocked by: "+string.Join(", ",blocked.Select(c=>c.name)));
        }
    }
}
