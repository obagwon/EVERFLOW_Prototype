"""Preservation checks for the scene-local visual cleanup; not a Unity Play test."""
from pathlib import Path
import hashlib,json,subprocess,datetime,re
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'docs/visual-cleanup'
def main():
    b=json.loads((OUT/'baseline-sha256.json').read_text())
    allowed={'Assets/EverflowMap/Runtime/EFV2PuzzleAssembly.cs','Assets/EverflowMap/Runtime/EFV2Plant.cs','Assets/EverflowMap/Editor/EFV2EditorBridge.cs'}
    changed=[p for p,h in b.items() if p not in allowed and (not (ROOT/p).is_file() or hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=h)]
    assert not changed,changed
    diff=subprocess.check_output(['git','diff','--binary'],cwd=ROOT,stderr=subprocess.DEVNULL)
    assert diff.replace(b'\r\n',b'\n')==(OUT/'baseline-git.diff').read_bytes().replace(b'\r\n',b'\n'),'Tracked user diff changed'
    all_guids={}
    for p in (ROOT/'Assets').rglob('*.meta'):
        m=re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8-sig',errors='replace'),re.M)
        if m:all_guids.setdefault(m[1],[]).append(p)
    for path in ['Assets/EverflowMap/Runtime/EFV2VisualCleanup.cs.meta','Assets/EverflowMap/Editor/EFV2VisualChecks.cs.meta']:
        guid=re.search(r'^guid: (\w+)',(ROOT/path).read_text(),re.M)[1];assert len(all_guids[guid])==1
    report={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'kind':'Static preservation only; see runtime reports separately','passed':True,'baseline_files':len(b),'allowed_existing_changes':sorted(allowed),'preserved':['Original scene and independent puzzle scene YAML + GUIDs','Team FBX and importer','Entire PuzzleLab source/assets','Water shaders/materials/textures/meshes/integration scripts','Puzzle state/ability/circuit/input/camera scripts','Pipeline/quality/player/build settings and packages','All pre-existing tracked user changes'],'new_script_guids_unique':True}
    (OUT/'preservation-results.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print('PASS visual cleanup preservation; '+str(len(b)-len(allowed))+' existing files unchanged, tracked user diff unchanged, new script GUIDs unique. STATIC ONLY.')
if __name__=='__main__':main()
