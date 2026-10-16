"""Independent preservation / Roslyn checks. Does not open or edit Unity assets.

Roslyn success is not a Unity import, play-mode test, or player build.
"""
from pathlib import Path
import argparse
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
REPORT = ROOT / 'docs/release-integration'
NS = {'m': 'http://schemas.microsoft.com/developer/msbuild/2003'}


def preservation(allowed):
    baseline = json.loads((REPORT / 'baseline-sha256.json').read_text(encoding='utf-8-sig'))
    changes = []
    for name, before in baseline.items():
        path = ROOT / name
        after = hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else 'MISSING'
        if before != after:
            changes.append({'path': name, 'before': before, 'after': after})
    unexpected = [item['path'] for item in changes if item['path'] not in allowed]
    return {'kind': 'SHA256 comparison to pre-task working files',
            'baseline_files': len(baseline), 'allowed_changes': sorted(allowed),
            'changed_files': changes, 'unexpected_changes': unexpected,
            'pass': not unexpected}


def compile_scripts():
    out = REPORT / 'verification-compile'
    out.mkdir(parents=True, exist_ok=True)
    csc = Path('C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Data/DotNetSdkRoslyn/csc.dll')
    results = []
    for mode in ('runtime-editor', 'editor', 'runtime-player'):
        editor = mode == 'editor'
        project = ET.parse(ROOT / ('Assembly-CSharp' + ('-Editor' if editor else '') + '.csproj'))
        references = [item.text for item in project.findall('.//m:Reference/m:HintPath', NS)]
        references = [str((ROOT / Path(item)).resolve()) if not Path(item).is_absolute() else item
                      for item in references]
        if editor:
            references.append(str(out / 'Assembly-CSharp.dll'))
        scripts = sorted(p for p in (ROOT / 'Assets').rglob('*.cs') if ('Editor' in p.relative_to(ROOT).parts) == editor)
        assembly = 'Assembly-CSharp' + ('-Editor' if editor else '-Player' if mode == 'runtime-player' else '')
        defines = project.find('.//m:DefineConstants', NS).text
        if mode == 'runtime-player':
            defines = ';'.join(value for value in defines.split(';') if not value.startswith('UNITY_EDITOR'))
        lines = ['-nologo', '-target:library', '-langversion:9.0', '-nostdlib+',
                 '-out:"' + str(out / (assembly + '.dll')) + '"', '-define:' + defines]
        lines += ['-r:"' + item + '"' for item in dict.fromkeys(references)]
        lines += ['"' + str(path) + '"' for path in scripts]
        response = out / (assembly + '.rsp')
        response.write_text('\n'.join(lines), encoding='utf-8')
        result = subprocess.run(['dotnet', str(csc), '@' + str(response)], cwd=ROOT, capture_output=True, text=True)
        diagnostics = result.stdout + result.stderr
        (out / (assembly + '-compile.txt')).write_text(diagnostics, encoding='utf-8')
        results.append({'mode': mode, 'scripts': len(scripts), 'exit_code': result.returncode,
                        'diagnostics': diagnostics})
        print(mode, 'scripts', len(scripts), 'exit', result.returncode)
        print(diagnostics)
        if result.returncode and mode == 'runtime-editor':
            break  # The editor assembly depends on this output.
    return {'kind': 'Roslyn against installed Unity 6000.3.21f1 / project reference DLLs; not Unity execution',
            'results': results, 'pass': len(results) == 3 and all(item['exit_code'] == 0 for item in results)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--allow', action='append', default=[], help='Baseline file explicitly permitted to change')
    parser.add_argument('--compile', action='store_true', help='Compile editor/runtime sources in three configurations')
    options = parser.parse_args()
    result = {'preservation': preservation(set(options.allow))}
    (REPORT / 'verification-preservation-results.json').write_text(json.dumps(result['preservation'], indent=2), encoding='utf-8')
    if options.compile:
        result['compilation'] = compile_scripts()
        (REPORT / 'verification-compile-results.json').write_text(json.dumps(result['compilation'], indent=2), encoding='utf-8')
    result['pass'] = all(item['pass'] for item in result.values())
    (REPORT / 'verification-results.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result, indent=2))
    return 0 if result['pass'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
