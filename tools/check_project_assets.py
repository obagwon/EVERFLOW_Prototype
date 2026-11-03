"""Read-only Unity serialization audit. Does not import assets or prove Play behavior.

GUID reachability is diagnostic only: absence is never evidence for deletion.
"""
from pathlib import Path
import argparse
import collections
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
GUID = re.compile(r'\bguid: ([a-f0-9]{32})')
HEADER = re.compile(r'^--- !u!(\d+) &(-?\d+).*$', re.M)
SERIALIZED = {'.unity', '.prefab', '.asset', '.mat', '.anim', '.controller',
              '.overrideController', '.playable'}


def audit():
    assets = sorted(p for p in (ROOT / 'Assets').rglob('*') if p.is_file())
    owners, duplicates, missing_meta, orphan_meta = {}, [], [], []
    for p in assets:
        if p.suffix == '.meta':
            target = Path(str(p)[:-5])
            if not target.exists():
                orphan_meta.append(p.relative_to(ROOT).as_posix())
            match = re.search(r'^guid: ([a-f0-9]{32})$', p.read_text(encoding='utf-8-sig'), re.M)
            if match:
                guid = match[1]
                if guid in owners:
                    duplicates.append([owners[guid], target.relative_to(ROOT).as_posix()])
                owners[guid] = target.relative_to(ROOT).as_posix()
        elif not Path(str(p) + '.meta').exists():
            missing_meta.append(p.relative_to(ROOT).as_posix())

    data, dependencies, references = {}, {}, collections.defaultdict(set)
    scenes, local_errors, missing_scripts, invariants = {}, [], [], []
    script_references = set()
    for p in assets:
        if p.suffix not in SERIALIZED:
            continue
        content = p.read_text(encoding='utf-8-sig', errors='replace')
        if not content.startswith('%YAML'):
            continue  # Binary assets require Unity; never label them unused.
        path = p.relative_to(ROOT).as_posix()
        data[path] = content
        deps = {g for g in GUID.findall(content) if not g.startswith('0000000000000000')}
        dependencies[path] = sorted(deps)
        for guid in deps:
            references[guid].add(path)
        script_references.update(re.findall(r'm_Script: \{fileID: 11500000, guid: ([a-f0-9]{32})', content))
        headers = list(HEADER.finditer(content))
        blocks = {int(h[2]): (int(h[1]), content[h.end():headers[i+1].start() if i+1 < len(headers) else len(content)])
                  for i, h in enumerate(headers)}
        if len(blocks) != len(headers):
            local_errors.append({'asset': path, 'duplicate_local_ids': True})
        local = {int(x) for x in re.findall(r'\{fileID: (-?\d+)\}', content)} - {0}
        if local - blocks.keys():
            local_errors.append({'asset': path, 'missing_local_ids': sorted(local - blocks.keys())})
        for ident, (kind, block) in blocks.items():
            if kind == 114 and re.search(r'm_Script: \{fileID: 0\}', block):
                missing_scripts.append({'asset': path, 'fileID': ident})
        if p.suffix != '.unity':
            continue
        counts = collections.Counter()
        def active(ident, seen=None):
            seen = set() if seen is None else seen
            if ident in seen or ident not in blocks:
                return False
            seen.add(ident)
            _, block = blocks[ident]
            if '  m_IsActive: 0\n' in block:
                return False
            transform = next((b for k, b in blocks.values() if k in (4, 224) and
                              re.search(r'm_GameObject: \{fileID: ' + str(ident) + r'\}', b)), '')
            father = re.search(r'm_Father: \{fileID: (-?\d+)\}', transform)
            if father and int(father[1]) in blocks:
                parent = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', blocks[int(father[1])][1])
                if parent:
                    return active(int(parent[1]), seen)
            return True
        for kind, block in blocks.values():
            script = re.search(r'm_EditorClassIdentifier: .*::([^\n]+)', block)
            name = {20: 'Camera', 81: 'AudioListener'}.get(kind)
            if script:
                name = script[1].strip()
            if name:
                counts[name] += 1
                go = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', block)
                if go and active(int(go[1])) and '  m_Enabled: 0\n' not in block:
                    counts['active:' + name] += 1
        scenes[path] = {'blocks': len(blocks), 'components': dict(counts)}
        if path == 'Assets/Scenes/Everflow_Map_MVP.unity':
            def check(value, name):
                invariants.append({'pass': bool(value), 'description': name})
            def component(name):
                matches = [(i, b) for i, (k, b) in blocks.items() if k == 114 and
                           'Assembly-CSharp::EverflowMap.' + name + '\n' in b]
                check(len(matches) == 1, 'Exactly one ' + name)
                return matches[0] if len(matches) == 1 else (0, '')
            def ref(block, field):
                match = re.search(r'^  ' + re.escape(field) + r': \{fileID: (-?\d+)\}', block, re.M)
                return int(match[1]) if match else 0
            wid, world = component('EFMapWorld')
            fid, finale = component('EFMvpFinale')
            component('EFMapPlayer')
            component('EFCanalExperiment')
            check(ref(world, 'mvpFinale') == fid and ref(finale, 'world') == wid,
                  'World and finale references are reciprocal')
            for field, kind in {'growth': 114, 'machine': 114, 'connection': 114, 'gate': 114, 'finalControl': 114,
                                'bloomRoute': 65, 'nearDeck': 65, 'frozenRoute': 65, 'farDeck': 65,
                                'nearCheckpoint': 4, 'farCheckpoint': 4, 'reactionCamera': 20,
                                'restoredLight': 108, 'feedback': 82, 'grounding': 114}.items():
                ident = ref(finale, field)
                check(ident in blocks and blocks[ident][0] == kind, 'Finale reference: ' + field)
            gate = blocks.get(ref(finale, 'gate'), (0, ''))[1]
            check(ref(gate, 'supply') == ref(finale, 'finalControl') != ref(finale, 'machine'),
                  'Only the separate last PULSE supplies the final gate')
            check('  requireGroundedArrivals: 1\n' in world and '  stabilizeCompletedStages: 1\n' in world,
                  'Grounded arrival and completed-stage preservation remain enabled')
            for label in ('Camera', 'AudioListener'):
                check(counts['active:' + label] == 1, 'Exactly one serialized active ' + label)
            for _, (kind, block) in blocks.items():
                if kind == 114 and 'Assembly-CSharp::EverflowMap.EFMapAbilityTarget\n' in block:
                    receiver_id = ref(block, 'receiver')
                    # EFMapAbilityTarget explicitly uses null to mark solid
                    # non-targets such as pillars, rather than missing wiring.
                    if receiver_id:
                        receiver = blocks.get(receiver_id, (0, ''))[1]
                        check('Assembly-CSharp::EverflowMap.EFMapNode\n' in receiver,
                              'Ability target receiver resolves to a map node')

    unresolved = set(references) - owners.keys()
    package_owners = {}
    if unresolved and (ROOT / 'Library/PackageCache').exists():
        query = subprocess.run(['rg', '-n', '-g', '*.meta', '^guid: (' + '|'.join(sorted(unresolved)) + ')$',
                                'Library/PackageCache'], cwd=ROOT, capture_output=True, text=True)
        if query.returncode not in (0, 1):
            raise RuntimeError('Package GUID lookup failed: ' + query.stderr)
        for line in query.stdout.splitlines():
            match = re.match(r'(.+\.meta):\d+:guid: ([a-f0-9]{32})', line)
            if match:
                package_owners[match[2]] = match[1].replace('\\', '/')[:-5]
    unresolved -= package_owners.keys()
    unresolved_scripts = sorted(unresolved & script_references)

    settings = (ROOT / 'ProjectSettings/EditorBuildSettings.asset').read_text(encoding='utf-8-sig')
    build_scenes, build_errors = [], []
    for enabled, path, guid in re.findall(r'- enabled: ([01])\s+path: (.+)\s+guid: ([a-f0-9]{32})', settings):
        entry = {'path': path, 'enabled': enabled == '1', 'guid': guid,
                 'exists': (ROOT / path).is_file(), 'guid_matches': owners.get(guid) == path}
        build_scenes.append(entry)
        if not entry['exists'] or not entry['guid_matches']:
            build_errors.append(entry)
    title = (ROOT / 'Assets/EverflowMVP/Runtime/EFTitleScreen.cs').read_text(encoding='utf-8-sig')
    route = re.search(r'GameplayScene = "([^"]+)"', title)[1]
    enabled = [s['path'] for s in build_scenes if s['enabled']]
    route_ok = enabled == ['Assets/EverflowMVP/Scenes/Everflow_Title.unity', route]
    reachable, pending = set(), list(enabled)
    while pending:
        path = pending.pop()
        if path in reachable:
            continue
        reachable.add(path)
        pending.extend(owners[g] for g in dependencies.get(path, []) if g in owners)
    report = {'kind': 'Static GUID/local-reference audit only. No Unity import, subasset-ID validation or physics.',
              'duplicate_guids': duplicates, 'missing_meta': missing_meta, 'orphan_meta': orphan_meta,
              'unresolved_guids': {g: sorted(references[g]) for g in sorted(unresolved)},
              'package_guids_resolved': len(package_owners), 'local_reference_errors': local_errors,
              'unresolved_script_guids': unresolved_scripts, 'mvp_invariants': invariants,
              'explicit_missing_scripts': missing_scripts, 'scenes': scenes, 'build_scenes': build_scenes,
              'build_errors': build_errors, 'title_route': route, 'title_route_matches_build': route_ok,
              'release_serialized_dependencies': sorted(reachable),
              'special_folders': [p.relative_to(ROOT).as_posix() for p in (ROOT / 'Assets').rglob('*')
                                  if p.is_dir() and p.name in ('Resources', 'StreamingAssets', 'AddressableAssetsData')],
              'asmdefs': [p.relative_to(ROOT).as_posix() for p in (ROOT / 'Assets').rglob('*.asmdef')]}
    report['pass'] = not any((duplicates, missing_meta, orphan_meta, unresolved, local_errors,
                              missing_scripts, build_errors)) and route_ok and all(c['pass'] for c in invariants)
    report['scene_and_script_pass'] = not any((duplicates, missing_meta, orphan_meta, unresolved_scripts,
                                               local_errors, missing_scripts, build_errors)) and route_ok and all(c['pass'] for c in invariants)
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT / 'docs/everflow_mvp/evidence/cleanup-audit.json')
    args = parser.parse_args()
    result = audit()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
    print(json.dumps({k: result[k] for k in ('pass', 'duplicate_guids', 'missing_meta', 'orphan_meta',
                                            'unresolved_guids', 'local_reference_errors', 'build_errors')}, indent=2))
    raise SystemExit(0 if result['pass'] else 1)
