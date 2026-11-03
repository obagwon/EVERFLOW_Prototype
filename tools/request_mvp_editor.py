"""Send one allowlisted request to an explicitly enabled Editor session.

Does not launch/kill Unity, modify a license, or execute a shell in the Editor.
The request is accepted only by the project's opt-in Local Check Bridge.
"""
from pathlib import Path
import argparse
import datetime
import json
import os
import uuid

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'Temp/mvp-experiment'
STATUS = FOLDER / 'editor-bridge-status.json'
REQUEST = FOLDER / 'editor-bridge-request.json'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['status', 'p3-checks', 'p3-repair-access', 'mvp-create', 'mvp-checks', 'mvp-publish', 'mvp-title-check', 'mvp-build'])
    args = parser.parse_args()
    if not STATUS.is_file():
        raise SystemExit('Bridge unavailable. Enable Local Check Bridge in the existing normal-user Editor first.')
    status = json.loads(STATUS.read_text(encoding='utf-8-sig'))
    if args.command == 'status':
        visible = {k: v for k, v in status.items() if k != 'sessionId'}
        print(json.dumps(visible, indent=2))
        return
    if status.get('version') != 1 or not status.get('enabled'):
        raise SystemExit('Bridge is disabled or incompatible; no request sent.')
    if status.get('implementationRevision') != 2:
        raise SystemExit('The Editor still has the previous bridge assembly. Refresh the existing Editor; no request sent.')
    if args.command.startswith('mvp-') and status.get('commandScope') != 'mvp':
        raise SystemExit('Enable Integration Bridge in the Editor before sending MVP commands; no request sent.')
    if status.get('runState') in ('accepted', 'running', 'stalled'):
        raise SystemExit('A run is in flight/stalled; inspect its evidence before another request.')
    if REQUEST.exists():
        raise SystemExit('An unconsumed request exists; it was preserved, not overwritten.')
    updated = datetime.datetime.fromisoformat(status['updatedUtc'].replace('Z', '+00:00'))
    now = datetime.datetime.now(datetime.timezone.utc)
    if (now - updated).total_seconds() > 120:
        raise SystemExit('Bridge status is stale; the existing Editor may be unavailable. No Editor was launched.')
    request = {'version': 1, 'sessionId': status['sessionId'],
               'sequence': int(status.get('consumedSequence', 0)) + 1,
               'command': args.command, 'issuedUtc': now.isoformat()}
    temporary = FOLDER / ('editor-bridge-request-' + uuid.uuid4().hex + '.tmp')
    temporary.write_text(json.dumps(request), encoding='utf-8')
    # Windows rename refuses to replace an existing destination, so another
    # producer cannot silently replace a complete pending request.
    try:
        os.rename(temporary, REQUEST)
    finally:
        if temporary.exists():
            temporary.unlink()
    print(json.dumps({'request': str(REQUEST.relative_to(ROOT)),
                      'sequence': request['sequence'], 'command': args.command,
                      'state': 'written, not yet accepted or verified'}, indent=2))


if __name__ == '__main__':
    main()
