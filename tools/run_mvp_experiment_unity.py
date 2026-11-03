"""Bounded Unity entry points for the experiment. Never kills a pre-existing Editor.

Run this from the normal Windows user's terminal after the IPC issue is resolved.
  python tools/run_mvp_experiment_unity.py diagnose
  python tools/run_mvp_experiment_unity.py create
  python tools/run_mvp_experiment_unity.py test
The test entry requires a previously generated experimental scene.
"""
from pathlib import Path
import argparse
import contextlib
import datetime
import json
import os
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
UNITY = Path('C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe')
OUT = ROOT / 'docs/everflow_mvp/evidence'


def inspect_launch_context():
    """Read the real token identity, not inherited USERNAME/USERPROFILE alone."""
    query = r'''
$OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$ErrorActionPreference = 'Stop'
$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$processes = @(Get-Process -Name Unity -ErrorAction SilentlyContinue | ForEach-Object {
    [ordered]@{ pid=$_.Id; path=$_.Path; title=$_.MainWindowTitle; sessionId=$_.SessionId }
})
[ordered]@{ actualIdentity=$identity.Name; environmentUser=$env:USERNAME;
    environmentProfile=$env:USERPROFILE; unityProcesses=$processes } | ConvertTo-Json -Depth 5 -Compress
'''
    result = subprocess.run(['powershell', '-NoProfile', '-Command', query], cwd=ROOT,
                            capture_output=True, encoding='utf-8', errors='replace', timeout=15)
    if result.returncode != 0 or not result.stdout.strip():
        raise RuntimeError('Cannot inspect token/process state safely. No Editor was started.')
    return json.loads(result.stdout.lstrip('\ufeff'))


def launch_blockers(context):
    """Fail closed on account mismatch, existing Editors, or unknown Unity PIDs."""
    blockers = []
    actual = context.get('actualIdentity', '').rsplit('\\', 1)[-1].casefold()
    expected = context.get('environmentUser', '').casefold()
    if not actual or not expected or actual != expected:
        blockers.append('Windows token identity differs from the inherited user environment '
                        f'({context.get("actualIdentity")} / {context.get("environmentUser")}). '
                        'Use the existing Editor menus or a normal user terminal; license expiry is not implied.')
    for process in context.get('unityProcesses', []):
        path = (process.get('path') or '').replace('\\', '/').casefold()
        if path.endswith('/unity hub/resources/unity.exe'):
            continue  # Hub helper is not the Unity Editor.
        if path.endswith('/editor/unity.exe'):
            blockers.append(f'Editor PID {process["pid"]} is already running. Use its menu; it was not stopped.')
        else:
            blockers.append(f'Cannot rule out an Editor for Unity PID {process.get("pid")}. No second Editor will be started.')
    return blockers


@contextlib.contextmanager
def reserve_batch_launch():
    """Serialize our own helpers without deleting any pre-existing lock."""
    directory = ROOT / 'Temp/mvp-experiment'
    directory.mkdir(parents=True, exist_ok=True)
    lock = directory / 'batch-launch.lock'
    try:
        handle = os.open(str(lock), os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
    except FileExistsError:
        raise SystemExit('Another experiment helper owns batch-launch.lock. No Editor was started; no lock was removed.')
    try:
        os.write(handle, str(os.getpid()).encode('ascii'))
        yield
    finally:
        os.close(handle)
        lock.unlink()  # This invocation created this exact lock, never an existing one.


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=('diagnose', 'create', 'test'))
    parser.add_argument('--timeout', type=int, default=240)
    args = parser.parse_args()
    if args.timeout < 30 or args.timeout > 1800:
        parser.error('--timeout must be 30..1800 seconds')
    OUT.mkdir(parents=True, exist_ok=True)
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
    context = inspect_launch_context()
    blockers = launch_blockers(context)
    lock = ROOT / 'Temp/UnityLockfile'
    if lock.exists():
        blockers.append('The project has a UnityLockfile. Inspect the existing Editor; this helper will not remove the lock or start another Editor.')
    inspection = {'utc': stamp, 'action': args.action, 'context': context, 'blockers': blockers,
                  'editor_started': False,
                  'note': 'Sandbox process discovery can be incomplete. No instances does not prove the GUI Editor is closed.'}
    (OUT / ('environment-' + stamp + '.json')).write_text(json.dumps(inspection, indent=2), encoding='utf-8')
    if args.action == 'diagnose':
        print(json.dumps(inspection, indent=2), flush=True)
        return
    if blockers:
        raise SystemExit('\n'.join(blockers) + '\nNo Editor was started. Do not repeat the old IPC-failing batch command.')
    scene = ROOT / 'Assets/Scenes/Everflow_Map_P3_DesignTest_20261016.unity'
    if args.action == 'test' and not scene.is_file():
        raise SystemExit('Create P3 Copy has not produced the saved experimental scene. No Editor was started.')
    if args.action == 'create' and scene.exists():
        raise SystemExit('The experiment scene already exists. Inspect it in the Editor; no copy was overwritten.')
    with reserve_batch_launch():
        # User can open a GUI while a helper is preparing. Recheck immediately
        # before launch; Unity itself owns the final project-exclusive lock.
        blockers = launch_blockers(inspect_launch_context())
        if (ROOT / 'Temp/UnityLockfile').exists():
            blockers.append('A project UnityLockfile appeared while preparing the command.')
        if blockers:
            raise SystemExit('\n'.join(blockers) + '\nNo Editor was started.')
        execute_batch(args, stamp)


def execute_batch(args, stamp):
    log = OUT / (args.action + '-unity-' + stamp + '.log')
    method = ('EverflowMap.Editor.EFCanalExperimentSetup.Create' if args.action == 'create'
              else 'EverflowMap.Editor.EFCanalExperimentTests.Run')
    command = [str(UNITY), '-batchmode', '-nographics', '-projectPath', str(ROOT),
               '-executeMethod', method, '-logFile', str(log)]
    if args.action == 'create':
        command.append('-quit')
    started = time.monotonic()
    process = subprocess.Popen(command, cwd=ROOT, creationflags=subprocess.CREATE_NO_WINDOW)
    print('Started own Editor PID', process.pid, 'action', args.action, 'log', log, flush=True)
    timed_out = False
    try:
        exit_code = process.wait(timeout=args.timeout)
    except subprocess.TimeoutExpired:
        timed_out = True
        process.kill()  # Only the Editor process created by this exact invocation.
        exit_code = process.wait()
    report = {'action': args.action, 'command': command, 'pid': process.pid,
              'timed_out': timed_out, 'timeout_seconds': args.timeout,
              'exit_code': exit_code, 'elapsed_seconds': round(time.monotonic() - started, 2),
              'note': 'Exit 0 alone is not evidence of physical traversal; inspect the experiment test report.'}
    target = OUT / (args.action + '-launch-' + stamp + '.json')
    target.write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report, indent=2), flush=True)
    raise SystemExit(1 if timed_out else exit_code)


if __name__ == '__main__':
    main()
