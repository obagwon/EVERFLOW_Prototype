"""One command for offline preservation, scene/GUID, C# and state checks.

Does not launch Unity or change any device state. Runtime/ordinary play remain
separate and must never be inferred from this script's successful result.
"""
from pathlib import Path
import datetime
import hashlib
import json
import os
import re
import subprocess
import sys
sys.path.insert(0,str(Path(__file__).parent))
from compile_v2_puzzles import main as compile_all
from verify_v2_puzzle_geometry import main as geometry
from verify_v2_exploration import parse

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'docs/v2-puzzle-validation'

def main():
    checks=[]
    def check(condition,label):
        assert condition,label
        checks.append(label);print('PASS',label)
    source=ROOT/'Assets/Scenes/Everflow_Map_v2_test.unity'
    scene=ROOT/'Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity'
    baseline=json.loads((OUT/'preserved-hashes.json').read_text(encoding='utf-8-sig'))
    for record in baseline:
        check(hashlib.sha256(Path(record['Path']).read_bytes()).hexdigest().upper()==record['Hash'], 'Preserved '+Path(record['Path']).relative_to(ROOT).as_posix())
    def norm(text):return text.replace('\r\n','\n').strip()
    before=(OUT/'preexisting-changes.patch').read_text(encoding='utf-8-sig')
    diff=subprocess.run(['git','diff','--binary'],cwd=ROOT,capture_output=True,encoding='utf-8',errors='replace',check=True).stdout
    check(norm(before)==norm(diff),'All existing tracked user changes match task-start diff; no shared source edits')
    a=source.read_text(encoding='utf-8-sig');b=scene.read_text(encoding='utf-8-sig')
    script_guid=re.search(r'^guid: (\w+)',(ROOT/'Assets/EverflowMap/Runtime/EFV2PuzzleWorld.cs.meta').read_text(),re.M)[1]
    original_guid=re.search(r'^guid: (\w+)',(ROOT/'Assets/EverflowMap/Runtime/EFMapExploration.cs.meta').read_text(),re.M)[1]
    expected=a.replace(original_guid,script_guid).replace('Assembly-CSharp::EverflowMap.EFMapExploration','Assembly-CSharp::EverflowMap.EFV2PuzzleWorld').replace('m_Name: V2 Exploration\n','m_Name: EVERFLOW V2.3 Puzzle World\n')
    check(b==expected,'Cloned scene differs only in scene-local puzzle host; FBX/player/Cinemachine instance YAML retained')
    parsed=parse(b)
    for fileid,(_,_,body) in parsed.items():
        for ref,tail in re.findall(r'\{fileID: (-?\d+)([^}]*)\}',body):
            check_id=int(ref)
            assert not check_id or 'guid:' in tail or check_id in parsed,(fileid,ref)
    check(True,'All cloned scene local fileID references resolve')
    guids={}
    for p in (ROOT/'Assets').rglob('*.meta'):
        text=p.read_text(encoding='utf-8-sig',errors='replace')
        match=re.search(r'^guid: (\w+)',text,re.M)
        if match:guids.setdefault(match[1],[]).append(p)
    new_metas=list((ROOT/'Assets/EverflowMap/Runtime').glob('EFV2*.meta'))+list((ROOT/'Assets/EverflowMap/Editor').glob('EFV2*.meta'))+[Path(str(scene)+'.meta')]
    for p in new_metas:
        guid=re.search(r'^guid: (\w+)',p.read_text(),re.M)[1]
        assert len(guids[guid])==1,(guid,guids[guid])
    check(True,f'{len(new_metas)} new asset GUIDs are unique across Assets')
    scene_guid=re.search(r'^guid: (\w+)',Path(str(scene)+'.meta').read_text(),re.M)[1]
    check(scene_guid!=re.search(r'^guid: (\w+)',Path(str(source)+'.meta').read_text(),re.M)[1],'Scene has independent meta GUID')
    runtime=(ROOT/'Assets/EverflowMap/Runtime/EFV2PuzzleWorld.cs').read_text(encoding='utf-8')
    check('KeyCode.Q' not in runtime and 'Ability.Pulse' not in runtime and 'StageIndex' not in runtime,'V2 host has no manual recall, PULSE or stage ability restrictions')
    compile_all()
    env=os.environ.copy();env['DOTNET_CLI_HOME']=str(ROOT/'Temp/dotnet-v2-puzzle');env['DOTNET_SKIP_FIRST_TIME_EXPERIENCE']='1';env['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
    run=subprocess.run(['C:/Program Files/dotnet/dotnet.exe','run','--project','tools/v2-puzzle-state-checks/StateChecks.csproj','--configuration','Release','--no-restore','--verbosity','quiet'],cwd=ROOT,env=env,capture_output=True,encoding='utf-8',errors='replace',timeout=90)
    (OUT/'managed-state-checks.txt').write_text(run.stdout+run.stderr,encoding='utf-8')
    check(run.returncode==0 and 'TOTAL 69 checks + 256 circuit combinations; 0 failures' in run.stdout,'69 production state checks + 256 circuit combinations')
    geometry()
    geom=json.loads((OUT/'geometry-checks.json').read_text())
    check(geom['embedded_samples']==0,'Nine generated stair centerline samples show no embedded original horizontal floor within body clearance')
    report={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'kind':'Offline verifier only; see PLAY_RESULTS.md for separate Unity execution evidence','checks':checks,'passed':True,'unity_import':False,'unity_play':False,'ordinary_input_playthrough':False,
        'limitations':['Cached-reference Roslyn is not Unity import or player build.','FBX analytical height tests do not simulate Unity CharacterController, moving support, camera collision, occluded aiming or unintended skips.','This offline verifier does not run Unity or measure human play time. Separate actual Unity test evidence is in PLAY_RESULTS.md.']}
    (OUT/'offline-results.json').write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding='utf-8')
    print('ALL OFFLINE CHECKS PASS. SEE PLAY_RESULTS.md FOR SEPARATE UNITY PLAY EVIDENCE.')

if __name__=='__main__':main()
