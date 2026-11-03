"""Read-only serialization and preservation checks for the generated P3 scene.

This does not import a scene, invoke Unity physics, or prove a traversal.
"""
from pathlib import Path
import argparse
import datetime
import hashlib
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
SCENE = ROOT / 'Assets/Scenes/Everflow_Map_P3_DesignTest_20261016.unity'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--access-repair', type=Path, help='Explicit successful repair evidence; keep the original baseline intact.')
    args = parser.parse_args()
    checks = []

    def check(value, description):
        checks.append({'pass': bool(value), 'description': description})

    data = SCENE.read_text(encoding='utf-8-sig')
    pieces = re.split(r'(?=^--- !u!)', data, flags=re.M)[1:]
    ids = [int(re.match(r'--- !u!\d+ &(-?\d+)', p)[1]) for p in pieces]
    blocks = dict(zip(ids, pieces))
    check(len(ids) == len(blocks), 'Serialized local file IDs are unique')
    references = {int(x) for x in re.findall(r'\{fileID: (-?\d+)\}', data)} - {0}
    missing = sorted(references - blocks.keys())
    check(not missing, 'All exact local {fileID} references resolve (external GUID references excluded)')
    for script in ('EverflowMap.EFMapWorld', 'EverflowMap.EFCanalExperiment'):
        check(sum('Assembly-CSharp::' + script + '\n' in b for b in pieces) == 1,
              'Exactly one saved ' + script)
    parent_errors = []
    for file_id, block in blocks.items():
        children = re.search(r'  m_Children:(.*?)\n  m_Father:', block, re.S)
        if not children:
            continue
        for child in map(int, re.findall(r'fileID: (-?\d+)', children[1])):
            if child not in blocks:
                continue
            parent = re.search(r'  m_Father: \{fileID: (-?\d+)\}', blocks[child])
            if parent and int(parent[1]) != file_id:
                parent_errors.append([file_id, child, int(parent[1])])
    check(not parent_errors, 'Transform parent/child ownership is consistent')

    required = set(re.findall(r'm_Script: \{fileID: 11500000, guid: ([a-f0-9]{32})', data))
    own = {}
    for meta in (ROOT / 'Assets').rglob('*.cs.meta'):
        match = re.search(r'^guid: ([a-f0-9]{32})', meta.read_text(encoding='utf-8-sig'), re.M)
        if match:
            own[match[1]] = str(meta.relative_to(ROOT))
    unresolved = required - own.keys()
    if unresolved:
        query = subprocess.run(['rg', '-l', '-g', '*.cs.meta', '-e',
                                'guid: (' + '|'.join(sorted(unresolved)) + ')', 'Library/PackageCache'],
                               cwd=ROOT, capture_output=True, text=True)
        for name in query.stdout.splitlines():
            match = re.search(r'^guid: ([a-f0-9]{32})', (ROOT / name).read_text(encoding='utf-8-sig'), re.M)
            if match:
                unresolved.discard(match[1])
    check(not unresolved, 'All saved MonoBehaviour script GUIDs resolve in Assets or installed PackageCache')

    def reference(block, field):
        return int(re.search(r'^  ' + re.escape(field) + r': \{fileID: (-?\d+)\}', block, re.M)[1])

    experiment = next(b for b in pieces if 'Assembly-CSharp::EverflowMap.EFCanalExperiment\n' in b)
    for field, class_id in (('departureFloor', 65), ('arrivalFloor', 65), ('departureCheckpoint', 4)):
        target = reference(experiment, field)
        check(target in blocks and blocks[target].startswith('--- !u!' + str(class_id) + ' '),
              field + ' references the expected Unity component type')
    world = blocks[reference(experiment, 'world')]
    check('  requireGroundedArrivals: 1\n' in world and '  stabilizeCompletedStages: 1\n' in world,
          'Saved copy enables both experimental completion safety flags')
    node_ids = {field: reference(experiment, field) for field in ('source', 'gate', 'water')}
    for field, file_id in node_ids.items():
        check('  stage: 3\n' in blocks[file_id], field + ': planning canal role / actual UI P4 / stage index 3')
    check(reference(blocks[node_ids['water']], 'supply') == node_ids['gate'] and
          reference(blocks[node_ids['gate']], 'supply') == node_ids['source'],
          'Saved source -> gate -> water causal references are connected')

    before = json.loads((ROOT / 'docs/everflow_mvp/evidence/resume-20261017/protected-before.json').read_text(encoding='utf-8'))
    changed = [p for p, digest in before['files'].items()
               if not (ROOT / p).is_file() or hashlib.sha256((ROOT / p).read_bytes()).hexdigest() != digest]
    allowed_repair = None
    if args.access_repair:
        evidence_folder = (ROOT / 'docs/everflow_mvp/evidence/resume-20261017').resolve()
        path = args.access_repair.resolve()
        if path.parent != evidence_folder or not path.name.startswith('p3-access-repair-'):
            raise ValueError('Repair evidence must be the generated JSON under the current evidence directory.')
        repair = json.loads(path.read_text(encoding='utf-8-sig'))
        backup = (ROOT / repair['backup']).resolve()
        if backup.parent != evidence_folder:
            raise ValueError('Backup must stay in the evidence directory.')
        scene_name = SCENE.relative_to(ROOT).as_posix()
        backup_data = backup.read_bytes()
        current_hash = hashlib.sha256(SCENE.read_bytes()).hexdigest()
        provenance = (repair['scene'] == scene_name and repair.get('saved') is True and
                      repair.get('protectedAssetsUnchanged') is True and
                      repair['beforeSha256'] == before['files'][scene_name] and
                      hashlib.sha256(backup_data).hexdigest() == repair['beforeSha256'] and
                      current_hash == repair['afterSha256'])
        check(provenance, 'Explicit repair evidence links preserved original copy, byte backup and current saved copy')
        old_pieces = re.split(r'(?=^--- !u!)', backup_data.decode('utf-8-sig'), flags=re.M)[1:]
        old_blocks = {int(re.match(r'--- !u!\d+ &(-?\d+)', p)[1]): p for p in old_pieces}
        changed_ids = sorted(k for k in blocks if old_blocks.get(k) != blocks[k])
        unexpected = []
        # Only position/rotation/scale on the two generated stair profiles may
        # differ. IDs, all gameplay components and unrelated assets must remain.
        for k in changed_ids:
            old, new = old_blocks.get(k, ''), blocks[k]
            if not new.startswith('--- !u!4 ') or not old.startswith('--- !u!4 '):
                unexpected.append(k)
                continue
            go_id = reference(new, 'm_GameObject')
            name = re.search(r'^  m_Name: (.*)$', blocks[go_id], re.M)
            if name is None or not re.fullmatch(r'(Existing ridge to courtyard|Short side access) ([1-9]|1[0-9]|2[0-5])', name[1]):
                unexpected.append(k)
                continue
            mutable = r'^  m_Local(?:Rotation|Position|Scale|EulerAnglesHint):.*$'
            if re.sub(mutable, '', old, flags=re.M) != re.sub(mutable, '', new, flags=re.M):
                unexpected.append(k)
        restricted = old_blocks.keys() == blocks.keys() and not unexpected
        check(restricted, 'Saved repair changes only generated stair Transform position/rotation/scale; all other scene blocks unchanged')
        allowed_repair = {'evidence': str(path.relative_to(ROOT)), 'backup': repair['backup'],
                          'changed_transform_ids': changed_ids, 'unexpected_ids': unexpected,
                          'provenance_valid': provenance, 'restricted_diff': restricted}
        if provenance and restricted and scene_name in changed:
            changed.remove(scene_name)
    check(not changed, 'Resume baseline: protected scenes/assets unchanged except an explicitly verified stair repair')
    report = {'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
              'kind': 'Static serialization/reference/hash checks only. No Unity import or physics execution.',
              'scene': str(SCENE.relative_to(ROOT)), 'blocks': len(blocks),
              'local_references': len(references), 'missing_local_ids': missing,
              'parent_errors': parent_errors, 'unresolved_script_guids': sorted(unresolved),
              'changed_protected_files': changed, 'checks': checks, 'pass': all(c['pass'] for c in checks)}
    report['explicit_access_repair'] = allowed_repair
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
    target = ROOT / ('docs/everflow_mvp/evidence/resume-20261017/saved-scene-' + stamp + '.json')
    target.write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps({'report': str(target.relative_to(ROOT)), 'blocks': len(blocks),
                      'checks': len(checks), 'pass': report['pass'],
                      'failures': [c['description'] for c in checks if not c['pass']]}, indent=2))
    raise SystemExit(0 if report['pass'] else 1)


if __name__ == '__main__':
    main()
