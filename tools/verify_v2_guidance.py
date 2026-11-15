"""Preservation + production method comparison for the presentation-only guidance pass.

No Unity execution. Read runtime-guidance/route reports separately.
"""
from pathlib import Path
import datetime,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'docs/guidance-validation'

def method(text,name):
    m=re.search(r'\b(?:void|bool)\s+'+re.escape(name)+r'\s*\([^)]*\)\s*\{',text)
    if not m:raise AssertionError('Method missing: '+name)
    # Ignore strings/comments when counting braces, preserving the exact returned source.
    p=m.end();depth=1;quote=None;escape=False;line=False;block=False
    while depth:
        c=text[p];n=text[p+1] if p+1<len(text) else ''
        if line:
            if c=='\n':line=False
        elif block:
            if c=='*' and n=='/':block=False;p+=1
        elif quote:
            if escape:escape=False
            elif c=='\\':escape=True
            elif c==quote:quote=None
        elif c=='/' and n=='/':line=True;p+=1
        elif c=='/' and n=='*':block=True;p+=1
        elif c in '\"\'':quote=c
        elif c=='{':depth+=1
        elif c=='}':depth-=1
        p+=1
    return text[m.start():p].replace('\r\n','\n')

def main():
    baseline=json.loads((OUT/'baseline-sha256.json').read_text())
    allowed={'Assets/EverflowMap/Runtime/EFV2PuzzleWorld.cs','Assets/EverflowMap/Editor/EFV2EditorBridge.cs'}
    changed=[p for p,h in baseline.items() if p not in allowed and (not (ROOT/p).is_file() or hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=h)]
    assert not changed,changed
    before=(OUT/'world-before.cs.txt').read_text(encoding='utf-8-sig');after=(ROOT/'Assets/EverflowMap/Runtime/EFV2PuzzleWorld.cs').read_text(encoding='utf-8-sig')
    methods=['Start','Record','Update','Select','CycleFlow','TickCircuit','Show','TickOrbs','LateUpdate','CanUse','TryCast','Reach','Respawn','BeginCinematic','EndCinematic','SetPaused','Restart','OnApplicationFocus','OnDisable']
    for name in methods:assert method(before,name)==method(after,name),'Gameplay method changed: '+name
    hook='\n            gameObject.AddComponent<EFV2Guidance>().world=this;'
    assert method(after,'Awake').replace(hook,'')==method(before,'Awake'),'Awake differs beyond the presentation hook'
    tracked=subprocess.check_output(['git','diff','--binary'],cwd=ROOT,stderr=subprocess.DEVNULL).replace(b'\r\n',b'\n')
    assert tracked==(OUT/'baseline-git.diff').read_bytes().replace(b'\r\n',b'\n'),'Preexisting tracked changes differ'
    guids={}
    for f in (ROOT/'Assets').rglob('*.meta'):
        match=re.search(r'^guid: (\w+)',f.read_text(encoding='utf-8-sig',errors='replace'),re.M)
        if match:guids.setdefault(match[1],[]).append(f)
    for name in ['Assets/EverflowMap/Runtime/EFV2Guidance.cs.meta','Assets/EverflowMap/Editor/EFV2GuidanceChecks.cs.meta']:
        guid=re.search(r'^guid: (\w+)',(ROOT/name).read_text(),re.M)[1];assert len(guids[guid])==1
    result={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'kind':'STATIC ONLY; not Play or first-time human usability testing','passed':True,'unchanged_baseline_files':len(baseline)-len(allowed),'byte_identical_gameplay_methods':methods,'awake_delta':'one presentation component attachment only','preserved':['Original and puzzle scene YAML/GUIDs','Team FBX/PuzzleLab/external assets','Visual cleanup builder/colliders/camera/player/input','All water assets/state integration','Project/pipeline/quality/build/package settings','Preexisting tracked user changes'],'new_guids_unique':True}
    (OUT/'preservation-results.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print('PASS protected assets and user diff; 19 gameplay method bodies byte-identical; Awake only adds guidance; new GUIDs unique.')
if __name__=='__main__':main()
