"""Analytical FBX/support checks, explicitly not Unity collision or traversal."""
from pathlib import Path
import json
import math
import re
import sys
sys.path.insert(0,str(Path(__file__).parent))
from inspect_v2_fbx import inspect, heights

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'docs/v2-puzzle-validation'
SOURCE=ROOT/'Assets/EverflowMap/Runtime/EFV2PuzzleAssembly.cs'

def vector(text):return tuple(float(s.strip().removesuffix('f')) for s in text.split(','))
def lerp(a,b,t):return tuple(x+(y-x)*t for x,y in zip(a,b))

def main():
    _,rows=inspect()
    byname={r['name']:r for r in rows}
    source=SOURCE.read_text(encoding='utf-8')
    scene=(ROOT/'Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity').read_text(encoding='utf-8')
    solid=re.search(r'  solidMeshNames:\n(.*?)  fallHeight:',scene,re.S)[1]
    names=set(re.findall(r'  - (.+)',solid))
    moving={'PH_A_Leaf01','PH_A_Leaf02','PH_A_Tut_MovingPad','PH_A_Tut_WitheredPad','PH_B_MovingStone','PH_C_LeafPlatform','PH_D2_Leaf','PH_G_StoneDoor','PH_D4_Gate','PH_D5_BypassDoor','PH_D5_MovingChannel'}
    solids=[r for r in rows if r['name'] in names-moving]
    required=set(re.findall(r'(?:Mesh|Bounds|Carrier|Rotor)\("([^\"]+)"',source))
    required.update(re.findall(r'"((?:PH_|W_)[A-Za-z0-9_]+)"',source))
    assert required<=set(byname),sorted(required-set(byname))
    routes=[]
    for name,aa,bb,width,material in re.findall(r'Steps\("([^\"]+)",new Vector3\(([^)]+)\),new Vector3\(([^)]+)\),([\d.]+)f?,(wood|stone)',source):
        a,b=vector(aa),vector(bb); width=float(width)
        distance=math.hypot(b[0]-a[0],b[2]-a[2]);rise=abs(b[1]-a[1])
        count=max(2,math.ceil(max(rise/.28,distance/.65)))
        result={'name':name,'from':a,'to':b,'width':width,'steps':count,'rise_per_step':rise/count,'run_per_step':distance/count,'embedded_samples':[]}
        # The final garden uses a physically verified 1.8m walkway; do not impose the earlier 2m draft everywhere.
        assert width>=1.6 and rise/count<=.281
        for i in range(count):
            p=lerp(a,b,(i+.5)/count)
            # Horizontal upward-facing surfaces between feet+.55 and head mean an analytically embedded stair.
            for r in solids:
                for y,normal in heights(r,p[0],p[2]):
                    if normal>.65 and p[1]+.55<y<p[1]+2:
                        result['embedded_samples'].append({'step':i,'surface':p,'blocker':r['name'],'blocker_top':round(y,3)})
        routes.append(result)
    # Existing exported stair geometry: max individual riser, not playability.
    stairs={}
    for prefix in ('TWR_Flight1_Step','TWR_Flight2_Step','TWR_Flight3_Step','TWR_Flight4_Step','TWR_Flight5_Step','TWR_Flight6_Step','D1_Stairs_Step','D3_DockStairs_Step'):
        steps=sorted([r for r in rows if r['name'].startswith(prefix)],key=lambda r:r['name'])
        riser=max(abs(a['max'][1]-b['max'][1]) for a,b in zip(steps,steps[1:]))
        assert riser<=.3
        stairs[prefix]={'steps':len(steps),'max_riser':riser}
    report={'kind':'ANALYTICAL only: FBX transform/height samples plus generated staircase formulas. NOT imported Unity physics, camera or Play.',
        'required_models':len(required),'missing_models':[], 'routes':routes,'existing_stairs':stairs,
        'embedded_samples':sum(len(r['embedded_samples']) for r in routes),'unity_play':False}
    (OUT/'geometry-checks.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print('Required imported model names:',len(required),'all found')
    for r in routes:print(r['name'],r['steps'],'steps',round(r['rise_per_step'],3),'m rise; embedded samples',len(r['embedded_samples']))
    print('Existing exported stair risers:',len(stairs),'groups pass; findings',report['embedded_samples'])

if __name__=='__main__':main()
