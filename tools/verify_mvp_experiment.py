"""Offline preservation + C# compilation. Does NOT run Unity or a player.

Run from the repository root: python tools/verify_mvp_experiment.py
Baseline is captured once by the coordinator, never silently recreated here.
"""
from pathlib import Path
import argparse
import hashlib
import json
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs/everflow_mvp/evidence'
TEMP = ROOT / 'Temp/mvp-experiment'
NS = {'m': 'http://schemas.microsoft.com/developer/msbuild/2003'}
ALLOWED = {
    'Assets/EverflowMap/Runtime/EFMapWorld.cs',
    'Assets/EverflowMap/Runtime/EFMapNode.cs',
    'Assets/EverflowMVP/Runtime/EFTitleScreen.cs',
    'ProjectSettings/EditorBuildSettings.asset',
    'ProjectSettings/ProjectSettings.asset',  # Existing release policy: next patch on successful build.
    # 2026-10-17 cleanup: broken unreferenced volume subassets and validation guards.
    'Assets/Settings/DefaultVolumeProfile.asset',
    'Assets/EverflowMVP/Editor/EFReleaseSetup.cs',
    'Assets/EverflowMap/Editor/EFMapPlayTests.cs',
}


def main():
    global OUT
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--staging',action='store_true',help='Compile prepared integration sources outside Assets; not scene integration.')
    parser.add_argument('--output-dir', type=Path, help='Write new evidence separately from historical reports.')
    parser.add_argument('--baseline', type=Path, default=ROOT / 'docs/everflow_mvp/evidence/preserved-baseline.json',
                        help='Explicit path-to-SHA256 baseline; never created or updated by this command.')
    parser.add_argument('--allow-changed', action='append', default=[], help='Explicit additional changed path relative to the project root.')
    options=parser.parse_args()
    if options.staging and (ROOT/'Assets/EverflowMap/Runtime/EFMvpFinale.cs').exists():
        parser.error('Preparation snapshots are historical after integration. Run without --staging to verify current Assets.')
    if options.staging:
        OUT=ROOT/'docs/everflow_mvp/evidence/integration-20261017/prepared-compile'
    if options.output_dir:
        OUT = options.output_dir.resolve()
    OUT.mkdir(parents=True, exist_ok=True)
    TEMP.mkdir(parents=True, exist_ok=True)
    baseline = json.loads(options.baseline.read_text(encoding='utf-8'))
    allowed = ALLOWED | set(options.allow_changed)
    changed = [p for p, digest in baseline.items()
               if not (ROOT / p).is_file() or hashlib.sha256((ROOT / p).read_bytes()).hexdigest() != digest]
    preservation = {'kind': 'SHA256 comparison, not Unity verification',
                    'baseline_file_count': len(baseline), 'changed': changed,
                    'baseline': str(options.baseline), 'allowed': sorted(allowed),
                    'unexpected': sorted(set(changed) - allowed),
                    'pass': not (set(changed) - allowed)}
    (OUT / 'preservation.json').write_text(json.dumps(preservation, indent=2), encoding='utf-8')
    print('Preservation:', 'PASS' if preservation['pass'] else 'FAIL', 'changed:', changed)

    # Validate script GUID uniqueness and that every new source has its own meta.
    guids = {}
    duplicates = []
    for meta in (ROOT / 'Assets').rglob('*.meta'):
        match = re.search(r'^guid: ([a-f0-9]{32})$', meta.read_text(encoding='utf-8-sig'), re.M)
        if match:
            g = match[1]
            if g in guids:
                duplicates.append([str(guids[g].relative_to(ROOT)), str(meta.relative_to(ROOT))])
            guids[g] = meta
    new_sources = list((ROOT / 'Assets/EverflowMap').rglob('EFCanal*.cs'))
    new_sources += list((ROOT / 'Assets/EverflowMap').rglob('EFMvp*.cs'))
    missing_meta = [str(p.relative_to(ROOT)) for p in new_sources if not Path(str(p) + '.meta').is_file()]
    assets = {'kind': 'Static GUID/meta inspection, not scene import', 'duplicate_guids': duplicates,
              'new_sources': [str(p.relative_to(ROOT)) for p in new_sources], 'missing_meta': missing_meta,
              'pass': not duplicates and not missing_meta}
    (OUT / 'asset-checks.json').write_text(json.dumps(assets, indent=2), encoding='utf-8')
    print('Asset checks:', 'PASS' if assets['pass'] else 'FAIL')

    csc = Path('C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Data/DotNetSdkRoslyn/csc.dll')
    results = []
    for mode in ('runtime-editor', 'editor', 'runtime-player', 'runtime-development'):
        editor = mode == 'editor'
        project = ET.parse(ROOT / ('Assembly-CSharp' + ('-Editor' if editor else '') + '.csproj'))
        refs = [x.text for x in project.findall('.//m:Reference/m:HintPath', NS)]
        refs = [str((ROOT / p).resolve()) if not Path(p).is_absolute() else p for p in refs]
        if mode in ('runtime-player', 'runtime-development'):
            refs = [p for p in refs if not Path(p).name.startswith('UnityEditor') and
                    '.Editor' not in Path(p).stem and not Path(p).stem.endswith('-Editor')]
        if editor:
            refs.append(str(TEMP / 'Assembly-CSharp.dll'))
        scripts = sorted(p for p in (ROOT / 'Assets').rglob('*.cs') if ('Editor' in p.relative_to(ROOT).parts) == editor)
        if options.staging:
            prepared=ROOT/'tools/mvp_staging'
            if editor:
                scripts.append(prepared/'EFMvpSetup.cs')
            else:
                scripts=[p for p in scripts if p.name not in ('EFMapWorld.cs','EFMapNode.cs')]
                scripts.extend(prepared/name for name in ('EFMapWorld.cs','EFMapNode.cs','EFMvpFinale.cs','EFMvpRotary.cs'))
        assembly = 'Assembly-CSharp' + ('-Editor' if editor else '-Player' if mode == 'runtime-player' else '-Development' if mode == 'runtime-development' else '')
        defines = project.find('.//m:DefineConstants', NS).text
        if mode in ('runtime-player', 'runtime-development'):
            defines = ';'.join(d for d in defines.split(';') if not d.startswith('UNITY_EDITOR') and d != 'DEVELOPMENT_BUILD')
            if mode == 'runtime-development':
                defines += ';DEVELOPMENT_BUILD'
        args = ['-nologo', '-target:library', '-langversion:9.0', '-nostdlib+',
                '-out:"' + str(TEMP / (assembly + '.dll')) + '"', '-define:' + defines]
        args += ['-r:"' + r + '"' for r in dict.fromkeys(refs)]
        args += ['"' + str(p) + '"' for p in scripts]
        rsp = TEMP / (assembly + '.rsp')
        rsp.write_text('\n'.join(args), encoding='utf-8')
        run = subprocess.run(['dotnet', str(csc), '@' + str(rsp)], cwd=ROOT, capture_output=True, encoding='utf-8', errors='replace')
        diagnostics = run.stdout + run.stderr
        (OUT / (mode + '-compile.txt')).write_text(diagnostics or 'No compiler diagnostics.\n', encoding='utf-8')
        results.append({'mode': mode, 'scripts': len(scripts), 'exit_code': run.returncode,
                        'diagnostics': diagnostics})
        print(mode, 'exit', run.returncode, 'scripts', len(scripts))
        if diagnostics:
            print(diagnostics)
        if run.returncode:
            break
    compilation = {'kind': 'Roslyn against installed Unity/project DLLs. No Unity import, Play Mode or player build.',
                   'results': results, 'pass': len(results) == 4 and all(r['exit_code'] == 0 for r in results)}
    if compilation['pass']:
        smoke = b'EFMvpBuildSmoke'
        boundary = {'release_smoke_absent': smoke not in (TEMP / 'Assembly-CSharp-Player.dll').read_bytes(),
                    'development_smoke_present': smoke in (TEMP / 'Assembly-CSharp-Development.dll').read_bytes(),
                    'release_editor_reference_absent': b'UnityEditor' not in (TEMP / 'Assembly-CSharp-Player.dll').read_bytes(),
                    'development_editor_reference_absent': b'UnityEditor' not in (TEMP / 'Assembly-CSharp-Development.dll').read_bytes()}
        compilation['smoke_boundary'] = boundary
        compilation['pass'] = all(boundary.values())
    compilation['prepared_sources_outside_assets']=options.staging
    (OUT / 'compile-results.json').write_text(json.dumps(compilation, indent=2), encoding='utf-8')
    return 0 if preservation['pass'] and assets['pass'] and compilation['pass'] else 1


if __name__ == '__main__':
    sys.exit(main())
