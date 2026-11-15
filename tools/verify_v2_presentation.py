"""Static preservation checks for ability art / opening presentation. Not Play."""
from pathlib import Path
import datetime
import hashlib
import json
import re
import subprocess
from verify_v2_guidance import method

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'docs/ability-intro-validation'

def main():
    baseline=json.loads((OUT/'baseline-sha256.json').read_text())
    allowed={
        'Assets/EverflowMap/Editor/EFV2EditorBridge.cs',
        'Assets/EverflowMap/Editor/EFV2GuidanceChecks.cs',
        'Assets/EverflowMap/Editor/EFV2PuzzlePlayChecks.cs',
        'Assets/EverflowMap/Editor/EFV2WaterPlayChecks.cs',
        'Assets/EverflowMap/Runtime/EFV2Guidance.cs',
        'Assets/EverflowMap/Runtime/EFV2PuzzleWorld.cs',
        'Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity',
    }
    changed=[p for p,h in baseline.items() if not (ROOT/p).is_file() or hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=h]
    unexpected=set(changed)-allowed
    assert not unexpected,sorted(unexpected)
    assert subprocess.check_output(['git','diff','--binary'],cwd=ROOT,stderr=subprocess.DEVNULL).replace(b'\r\n',b'\n')==(OUT/'baseline-git.diff').read_bytes().replace(b'\r\n',b'\n'),'Preexisting tracked user changes differ'
    before=(OUT/'world-before.cs.txt').read_text(encoding='utf-8-sig')
    after=(ROOT/'Assets/EverflowMap/Runtime/EFV2PuzzleWorld.cs').read_text(encoding='utf-8-sig')
    identical=['Awake','Record','Select','CycleFlow','TickCircuit','Show','TickOrbs','LateUpdate','CanUse','TryCast','Reach','Respawn','BeginCinematic','EndCinematic','OnApplicationFocus','OnDisable']
    for name in identical:assert method(before,name)==method(after,name),'Gameplay method changed: '+name
    assert method(after,'Start').replace('\n            if(presentation!=null)presentation.Begin();','')==method(before,'Start')
    assert method(after,'Update').replace('\n            if (OpeningActive) return;','')==method(before,'Update')
    assert method(after,'Restart').replace('\n            if(presentation!=null)presentation.Finish();','')==method(before,'Restart')
    old=method(before,'SetPaused').replace('player.enabled=!paused && cinematicUntil<=0;','player.enabled=InputActive;').replace('player.cinemachineRig.SetInputActive(!paused && cinematicUntil<=0);','player.cinemachineRig.SetInputActive(InputActive);')
    assert old==method(after,'SetPaused')
    guids={}
    for p in (ROOT/'Assets').rglob('*.meta'):
        m=re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8-sig',errors='replace'),re.M)
        if m:guids.setdefault(m[1],[]).append(p)
    for name in ['Runtime/EFV2ScenePresentation','Editor/EFV2PresentationTools','Editor/EFV2PresentationChecks']:
        p=ROOT/('Assets/EverflowMap/'+name+'.cs.meta')
        guid=re.search(r'^guid: (\w+)',p.read_text(),re.M)[1]
        assert len(guids[guid])==1,'Duplicate new script GUID'
    scene=(ROOT/'Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity').read_text(encoding='utf-8-sig')
    for name in ['bloom','freeze','pulse']:
        guid=re.search(r'^guid: (\w+)',(ROOT/f'Assets/icon/{name}_icon.png.meta').read_text(),re.M)[1]
        assert guid in scene,'Reference icon missing in saved scene: '+name
    assert 'overviewSeconds: 3' in scene and 'approachSeconds: 1.25' in scene
    result={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'kind':'STATIC ONLY; read runtime reports for actual Play results','passed':True,'protected_baseline_files':len(baseline)-len(allowed),'expected_changed_files':sorted(changed),'byte_identical_world_methods':identical,'lifecycle_changes':'Only intro Begin/early return/Cancel and shared input gate; checked exact remaining bodies','references':'3 existing sprites, scene-specific component and unique new script GUIDs','preserved':['Existing tracked user diff','Original map/v2/PuzzleLab scenes and FBX','Original UI/camera scripts and icon importers','Player/input/Cinemachine rig and cleanup geometry','All water assets and circuit rules','Project/pipeline/quality/build/package settings']}
    (OUT/'preservation-results.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print('PASS static preservation:',result['protected_baseline_files'],'protected files;',len(identical),'world methods identical; lifecycle hooks limited; images/unique GUIDs valid.')

if __name__=='__main__':main()
