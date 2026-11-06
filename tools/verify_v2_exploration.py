"""Offline source/scene/FBX checks and cached-reference C# compilation. Never launches Unity."""
from pathlib import Path
import datetime
import hashlib
import json
import re
import subprocess
from inspect_v2_fbx import ROOT, inspect, heights
from setup_v2_exploration import selected

OUT=ROOT/'docs/v2-exploration'
TEMP=ROOT/'Temp/V2ExplorationCompile'
SCENE='Assets/Scenes/Everflow_Map_v2_test.unity'


def parse(text):
    return {int(i):(int(k),bool(s),b) for k,i,s,b in re.findall(r'^--- !u!(\d+) &(-?\d+)( stripped)?\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S)}


def compile_source(name, runtime=None, player=False):
    candidates=sorted((ROOT/'Library/Bee/artifacts').glob(f'*/{name}.rsp'),key=lambda p:p.stat().st_mtime,reverse=True)
    candidates=[p for p in candidates if re.search(r'^-define:UNITY_EDITOR$',p.read_text(encoding='utf-8-sig'),re.M)]
    if not candidates: raise RuntimeError('No cached Editor references for '+name)
    lines=candidates[0].read_text(encoding='utf-8-sig').splitlines()
    lines=[l for l in lines if not re.match(r'[-/](out|refout|additionalfile|analyzer|analyzerconfig):',l,re.I)]
    if player: lines=[l for l in lines if not re.match(r'-define:UNITY_EDITOR(?:_WIN|_64)?$',l)]
    if runtime:
        lines=[f'-r:"{runtime.as_posix()}"' if re.match(r'-r:".*[/\\]Assembly-CSharp(?:\.ref)?\.dll"$',l) else l for l in lines]
    folder='Editor' if name.endswith('Editor') else 'Runtime'
    for area in ('EverflowMap','EverflowPuzzleLab'):
        for p in sorted((ROOT/f'Assets/{area}/{folder}').glob('*.cs')):
            if not any(p.name in l and area in l for l in lines): lines.append(f'"{p.as_posix()}"')
    suffix='-player' if player else ''
    target=TEMP/f'{name}{suffix}.dll'; response=TEMP/f'{name}{suffix}.rsp'
    lines.append(f'-out:"{target.as_posix()}"'); response.write_text('\n'.join(lines)+'\n',encoding='utf-8')
    data=Path('C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Data')
    result=subprocess.run([str(data/'NetCoreRuntime/dotnet.exe'),str(data/'DotNetSdkRoslyn/csc.dll'),'@'+str(response)],cwd=ROOT,capture_output=True,encoding='utf-8',errors='replace',timeout=90)
    (OUT/f'{name}{suffix}-compile.txt').write_text('Offline Roslyn, cached Unity references. Not Editor import or Play.\n'+result.stdout+result.stderr,encoding='utf-8')
    if result.returncode: raise RuntimeError(result.stdout+result.stderr)
    print('PASS offline compile',name+suffix)
    return target


def main():
    OUT.mkdir(parents=True,exist_ok=True); TEMP.mkdir(parents=True,exist_ok=True)
    baseline=json.loads((OUT/'baseline-sha256.json').read_text())
    changed=[p for p,h in baseline.items() if not (ROOT/p).exists() or hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=h]
    assert changed==[SCENE],changed
    (OUT/'preservation.json').write_text(json.dumps({'checked_files':len(baseline),'changed_existing_files':changed,
        'original_map_player_prefab_lab_settings_and_team_assets':'byte-identical to task start, including user edits'},indent=2),encoding='utf-8')
    print('PASS preservation',len(baseline)-1,'unchanged existing files; only v2 scene changed')
    now=parse((ROOT/SCENE).read_text(encoding='utf-8-sig'))
    before=parse((ROOT/'Backups/V2-Exploration-20261105/Everflow_Map_v2_test.unity').read_text(encoding='utf-8-sig'))
    assert now[1736641463]==before[1736641463], 'Team map instance modified'
    assert now[686077271]==before[686077271] and now[686077272]==before[686077272]
    assert now[1235368015]==before[1235368015], 'Camera intrinsic settings modified'
    for i,(k,s,b) in now.items():
        for ref,tail in re.findall(r'\{fileID: (-?\d+)([^}]*)\}',b):
            if int(ref) and 'guid:' not in tail: assert int(ref) in now,(i,ref)
    assert now[880000021][1] and now[880000030][1]
    host=now[880000003][2]
    names=re.search(r'  solidMeshNames:\n(.*?)  fallHeight:',host,re.S)[1].splitlines()
    names=[s.removeprefix('  - ') for s in names]
    _,rows=inspect(); expected=sorted(r['name'] for r in rows if selected(r['name']))
    assert names==expected and len(names)==len(set(names))
    # Capsules are centered on the prefab pivot, not at its feet.
    spawn=(-46,1.25,-9); floor=[]
    for x,z in ((-46,-9),(-46.4,-9),(-45.6,-9),(-46,-9.4),(-46,-8.6)):
        hits=[(y,r['name'],normal) for r in rows if selected(r['name']) for y,normal in heights(r,x,z) if normal>.7 and y<spawn[1]]
        assert hits
        y,n,normal=max(hits)
        assert .05<spawn[1]-1-y<.3,(x,z,y,n)
        floor.append(dict(x=x,z=z,height=y,mesh=n,normal_y=normal))
    min_y=min(r['min'][1] for r in rows if selected(r['name']))
    assert -45<min_y-5
    # Stair risers and exported ramp slopes, not CharacterController traversal.
    stair_groups={}
    for prefix in ('TWR_Flight1_Step','TWR_Flight2_Step','D1_Stairs_Step','D3_DockStairs_Step'):
        steps=sorted([r for r in rows if r['name'].startswith(prefix)],key=lambda r:r['name'])
        rise=max(abs(a['max'][1]-b['max'][1]) for a,b in zip(steps,steps[1:]))
        assert rise<.55
        stair_groups[prefix]={'steps':len(steps),'max_riser':rise,'step_offset':.55,'physics_traversal':'unverified'}
    summaries={}
    guid_paths={}
    for base in (ROOT/'Assets',ROOT/'Packages',ROOT/'Library/PackageCache'):
        if base.exists():
            for p in base.rglob('*.meta'):
                m=re.search(r'^guid: ([0-9a-f]{32})$',p.read_text(encoding='utf-8',errors='replace'),re.M)
                if m: guid_paths[m[1]]=p
    for scene in ('Assets/Scenes/Everflow_Map.unity',SCENE,'Assets/Scenes/Everflow_V02_PuzzleLab.unity','Assets/Scenes/Everflow_V02_PuzzleLab_T01_Check.unity'):
        docs=parse((ROOT/scene).read_text(encoding='utf-8-sig')); counts={}; missing=[]
        for i,(k,s,b) in docs.items():
            if k!=114: continue
            script=re.search(r'm_Script: \{fileID: (\d+)(?:, guid: ([0-9a-f]+), type: \d+)?\}',b)
            if not script or script[1]=='0' or script[2] not in guid_paths: missing.append(i)
            match=re.search(r'm_EditorClassIdentifier: (.*)',b)
            label=match[1].strip() if match else 'unspecified'
            counts[label]=counts.get(label,0)+1
        # Lab has no model prefabs; all non-external references can be checked statically.
        if 'PuzzleLab' in scene:
            for i,(k,s,b) in docs.items():
                for ref,tail in re.findall(r'\{fileID: (-?\d+)([^}]*)\}',b):
                    if int(ref) and 'guid:' not in tail: assert int(ref) in docs,(scene,i,ref)
        assert not missing,(scene,missing)
        summaries[scene]={'serialized_component_types':counts,'missing_script_guid':missing,'records':len(docs),
                          'imported_prefab_internal_fileIDs':'not resolved outside Unity'}
    result={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'unity_play':'NOT EXECUTED',
            'scene_local_references':'pass','team_map_instance_and_transforms':'preserved','collider_mesh_names':len(names),
            'spawn_floor_analytical':floor,'lowest_selected_mesh_y':min_y,'fall_height':-45,
            'stairs_analytical':stair_groups,'scenes':summaries,
            'limits':['FBX coordinate calculations do not replace Unity importer/physics validation.',
                      'Imported FBX internal fileIDs, runtime cooking, camera obstruction and human traversal unverified.']}
    (OUT/'static-results.json').write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding='utf-8')
    print('PASS scene local refs, scripts GUIDs, exact collider selection, spawn 5-point floor, stair riser mathematics')
    runtime=compile_source('Assembly-CSharp')
    compile_source('Assembly-CSharp-Editor',runtime)
    compile_source('Assembly-CSharp',player=True)
    run=subprocess.run(['dotnet','run','--project','tools/puzzle-lab-state-checks/StateChecks.csproj'],cwd=ROOT,capture_output=True,encoding='utf-8',errors='replace',timeout=90)
    (OUT/'managed-state-checks.txt').write_text(run.stdout+run.stderr,encoding='utf-8')
    assert run.returncode==0,run.stdout+run.stderr
    print(run.stdout.splitlines()[-1])


if __name__=='__main__': main()
