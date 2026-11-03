"""Read-only environment diagnosis. Never launches Editor or changes licensing.

Only allowlisted Editor.log lines are retained: raw Hub/Editor logs can contain
authentication arguments and must not be copied to task evidence.
"""
from pathlib import Path
import datetime
import hashlib
import json
import re
import subprocess

from run_mvp_experiment_unity import ROOT, UNITY, inspect_launch_context, launch_blockers

CLI = Path('C:/Users/vzd12/AppData/Local/Unity/bin/unity.exe')
EDITOR_LOG = Path('C:/Users/vzd12/AppData/Local/Unity/Editor/Editor.log')


def safe_editor_excerpt(text):
    prefixes = ('Built from ', 'BatchMode:', 'Date:',
                '[Licensing::IpcConnector] Successfully connected to:',
                '[Licensing::Module] Successfully connected to LicensingClient',
                '[Licensing::Client] Successfully updated license,',
                '  Product:', '  Type:', '  Expiration:',
                "Loaded scene 'Assets/Scenes/Everflow_Map.unity'",
                '[INFO ] [relay] log Unity client requested immediate shutdown',
                '[Licensing::IpcConnector] LicenseClient-vzd12 channel disconnected',
                'Cleanup mono')
    lines = [line for line in text.splitlines() if line.startswith(prefixes)]
    if any(re.search(r'access.?token|password|secret|authorization', line, re.I) for line in lines):
        raise RuntimeError('Sensitive line rejected; raw Editor log was not saved.')
    return '\n'.join(lines)


def main():
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
    evidence = ROOT / 'docs/everflow_mvp/evidence/followup-20261016'
    evidence.mkdir(parents=True, exist_ok=True)
    context = inspect_launch_context()
    report = {'utc': stamp, 'actual_context': context, 'batch_launch_blockers': launch_blockers(context),
              'editor_path': str(UNITY), 'editor_path_exists': UNITY.is_file(),
              'hub_path_exists': Path('C:/Program Files/Unity Hub/Unity Hub.exe').is_file(),
              'editor_started': False, 'licensing_modified': False, 'processes_stopped': False,
              'discovery_caveat': 'Restricted accounts may see incomplete process/CLI discovery. Ask the user before deciding to start a second Editor.'}
    if EDITOR_LOG.is_file():
        data = EDITOR_LOG.read_bytes()
        text = data.decode('utf-8', errors='replace')
        excerpt = safe_editor_excerpt(text)
        excerpt_path = evidence / ('editor-excerpt-' + stamp + '.txt')
        excerpt_path.write_text(excerpt + '\n', encoding='utf-8')
        report['interactive_editor_log'] = {
            'source': str(EDITOR_LOG), 'sha256': hashlib.sha256(data).hexdigest(),
            'retained_excerpt': str(excerpt_path.relative_to(ROOT)),
            'hub_launch_observed': '-useHub\n' in text,
            'license_connection_success_observed': '[Licensing::Module] Successfully connected to LicensingClient' in text,
            'license_expiration': re.findall(r'^  Expiration:\s*(\S+)', text, re.M),
            'normal_shutdown_observed': 'Cleanup mono' in text,
            'raw_log_saved': False,
            'note': 'Historical log observations, not proof of current Editor liveness or exact pipe failure cause.'}
    if CLI.is_file():
        observations = []
        for arguments in (['version'], ['status'], ['pipeline', 'list']):
            command = [str(CLI), *arguments, '--json', '--non-interactive']
            run = subprocess.run(command, cwd=ROOT, capture_output=True, encoding='utf-8', errors='replace', timeout=20)
            try:
                result = json.loads(run.stdout)
            except json.JSONDecodeError:
                result = {'parse_failed': True}  # Do not retain untrusted raw diagnostics.
            observations.append({'command': command, 'exit_code': run.returncode, 'result': result})
        report['cli_read_only_observations'] = observations
    target = evidence / ('environment-diagnosis-' + stamp + '.json')
    target.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf-8')
    print(json.dumps({'report': str(target.relative_to(ROOT)), 'context': context,
                      'batch_launch_blockers': report['batch_launch_blockers'],
                      'editor_started': False, 'licensing_modified': False}, indent=2, ensure_ascii=False))


if __name__ == '__main__':
    main()
