"""Offline preservation/reference checks. This does not execute Unity or certify Play."""
from pathlib import Path
import datetime,hashlib,json,re,subprocess,sys
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
from verify_v2_exploration import parse

def main():
    report=[]
    def check(ok,label):
        if not ok:raise AssertionError(label)
        report.append(label);print('PASS',label)
    out=ROOT/'docs/water-validation'
    baseline=json.loads((out/'baseline-sha256.json').read_text())
    allowed={'Assets/EverflowMap/Editor/EFV2EditorBridge.cs','Assets/EverflowMap/Editor/EFV2PlayRecorder.cs','Assets/EverflowMap/Editor/EFV2PlayRecorder.cs.meta','Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity'}
    changed=[]
    for p,h in baseline.items():
        f=ROOT/p
        if p not in allowed and (not f.is_file() or hashlib.sha256(f.read_bytes()).hexdigest()!=h):changed.append(p)
    check(not changed,'All task-start assets/configuration preserved outside explicit integration/bridge/recorder changes: '+str(changed))
    moved=ROOT/'Assets/EverflowMap/Runtime/EFV2PlayRecorder.cs.meta'
    check(hashlib.sha256(moved.read_bytes()).hexdigest()==baseline['Assets/EverflowMap/Editor/EFV2PlayRecorder.cs.meta'],'Play recorder moved with its original GUID')
    def norm(s):return s.replace('\r\n','\n').strip()
    diff=subprocess.check_output(['git','diff','--binary'],cwd=ROOT).decode('utf-8',errors='replace')
    check(norm(diff)==norm((out/'baseline-git.diff').read_text(encoding='utf-8')),'All pre-existing tracked user changes preserved exactly')
    guids={}
    for p in (ROOT/'Assets').rglob('*.meta'):
        match=re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8-sig',errors='replace'),re.M)
        if match:guids.setdefault(match[1],[]).append(p)
    own=list((ROOT/'Assets/EverflowWater').rglob('*.meta'))+list((ROOT/'Assets/EverflowMap').rglob('EFV2Water*.meta'))+[ROOT/'Assets/Scenes/Everflow_Water_Test.unity.meta']
    for p in own:
        guid=re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8-sig'),re.M)[1]
        check(len(guids[guid])==1,'Unique GUID '+p.relative_to(ROOT).as_posix())
    for name in ['Everflow_Map_v2_test_with_puzzle','Everflow_Water_Test']:
        s=(ROOT/f'Assets/Scenes/{name}.unity').read_text(encoding='utf-8-sig');parsed=parse(s)
        for fid,(_,_,body) in parsed.items():
            for ref,tail in re.findall(r'\{fileID: (-?\d+)([^}]*)\}',body):
                check_id=int(ref)
                assert not check_id or 'guid:' in tail or check_id in parsed,(name,fid,ref)
        check(True,name+' local fileID references resolve')
    shader=(ROOT/'Assets/EverflowWater/Shaders/EF_CoolWater.shader').read_text(encoding='utf-8')
    check('Shader "EVERFLOW/Water/CoolWater"' in shader and 'CustomEditor' not in shader,'Dedicated shader name and no missing CustomEditor')
    check('_Time' not in shader and '_FlowOffset.xyz' in shader and '_WaterTime' in shader,'All shader animation controlled by pauseable local phase/continuous world offset')
    check('ComputeWorldSpacePosition' in shader and 'UNITY_REVERSED_Z' in shader,'URP platform-aware world depth reconstruction')
    importer=(ROOT/'Assets/EverflowWater/Textures/EF_WaterNormal.png.meta').read_text()
    check('textureType: 1' in importer and 'sRGBTexture: 0' in importer,'Dedicated copied normal is linear normal-map import')
    material=(ROOT/'Assets/EverflowWater/Materials/EF_CoolWater.mat').read_text()
    check('_FoamFlowX:' not in material and '_FoamFlowZ:' not in material,'Unused source flow controls removed from copied material')
    runtime=(ROOT/'Assets/EverflowMap/Runtime/EFV2WaterIntegration.cs').read_text(encoding='utf-8')
    check('CircuitRules' not in runtime and '.Apply(LabAbility' not in runtime and '.AddComponent<Collider' not in runtime,'Presentation does not duplicate circuit/ability/physics logic')
    (out/'offline-water-results.json').write_text(json.dumps({'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'kind':'Offline only; Unity Play reports are separate','checks':report,'passed':True},ensure_ascii=False,indent=2),encoding='utf-8')

if __name__=='__main__':main()
