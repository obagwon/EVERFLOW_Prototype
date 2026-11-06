"""One-time surgical scene wiring. Existing map instance and transforms remain byte-identical.

No Unity launch; new .meta GUIDs are deterministic. Does not regenerate any other scene.
"""
from pathlib import Path
import hashlib
import json
import re
from inspect_v2_fbx import inspect, ROOT
from update_v02_signage import meta

SCENE=ROOT/'Assets/Scenes/Everflow_Map_v2_test.unity'
OUT=ROOT/'docs/v2-exploration'


def selected(name):
    if name.startswith(('W_','FX_','ENV_','VOL_','UG_HangingRoot','UG_Torch')): return False
    if name.startswith('PROP_'):
        return name.startswith(('PROP_Rock','PROP_RuinPillar','PROP_G_RuinArch','PROP_W_WheelSupport','PROP_D3_MooringPost'))
    if name.startswith('PH_'):
        return name in {'PH_B_MovingStone','PH_B_MidLedge','PH_A_Tut_WitheredPad','PH_A_Tut_MovingPad',
                        'PH_A_Leaf01','PH_A_Leaf02','PH_B_Windmill_Pole','PH_C_LeafPlatform','PH_G_StoneDoor',
                        'PH_D2_Leaf','PH_D5_MovingChannel','PH_D5_BypassDoor','PH_D4_Gate','PH_D4_Winch'}
    return True


def main():
    text=SCENE.read_text(encoding='utf-8-sig')
    if '::EverflowMap.EFMapExploration' in text:
        print('UNCHANGED: exploration host already exists; scene not overwritten.'); return
    baseline=json.loads((OUT/'baseline-sha256.json').read_text())
    assert hashlib.sha256(SCENE.read_bytes()).hexdigest()==baseline[SCENE.relative_to(ROOT).as_posix()]
    script=ROOT/'Assets/EverflowMap/Runtime/EFMapExploration.cs'
    guid=meta(script); meta(ROOT/'Assets/EverflowMap/Editor/EFMapExplorationTools.cs')
    settings,rows=inspect(); names=sorted(r['name'] for r in rows if selected(r['name']))
    inventory=[{k:v for k,v in r.items() if k not in ('points','indices','properties')} | {'solid':selected(r['name'])} for r in rows]
    (OUT/'fbx-inventory.json').write_text(json.dumps({'coordinate_note':'Analytical FBX cm->m, X handedness conversion; not imported Unity physics.', 'settings':settings,'meshes':inventory},indent=2),encoding='utf-8')
    # Reuse the original capsule prefab by instance overrides, not by copying its objects.
    player_guid=meta(ROOT/'Assets/EverflowMap/Prefabs/EFMapPlayer.prefab')
    header='  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
    def go(i,name,components):
        return f'--- !u!1 &{i}\nGameObject:\n'+header+'  serializedVersion: 6\n  m_Component:\n'+''.join(f'  - component: {{fileID: {c}}}\n' for c in components)+f'  m_Layer: 0\n  m_Name: {name}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n'
    def transform(i,g,position,rotation):
        return f'--- !u!4 &{i}\nTransform:\n'+header+f'  m_GameObject: {{fileID: {g}}}\n  serializedVersion: 2\n  m_LocalRotation: {rotation}\n  m_LocalPosition: {position}\n  m_LocalScale: {{x: 1, y: 1, z: 1}}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n  m_Father: {{fileID: 0}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 180, z: 0}}\n'
    extra=go(880000001,'V2 Exploration',[880000002,880000003])+transform(880000002,880000001,'{x: 0, y: 0, z: 0}','{x: 0, y: 0, z: 0, w: 1}')
    extra+=f'--- !u!114 &880000003\nMonoBehaviour:\n'+header+f'  m_GameObject: {{fileID: 880000001}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: Assembly-CSharp::EverflowMap.EFMapExploration\n  player: {{fileID: 880000021}}\n  spawnPoint: {{fileID: 880000011}}\n  mapRoot: {{fileID: 880000030}}\n  solidMeshNames:\n'+''.join(f'  - {n}\n' for n in names)+'  fallHeight: -45\n'
    extra+=go(880000010,'SpawnPoint_V2_FirstPuzzle_Approach_TEMP',[880000011])+transform(880000011,880000010,'{x: -46, y: 1.25, z: -9}','{x: 0, y: 1, z: 0, w: 0}')
    extra+='--- !u!1001 &880000020\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_Modification:\n    serializedVersion: 3\n    m_TransformParent: {fileID: 0}\n    m_Modifications:\n'
    mods=[(925579041381698730,'puzzleLabLocomotion','1',0),
          (925579041381698730,'view','',1235368015),(925579041381698730,'follow','',1235368018),
          (3849014133912246841,'m_LocalPosition.x','-46',0),(3849014133912246841,'m_LocalPosition.y','1.25',0),
          (3849014133912246841,'m_LocalPosition.z','-9',0),(3849014133912246841,'m_LocalRotation.y','1',0),
          (3849014133912246841,'m_LocalRotation.w','0',0)]
    for target,key,value,ref in mods:
        extra+=f'    - target: {{fileID: {target}, guid: {player_guid}, type: 3}}\n      propertyPath: {key}\n      value: {value}\n      objectReference: {{fileID: {ref}}}\n'
    extra+=f'    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n    m_AddedComponents: []\n  m_SourcePrefab: {{fileID: 100100000, guid: {player_guid}, type: 3}}\n'
    extra+=f'--- !u!114 &880000021 stripped\nMonoBehaviour:\n  m_CorrespondingSourceObject: {{fileID: 925579041381698730, guid: {player_guid}, type: 3}}\n  m_PrefabInstance: {{fileID: 880000020}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: 0}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: 2df0e76407b673c43b29747d8ca55a73, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: Assembly-CSharp::EverflowMap.EFMapPlayer\n'
    extra+='--- !u!4 &880000030 stripped\nTransform:\n  m_CorrespondingSourceObject: {fileID: -8679921383154817045, guid: 1d4e7df0d3e36ea43bc217b468d03a5f, type: 3}\n  m_PrefabInstance: {fileID: 1736641463}\n  m_PrefabAsset: {fileID: 0}\n'
    text=text.replace('  player: {fileID: 0}\n','  player: {fileID: 880000021}\n')
    text=text.replace('  collisionIgnoreRoots:\n  - {fileID: 0}\n','  collisionIgnoreRoots: []\n')
    # Save the initial camera in the correct framing even before Play.
    text=text.replace('  m_LocalPosition: {x: 26.960466, y: 3.6434793, z: 36.293835}',
                      '  m_LocalPosition: {x: -46.6, y: 4.113119, z: -2.342604}')
    text=text.replace('--- !u!1660057539 &9223372036854775807\n',extra+'--- !u!1660057539 &9223372036854775807\n')
    text+='  - {fileID: 880000002}\n  - {fileID: 880000011}\n  - {fileID: 880000020}\n'
    SCENE.write_text(text,encoding='utf-8',newline='\n')
    print('Wired saved v2 scene; selected',len(names),'of',len(rows),'meshes for runtime static collision.')


if __name__=='__main__': main()
