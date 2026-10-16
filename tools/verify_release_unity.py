"""Run the read-only Unity verification entry point, with a bounded timeout."""
from pathlib import Path
import json
import subprocess
import time

root = Path(__file__).resolve().parents[1]
out = root / 'docs/release-integration'
command = [
    'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe',
    '-batchmode', '-nographics', '-quit', '-projectPath', str(root),
    '-executeMethod', 'EverflowMVP.Editor.EFReleaseIntegrationVerification.Run',
    '-logFile', str(out / 'verification-unity.log'),
]
started = time.monotonic()
process = subprocess.Popen(command, cwd=root, creationflags=subprocess.CREATE_NO_WINDOW)
print('Started verification Unity process PID', process.pid, flush=True)
timed_out = False
try:
    exit_code = process.wait(timeout=90)
except subprocess.TimeoutExpired:
    timed_out = True
    process.kill()  # Only the process created by this verifier; never the existing Hub/licensing client.
    exit_code = process.wait()
report = {'command': command, 'pid': process.pid, 'timeout_seconds': 90,
          'timed_out': timed_out, 'exit_code': exit_code,
          'elapsed_seconds': round(time.monotonic() - started, 2)}
(out / 'verification-unity-launch.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2), flush=True)
raise SystemExit(1 if timed_out else exit_code)
