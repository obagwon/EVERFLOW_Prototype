"""Create isolated copies of the shipped MVP components; never edits the originals."""
from pathlib import Path
import uuid

root = Path(__file__).resolve().parents[1]
src = root / 'Assets/EverflowMVP'
dst = root / 'Assets/EverflowGraybox'
if (dst / 'Runtime/EFWorld.cs').exists():
    raise SystemExit('Bootstrap already applied. Refusing to overwrite authored graybox scripts.')
(dst / 'Runtime').mkdir(parents=True, exist_ok=True)
(dst / 'Editor').mkdir(exist_ok=True)
for name in ['EFWorld', 'EFPlayer', 'EFNode', 'EFCamera', 'EFZone', 'EFInput', 'EFLayout']:
    text = (src / f'Runtime/{name}.cs').read_text(encoding='utf-8-sig').replace('namespace EverflowMVP', 'namespace EverflowGraybox')
    if name == 'EFPlayer':
        text = text.replace('public bool IsRiding', 'public bool FlowerUsed, VineUsed, SlideUsed, FallingUsed;\n        public bool IsRiding')
        text = text.replace('velocity.y = ground.launchVelocity.y;', 'FlowerUsed = true;\n                        velocity.y = ground.launchVelocity.y;')
        text = text.replace('if (hit.normal.y > .5f) ground = hit.collider.GetComponentInParent<EFNode>();', 'if (hit.normal.y > .5f) { ground = hit.collider.GetComponentInParent<EFNode>(); if (ground != null && ground.kind == NodeKind.Falling && ground.Frozen) FallingUsed = true; }')
        text = text.replace('ride = closest;', 'ride = closest;\n            if (ride.kind == NodeKind.Vine) VineUsed = true; else SlideUsed = true;')
        text = text.replace('Vector3 release = ride.SwingVelocity;', 'Vector3 release = ride.transform.parent.InverseTransformDirection(ride.SwingVelocity);')
        text = text.replace('EndRide(release);', 'EndRide(ride.transform.parent.TransformDirection(release));')
        text = text.replace('EndRide(Vector3.forward * rideSpeed)', 'EndRide(ride.transform.parent.forward * rideSpeed)')
        text = text.replace('Vector3.right * steer', 'ride.transform.parent.right * steer')
        text = text.replace('EndRide(new Vector3(0, 10, 17))', 'EndRide(ride.transform.parent.TransformDirection(new Vector3(0, 10, 17)))')
    if name == 'EFNode':
        text = text.replace('Vector3 delta = p - transform.parent.TransformPoint(homePosition);', 'Vector3 delta = transform.parent.InverseTransformPoint(p) - homePosition;')
    if name == 'EFWorld':
        text = text.replace('public bool debugStageSelection = true;', 'public bool debugStageSelection = false;\n        public Transform initialSpawn;\n        public EFGrayboxEnvironment environment;')
        text = text.replace('void Start() { EnterStage(startStage); }', 'void Start() { EnterStage(startStage); if (startStage == 0 && initialSpawn != null) player.Teleport(initialSpawn.position, true); }')
        text = text.replace('if (debugStageSelection)', 'if (debugStageSelection && (Application.isEditor || Debug.isDebugBuild))')
        start = text.index('            if (Completed.Length > 0 && Completed[StageIndex]')
        end = text.index('\n        }', start)
        text = text[:start] + text[end:]
        text = text.replace('        public void SetCheckpoint', '''        public void EnterOnFoot(int index)
        {
            if (index == StageIndex) return;
            if (index != StageIndex + 1 || !Completed[StageIndex]) { Respawn(); return; }
            StageIndex = index;
            Checkpoint = stages[index].spawn.position;
            Message = stages[index].hint;
        }
        public void RestartWorld()
        {
            foreach (var stage in stages) foreach (var node in stage.nodes) node.ResetNode();
            System.Array.Clear(Completed, 0, Completed.Length);
            player.FlowerUsed = player.VineUsed = player.SlideUsed = player.FallingUsed = false;
            player.selected = Ability.Bloom;
            environment.ResetPresentation();
            EnterStage(0);
            player.Teleport(initialSpawn.position, true);
        }
        public void SetCheckpoint''')
        text = text.replace('            Completed[StageIndex] = false;', '''            Completed[StageIndex] = false;
            if (StageIndex == 0) player.FlowerUsed = player.VineUsed = false;
            if (StageIndex == 1) player.FallingUsed = false;
            if (StageIndex == 3) player.SlideUsed = false;''')
        text = text.replace('            foreach (EFNode node in stages[index].goalRequirements)', '''            if (index == 0 && (!player.FlowerUsed || !player.VineUsed)) return false;
            if (index == 1 && !player.FallingUsed) return false;
            if (index == 3 && !player.SlideUsed) return false;
            foreach (EFNode node in stages[index].goalRequirements)''')
        text = text.replace('            Completed[index] = true;', '            for (int i = 0; i < index; i++) if (!Completed[i]) return;\n            Completed[index] = true;')
        line = next(l for l in text.splitlines() if 'Message = index == stages.Length' in l)
        text = text.replace(line, '            Message = index == 5 ? "All six flows restored. Follow the path to the waterwheel." : "Area restored. Follow the path to the next area.";')
    (dst / f'Runtime/{name}.cs').write_text(text, encoding='utf-8')

# Reuse the original scene serializer and exact object specifications, with an isolated runtime.
text = (src / 'Editor/EFSceneTools.cs').read_text(encoding='utf-8-sig')
start = text.index('            TextAsset json =')
end = text.index('            string path = AssetDatabase.GenerateUniqueAssetPath', start)
body = text[start:end]
body = body.replace('var materials = PrepareMaterials();', '''var materials = new Dictionary<string, Material>();
            foreach (string name in new[] { "Stone", "Floor", "Bloom", "Freeze", "Pulse", "Goal", "Player", "Wall" })
                materials[name] = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/" + name + ".mat");''')
body = body.replace('objects["World"].AddComponent<EFHud>().world = world;', '')
out = '''using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace EverflowGraybox.Editor
{
    public static partial class EFGrayboxBuilder
    {
        const string Root = "Assets/EverflowMVP/";
        public const string ScenePath = "Assets/Scenes/Everflow_World_Graybox.unity";
        [MenuItem("Tools/EVERFLOW/Graybox/Create New World Copy")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            if (File.Exists(ScenePath)) throw new IOException("World already exists; open it directly. Builder never overwrites authored work.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
''' + body + '''
            Configure(objects, world, materials);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Save failed");
            Validate();
            Debug.Log("GRAYBOX BUILD PASS: " + ScenePath);
        }
    }
}
'''
(dst / 'Editor/EFGrayboxBuilder.cs').write_text(out, encoding='utf-8')
for p in dst.rglob('*.cs'):
    meta = Path(str(p) + '.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n', encoding='utf-8')
