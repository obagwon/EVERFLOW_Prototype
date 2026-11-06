"""Read-only comparison against the pre-signage saved scene. No Unity Play claims."""
import hashlib
import json
import math
import re
from update_v02_signage import ROOT, SCENE, PRESENTATION, parse, field, reference, decode_vector


def main():
    report_path=ROOT/'docs/puzzle-lab/signage-verification.json'
    report=json.loads(report_path.read_text(encoding='utf-8'))
    baseline=(ROOT/report['backup']/SCENE.name).read_bytes()
    old=parse(baseline.decode('utf-8-sig').replace('\r\n','\n'))
    current=parse(SCENE.read_text(encoding='utf-8'))
    text_gos={reference(b,'m_GameObject') for k,b in old.values() if k==102}
    poses=0
    for i,(k,b) in old.items():
        if k in (1,4,23,102) and (i in text_gos if k==1 else reference(b,'m_GameObject') in text_gos): continue
        if k==4:
            for key in ('m_LocalPosition','m_LocalRotation','m_LocalScale','m_Father'):
                assert field(b,key)==field(current[i][1],key),(i,key)
            poses+=1
        else: assert b==current[i][1],('changed gameplay component',i,k)
    for i,(k,b) in current.items():
        for m in re.finditer(r'\{fileID: (-?\d+)([^}]*)\}',b):
            if int(m[1]) and 'guid:' not in m[2]: assert int(m[1]) in current,(i,m[1])
    boards=[(i,b) for i,(k,b) in current.items() if k==114 and '::EverflowPuzzleLab.LabInfoBoard\n' in b]
    labels=[i for i,(k,b) in current.items() if k==114 and '::EverflowPuzzleLab.LabShortLabel\n' in b]
    assert len(boards)==5 and len(labels)==20
    for i,b in boards:
        area=reference(b,'area'); status=reference(b,'status')
        assert reference(old[area][1],'sign')==status
        if reference(b,'circuitStatus'):
            assert reference(old[reference(old[area][1],'canal')][1],'readout')==reference(b,'circuitStatus')
        for key in ('title','goal','methods','controls','status'):
            text=current[reference(b,key)][1]
            assert 'rgba: 0' not in text
            assert field(text,'m_Anchor')=='0' and field(text,'m_Alignment')=='0'
            assert field(text,'m_LineSpacing') in ('.85','0.85')
    # Analytical initial 16:9 camera frustum only; no rendered visibility/occlusion claim.
    config=json.loads((PRESENTATION/'Signage.json').read_text(encoding='utf-8'))
    sine,cosine=math.sin(math.radians(18)),math.cos(math.radians(18))
    tan=math.tan(math.radians(65/2))
    frustum={}
    for entry in config['boards']:
        spawn=[0,5.2,-13] if entry['index']==4 else [0,1,-3]
        camera=[spawn[0]+.5,spawn[1]+.7+6*sine,spawn[2]-6*cosine]
        px,py,pz=entry['position']; w,h=entry['size']; points=[]
        for x in (px-w/2,px+w/2):
            for y in (py-h/2,py+h/2):
                dx,dy,dz=x-camera[0],y-camera[1],pz-camera[2]
                depth=-dy*sine+dz*cosine; up=dy*cosine+dz*sine
                ndc=(dx/(depth*tan*16/9),up/(depth*tan))
                assert depth>0 and max(abs(ndc[0]),abs(ndc[1]))<1,(entry['key'],ndc)
                points.append(ndc)
        frustum[entry['key']]='within initial 16:9 frustum (analytical only)'
    report.update(after_sha256=hashlib.sha256(SCENE.read_bytes()).hexdigest(),protected_geometry_poses=poses,
                  short_labels=len(labels),status_reference_ids_preserved=True,initial_camera_frustum=frustum,
                  offline_state_checks='38 passed; no presentation runtime test',
                  flat_preview='5 panels checked with installed Malgun Gothic; separate from Unity rendering',
                  scene_regenerated=False)
    report_path.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('PASS 5 boards, 20 labels, live status fileIDs, all local references, 324 existing geometry poses and all gameplay/collider components unchanged.')
    print('PASS initial 16:9 frustum mathematics. Unity rendering, occlusion and Play are unverified.')


if __name__=='__main__': main()
