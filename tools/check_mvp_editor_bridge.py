"""Four negative requests against an explicitly enabled Editor bridge.

No valid game/scene command is dispatched. Verifies rejection and preservation.
"""
from pathlib import Path
import datetime
import hashlib
import json
import os
import time
import uuid

from request_mvp_editor import ROOT, FOLDER, STATUS, REQUEST


def main():
    def status():
        return json.loads(STATUS.read_text(encoding='utf-8-sig'))

    initial = status()
    assert initial['enabled'] and initial['runState'] not in ('accepted', 'running', 'stalled')
    assert not REQUEST.exists()
    baseline = json.loads((ROOT / 'docs/everflow_mvp/evidence/resume-20261017/protected-before.json').read_text(encoding='utf-8'))
    before = {p: hashlib.sha256((ROOT / p).read_bytes()).hexdigest() for p in baseline['files']}
    sequence = initial['consumedSequence']
    now = datetime.datetime.now(datetime.timezone.utc)
    session = initial['sessionId']
    wrong_session = ('1' if session[0] != '1' else '2') + session[1:]
    cases = [('wrong session', wrong_session, sequence+1, 'p3-checks', now, sequence, 'Session nonce mismatch'),
             ('expired request', session, sequence+1, 'p3-checks', now-datetime.timedelta(minutes=10), sequence+1, 'older than five minutes'),
             ('unsupported command', session, sequence+2, 'unsupported-check', now, sequence+2, 'not allowlisted'),
             ('sequence replay', session, sequence+2, 'p3-checks', now, sequence+2, 'strictly increase')]
    results = []
    try:
        for label, nonce, seq, command, issued, consumed, reason in cases:
            assert not REQUEST.exists(), 'Pending request preserved; refusing to overwrite.'
            request = {'version': 1, 'sessionId': nonce, 'sequence': seq, 'command': command, 'issuedUtc': issued.isoformat()}
            temporary = FOLDER / ('negative-bridge-' + uuid.uuid4().hex + '.tmp')
            temporary.write_text(json.dumps(request), encoding='utf-8')
            os.rename(temporary, REQUEST)
            deadline = time.monotonic()+10
            observed = None
            while time.monotonic() < deadline:
                current = status()
                if not REQUEST.exists() and current['ackState'] == 'rejected' and reason in current['ackMessage']:
                    observed = current
                    break
                time.sleep(.15)
            passed = (observed is not None and observed['consumedSequence'] == consumed and
                      observed['runSequence'] == initial['runSequence'] and observed['runState'] == initial['runState'])
            results.append({'case': label, 'pass': passed, 'ack': observed['ackMessage'] if observed else 'No matching rejection in ten seconds'})
            assert passed, label
    finally:
        changed = [p for p, digest in before.items() if hashlib.sha256((ROOT / p).read_bytes()).hexdigest() != digest]
        report = {'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  'kind': 'Actual enabled Editor bridge negative requests, no game command dispatched',
                  'checks': results, 'protected_files_changed': changed,
                  'pass': len(results) == 4 and all(r['pass'] for r in results) and not changed}
        stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
        target = ROOT / ('docs/everflow_mvp/evidence/resume-20261017/editor-bridge-negative-' + stamp + '.json')
        target.write_text(json.dumps(report, indent=2), encoding='utf-8')
        print(json.dumps({'report': str(target.relative_to(ROOT)), **report}, indent=2))


if __name__ == '__main__':
    main()
