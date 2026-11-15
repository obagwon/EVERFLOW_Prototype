"""Static saved-scene checks; does not substitute for Unity deserialization or Play Mode."""
from pathlib import Path
import re, json, hashlib, subprocess

ROOT = Path(__file__).resolve().parents[2]
SCENE = ROOT / 'Assets/Scenes/Everflow_Map_v2_test.unity'
checks = []
def check(ok, label):
    checks.append({'pass': bool(ok), 'check': label})
    if not ok: raise AssertionError(label)

text = SCENE.read_text(encoding='utf-8-sig')
text = re.sub(r'[ \t]+$', '', text, flags=re.M)
headers = list(re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n', text, re.M))
docs = {int(m[2]): text[m.end():headers[i+1].start() if i+1 < len(headers) else len(text)] for i,m in enumerate(headers)}
check(len(headers) == len(docs), 'Saved scene has unique object/component IDs')
for match in re.finditer(r'\{fileID: (-?\d+)\}', text):
    fid = int(match[1])
    check(fid == 0 or fid in docs, f'Local fileID resolves: {fid}')
check('8b1a777fe6ac69b4f8194cf47a225d18' not in text and '1235368018' not in text, 'Legacy EFMapCamera and all references removed from target scene')
check('m_TagString: MainCamera' in docs[1235368013] and text.count('m_TagString: MainCamera') == 1, 'Original MainCamera tag and Camera object retained')
check('m_GameObject: {fileID: 1235368013}' in docs[1235368015], 'Original rendering Camera component retained')
check('m_GameObject: {fileID: 1235368013}' in docs[1235368019] and 'CinemachineBrain' in docs[1235368019], 'Existing Main Camera contains Brain')
check('UpdateMethod: 1' in docs[1235368019], 'Brain uses LateUpdate for CharacterController movement')
player_instance = docs[880000020]
check('propertyPath: view\n      value:\n      objectReference: {fileID: 1235368015}' in player_instance, 'Player view still references original rendering camera')
check('propertyPath: follow\n      value:\n      objectReference: {fileID: 0}' in player_instance, 'Player legacy follow override is null')
check('propertyPath: cinemachineRig\n      value:\n      objectReference: {fileID: 890000008}' in player_instance, 'Player Cinemachine bridge is a scene override')
check('addedObject: {fileID: 890000011}' in player_instance and 'm_Father: {fileID: 880000022}' in docs[890000011], 'CameraTarget is an added child of prefab player root')
check('m_LocalPosition: {x: 0, y: 0.65, z: 0}' in docs[890000011], 'CameraTarget is chest height above centred capsule origin')
for name, fid in [('CinemachineCamera',890000002),('CinemachineOrbitalFollow',890000003),('CinemachineRotationComposer',890000004),('CinemachineDeoccluder',890000005),('CinemachineInputAxisController',890000006),('EFMapCinemachineRig',890000008)]:
    check(name in docs[fid] and 'm_GameObject: {fileID: 890000000}' in docs[fid], 'FreeLook Camera component: ' + name)
check('TrackingTarget: {fileID: 890000011}' in docs[890000002] and 'LookAtTarget: {fileID: 890000011}' in docs[890000002], 'Follow and LookAt resolve to CameraTarget')
check('BindingMode: 4' in docs[890000003], 'Orbital Follow binding is WorldSpace (installed 3.1.7 enum)')
check(docs[890000003].count('Enabled: 0') == 3, 'Recentering is disabled on all three axes')
check('Range: {x: -180, y: 180}\n    Wrap: 1' in docs[890000003] and 'Range: {x: -30, y: 65}\n    Wrap: 0' in docs[890000003], 'Horizontal wraps 360 degrees; vertical clamps -30 to 65 degrees')
check('Strategy: 0' in docs[890000005] and 'DampingWhenOccluded: 0' in docs[890000005] and 'Damping: 0.35' in docs[890000005], 'Deoccluder pulls forward immediately and damps return')
check('m_Bits: 1' in docs[890000005], 'Deoccluder tests Default terrain; excludes player layer 2, FX, water and UI')
controllers = docs[890000006]
check(controllers.count('CancelDeltaTime: 1') == 3 and 'Gain: 0.14' in controllers and 'Gain: -0.119' in controllers, 'Axis controller uses pixel sensitivity with Cancel Delta Time')
check(controllers.count('LegacyInput:\n') == 3, 'Both input backends cannot double-feed mouse input')
actions = json.loads((ROOT / 'Assets/EverflowMap/V2Camera.inputactions').read_text())
check(actions['maps'][0]['bindings'][0]['path'] == '<Mouse>/delta', 'Look action reads Mouse delta')
reference = (ROOT / 'Assets/EverflowMap/V2CameraLook.asset').read_text()
check('m_ActionId: ' + actions['maps'][0]['actions'][0]['id'] in reference, 'InputActionReference action UUID matches Look')
action_meta = (ROOT / 'Assets/EverflowMap/V2Camera.inputactions.meta').read_text()
action_guid = re.search(r'^guid: (\w+)', action_meta, re.M)[1]
check('guid: ' + action_guid in reference and 'fileID: -944628639613478452' in reference, 'InputActionReference points to the inputaction importer main asset')
reference_guid = re.search(r'^guid: (\w+)', (ROOT / 'Assets/EverflowMap/V2CameraLook.asset.meta').read_text(), re.M)[1]
check(controllers.count(reference_guid) == 2, 'Horizontal and vertical controllers share Look reference')

# Resolve every external GUID in the scene against Assets and the installed package cache.
guids = {}
required_guids = set(re.findall(r'guid: ([0-9a-f]{32})', text))
scan = subprocess.run(['rg', '--no-heading', '-n', '-g', '*.meta', '^guid: (' + '|'.join(required_guids) + ')', 'Assets', 'Library/PackageCache'], cwd=ROOT, capture_output=True, text=True, check=True)
for line in scan.stdout.splitlines():
    match = re.match(r'(.+\.meta):\d+:guid: (\w+)', line)
    if match: guids[match[2]] = match[1][:-5]
for guid in required_guids:
    check(guid.startswith('0000000000000000') or guid in guids, 'External scene GUID resolves: ' + guid)
main_components = re.findall(r'component: \{fileID: (\d+)\}', docs[1235368013])
main_scripts = [guids[re.search(r'm_Script: \{fileID: 11500000, guid: (\w+)', docs[int(c)])[1]] for c in main_components if 'm_Script:' in docs[int(c)]]
check(len(main_scripts) == 2 and any('CinemachineBrain' in s for s in main_scripts) and any('UniversalAdditionalCameraData' in s for s in main_scripts), 'Main Camera has only Brain and URP scripts; no custom transform owner')
player_code = (ROOT / 'Assets/EverflowMap/Runtime/EFMapPlayer.cs').read_text()
check('Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized' in player_code, 'Movement retains actual camera horizontal-forward fallback when legacy follow is null')
check('m_PrefabInstance: {fileID: 880000020}' in docs[880000021], 'Serialized player remains linked to the original prefab instance')
manifest = json.loads((ROOT / 'Packages/manifest.json').read_text())
lock = json.loads((ROOT / 'Packages/packages-lock.json').read_text())
check(manifest['dependencies']['com.unity.cinemachine'] == lock['dependencies']['com.unity.cinemachine']['version'] == '3.1.7', 'Manifest and lock agree on Cinemachine 3.1.7')
check('propertyPath: puzzleLabLocomotion\n      value: 1' in player_instance, 'Target scene remains standalone exploration; ability gameplay is not enabled here')
before = ROOT / 'Temp/cinemachine-before.unity'
if before.exists():
    baseline = re.sub(r'[ \t]+$', '', before.read_text(encoding='utf-8-sig'), flags=re.M)
    old_headers = list(re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n', baseline, re.M))
    old_docs = {int(m[2]):baseline[m.end():old_headers[i+1].start() if i+1 < len(old_headers) else len(baseline)] for i,m in enumerate(old_headers)}
    changed = {880000020,1235368013,1235368016,1235368019,9223372036854775807}
    check(set(old_docs) - set(docs) == {1235368018}, 'Only the legacy camera component was removed from original scene objects')
    check(all(docs.get(fid) == body for fid,body in old_docs.items() if fid not in changed | {1235368018}), 'All original non-camera scene objects and geometry overrides are preserved')
result = {'kind':'STATIC ONLY; Unity scene reopening and Play Mode not executed', 'scene_sha256':hashlib.sha256(SCENE.read_bytes()).hexdigest(), 'passed':len(checks), 'checks':checks, 'main_camera_scripts':main_scripts}
output = ROOT / 'docs/cinemachine/static-checks.json'
output.write_text(json.dumps(result, indent=2), encoding='utf-8')
print(f'{len(checks)} static assertions passed; {output}')
