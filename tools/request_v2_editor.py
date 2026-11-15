"""Send fixed, expiring-session Unity test commands to the already-open Editor."""
from pathlib import Path
import argparse
import datetime
import json
import os
import subprocess
import uuid

ROOT=Path(__file__).resolve().parents[1]
FOLDER=ROOT/'Temp/v2-puzzle-bridge'

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command',choices=['grant','launch','status','open','forced','input','route','alternate','exitcheck','platforms','play','stop','refresh','inspect','screenshot','water-audit','water-build','water-integrate','water-test','water-check','water-verify','visual-before','visual-after','guidance-check','presentation-setup','presentation-check'])
    args=parser.parse_args();FOLDER.mkdir(parents=True,exist_ok=True)
    if args.command in ('grant','launch'):
        (FOLDER/'grant.json').write_text(json.dumps({'expiresUtc':(datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(hours=2)).isoformat(),'scope':'User-requested V2 puzzle Play/tests only; no shell/license/security changes.'}),encoding='utf-8')
        print('Granted fixed V2 Play/test commands for two hours in this project. Existing Editor must import EFV2EditorBridge.cs.')
        if args.command=='grant':return
        from run_mvp_experiment_unity import inspect_launch_context,launch_blockers,UNITY
        context=inspect_launch_context();blockers=launch_blockers(context)
        if blockers:
            print('\n'.join(blockers))
            raise SystemExit('No new Editor launched. In the existing normal-user Editor press Ctrl+R once.')
        if (ROOT/'Temp/UnityLockfile').exists():raise SystemExit('Project lock preserved. Use the existing Editor, Ctrl+R.')
        subprocess.Popen([str(UNITY),'-projectPath',str(ROOT),'-executeMethod','EverflowMap.Editor.EFV2PuzzleBuild.Open'],cwd=ROOT,creationflags=subprocess.CREATE_NO_WINDOW)
        print('Started the project in your normal Windows user session. No license/security settings changed.');return
    file=FOLDER/'status.json'
    if not file.exists():raise SystemExit('Existing Editor has not loaded V2 bridge yet. Refresh Unity once (Ctrl+R).')
    status=json.loads(file.read_text(encoding='utf-8-sig'))
    if args.command=='status':print(json.dumps(status,ensure_ascii=False,indent=2));return
    now=datetime.datetime.now(datetime.timezone.utc)
    updated=datetime.datetime.fromisoformat(status['updatedUtc'].replace('Z','+00:00'))
    if not status['enabled'] or (now-updated).total_seconds()>30:raise SystemExit('Bridge disabled/stale; existing Editor needs a refresh.')
    if (FOLDER/'request.json').exists():raise SystemExit('Pending request preserved; wait for consumption.')
    if status['outcome']=='running' and args.command!='stop':raise SystemExit('A runtime test is running. Wait for its report.')
    request={'session':status['session'],'id':uuid.uuid4().hex,'command':args.command,'issuedUtc':now.isoformat()}
    temporary=FOLDER/('request-'+request['id']+'.tmp');temporary.write_text(json.dumps(request),encoding='utf-8')
    os.rename(temporary,FOLDER/'request.json')
    print(json.dumps({'id':request['id'],'command':args.command,'status':'queued, not a test result'}))

if __name__=='__main__':main()
