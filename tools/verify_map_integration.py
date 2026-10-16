"""Static preservation verification, separate from Unity/physical tests."""
import hashlib, json, re
from pathlib import Path

root=Path('docs/map-integration')
baseline=json.loads((root/'preserved-assets-sha256.json').read_text())
changed=[]
for name, before in baseline.items():
    path=Path(name)
    after=hashlib.sha256(path.read_bytes()).hexdigest() if path.exists() else 'MISSING'
    if before!=after: changed.append(name)
allowed=['Assets/Scenes/Everflow_Map.unity']
unexpected=[p for p in changed if p not in allowed]
def models(text):
    result={}
    for part in re.split(r'(?=--- !u!1001 &)',text):
        if not part.startswith('--- !u!1001 &'): continue
        source=re.search(r'm_SourcePrefab: \{fileID: \d+, guid: ([a-f0-9]+)',part)
        if not source: continue
        values={}
        for match in re.finditer(r'propertyPath: (m_Local(?:Position|Rotation|Scale)\.[xyzw])\s*\n\s*value: (\S+)',part):
            values[match.group(1)]=float(match.group(2))
        result.setdefault(source.group(1),[]).append(values)
    return result
old=models((root/'Everflow_Map.before-integration.unity.txt').read_text(encoding='utf-8-sig'))
new=models(Path('Assets/Scenes/Everflow_Map.unity').read_text(encoding='utf-8-sig'))
transform_errors=[]
for guid,instances in old.items():
    if len(new.get(guid,[]))!=len(instances): transform_errors.append('instance count '+guid);continue
    for a,b in zip(instances,new[guid]):
        for key,val in a.items():
            if key not in b or abs(val-b[key])>.0001: transform_errors.append(guid+' '+key)
fbx_count=len(list(Path('Assets/map_ver1/fbx').glob('*.fbx')))
report={
    'kind':'Static file/hash and scene serialization checks; not Unity runtime verification',
    'individual_fbx_count':fbx_count,
    'original_model_instance_count':sum(len(v) for v in old.values()),
    'baseline_file_count':len(baseline),
    'changed_baseline_files':changed,
    'unexpected_preserved_asset_changes':unexpected,
    'original_model_transform_override_errors':transform_errors,
    'pass':not unexpected and not transform_errors and fbx_count==37,
}
(root/'preservation-results.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
raise SystemExit(0 if report['pass'] else 1)
