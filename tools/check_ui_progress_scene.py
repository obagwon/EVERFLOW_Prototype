"""Read-only saved scene checks, separate from Unity play/runtime verification."""
from pathlib import Path
import re, json, hashlib, itertools, math, subprocess
ROOT=Path.cwd();OUT=ROOT/'docs/ui-progress';OUT.mkdir(parents=True,exist_ok=True)
checks=[]
def check(value,message):
    checks.append(('PASS ' if value else 'FAIL ')+message)
    if not value:raise AssertionError(message)
def scene(path):
    text=Path(path).read_text(encoding='utf-8-sig');parts=re.split(r'(?=^--- !u!)',text,flags=re.M)[1:]; blocks={}
    for b in parts:
        id=int(re.match(r'--- !u!\d+ &(\d+)',b)[1]);check(id not in blocks,f'{Path(path).name}: unique object ID {id}') if id in blocks else None;blocks[id]=b
    return text,blocks

def scalar(b,key):return re.search(r'(?m)^  '+key+r': (.*)$',b)[1]
def vector(b,key):return tuple(float(x) for x in re.findall(r'[xyzw]: ([-.\d]+)',scalar(b,key)))
def localref(b,key):return int(re.search(r'fileID: (\d+)',scalar(b,key))[1])
def child_ids(b):return [int(x) for x in re.findall(r'fileID: (\d+)',re.search(r'  m_Children:(.*?)\n  m_Father:',b,re.S)[1])]
def name(b):return re.search(r'(?m)^  m_Name: (.*)',b)[1]
def go_of(b):return localref(b,'m_GameObject')
def guid(path):return re.search(r'(?m)^guid: (\w+)',Path(str(path)+'.meta').read_text())[1]

try:
    title,t=scene('Assets/EverflowMVP/Scenes/Everflow_Title.unity');text,m=scene('Assets/Scenes/Everflow_Map.unity')
    baseline=json.loads((OUT/'map-before-object-hashes.json').read_text(encoding='utf-8'))
    b={int(id):hash for id,hash in baseline['blocks'].items()}
    check(set(b)<=set(m),'Every original map object/component remains present')
    changed=[id for id in b if b[id]!=hashlib.sha256(m[id].encode()).hexdigest()]
    allowed={46391226,2052792844,1230967735,2067115161,1454693199,1361118013,1454693200,1361118012}
    check(set(changed)==allowed,'Only five marker component lists, HUD parent children, P5 arrival pose/size changed in original map objects')
    check(all(b[int(id)]==hashlib.sha256(m[int(id)].encode()).hexdigest() for id in baseline['prefab_blocks']),'Every original prefab instance block, including FBX root transforms, remains byte-identical after newline normalization')
    for path,blocks in [('title',t),('map',m)]:
        refs=[int(x) for p in blocks.values() for x in re.findall(r'\{fileID: (-?\d+)\}',p)]
        missing=sorted(set(x for x in refs if x and x not in blocks))
        check(not missing,f'{path}: every local serialized reference resolves ({len(refs)} refs); missing={missing}')
        for id,p in blocks.items():
            if (p.startswith('--- !u!224 ') or p.startswith('--- !u!4 ')) and '  m_Children:' in p:
                for child in child_ids(p):
                    if '  m_Father:' in blocks[child]: check(localref(blocks[child],'m_Father')==id,f'{path}: parent/child ownership {id} -> {child}')
        check(True,f'{path}: no duplicate file IDs')
    own_meta={guid(p):p for p in Path('Assets').rglob('*.cs') if Path(str(p)+'.meta').exists()}
    required_scripts={g for blocks in [t,m] for p in blocks.values() for g in re.findall(r'm_Script: \{fileID: 11500000, guid: (\w+)',p)}
    hits=subprocess.run(['rg','-l','-g','*.cs.meta','guid: ('+'|'.join(required_scripts)+')','Library/PackageCache'],capture_output=True,text=True,check=True).stdout.splitlines()
    package_meta={re.search(r'(?m)^guid: (\w+)',Path(p).read_text())[1] for p in hits}
    for path,blocks in [('title',t),('map',m)]:
        scripts={g for p in blocks.values() for g in re.findall(r'm_Script: \{fileID: 11500000, guid: (\w+)',p)}
        check(all(g in own_meta or g in package_meta for g in scripts),f'{path}: every MonoBehaviour script GUID resolves')
    icons=[Path('Assets/icon/'+s+'_icon.png') for s in ['bloom','freeze','pulse']]
    assets=icons+[Path('Assets/logo/EVERFLOW_main.png'),Path('Assets/logo/EVERFLOW_start_button.png')]
    for path in assets:
        meta=Path(str(path)+'.meta').read_text();g=guid(path)
        check(all(re.search(r'(?m)^\s*'+key+r': '+str(value)+r'$',meta) for key,value in [('textureType',8),('spriteMode',1),('enableMipMap',0),('alphaIsTransparency',1),('nPOTScale',0)]),str(path)+': single sprite, alpha, no mipmaps or NPOT distortion')
        check(g in (text if path in icons else title),str(path)+': directly referenced by saved build scene')
    check(title.count('m_Name: Main Composition\n')==1 and 'm_AspectMode: 3' in title,'One FitInParent main image composition')
    check(child_ids(t[1325455071])==[2067178196,9000000002,877238436],'Title background < main image/button composition < existing version label order')
    check(scalar(t[247730187],'m_IsActive')=='0','Old button text hidden; no repeated start text')
    check(localref(t[735874436],'m_TargetGraphic')==9000000007 and 'm_MethodName: StartGame' in t[735874436],'Original actual button callback preserved, feedback targets artwork')
    check(scalar(t[2067178197],'m_RaycastTarget')=='0' and scalar(t[1544790734],'m_RaycastTarget')=='0' and scalar(t[9000000007],'m_RaycastTarget')=='0','Background/main/artwork do not consume button input')
    check(vector(t[9000000005],'m_AnchorMin')==(.5,.5),'Button artwork centered inside hit rectangle')
    check(text.count('m_Name: Ability HUD\n')==1 and title.count('EFMapAbilityHUD')==0,'Exactly one saved gameplay HUD; none in title')
    check(text.count('Assembly-CSharp::EverflowMap.EFMapPassage')==5,'Exactly five passage components')
    for id in [x for x in m if x>=9000000000 and x!=9223372036854775807]:
        if 'm_RaycastTarget:' in m[id]:check(scalar(m[id],'m_RaycastTarget')=='0',f'HUD graphic {id} has no raycast target')
    check('m_BlocksRaycasts: 0' in m[9000000010] and 'm_Interactable: 0' in m[9000000010],'HUD group cannot block clicks')
    descriptions=['식물을 성장시킵니다','물을 얼리고 움직임을 멈춥니다','에너지를 전달해 장치를 작동시킵니다']
    check(all(json.dumps(d,ensure_ascii=True) in text for d in descriptions),'All exact Korean description strings serialized')
    world=m[1361118012]
    arrivals=[int(x) for x in re.findall(r'    arrival: \{fileID: (\d+)\}',world)]
    sizes=[tuple(float(x) for x in xyz) for xyz in re.findall(r'    arrivalSize: \{x: (.*?), y: (.*?), z: (.*?)\}',world)]
    centers=[vector(m[id],'m_LocalPosition') for id in arrivals]
    origin=(9.45035,0,-3.14175);relative=[tuple(c-o for c,o in zip(center,origin)) for center in centers]
    def contains(i,p):return all(abs(a-c)<=s/2 for a,c,s in zip(p,relative[i],sizes[i]))
    starts=[(18,1.12,34),(44,4.2,21),(44,8.6,-40),(17,8.6,-42),(-26,11.1,8)]
    approaches=[(26,4.1,23),(44,8.6,-25),(42,8.6,-42),(-19.4,25,-47),(-20.5,11,5)]
    landings=[(44,4.2,21),(44,8.6,-40),(18,8.6,-42),(-26,11.1,8),(-14,13.6,5)]
    for i in range(5):
        check(not contains(i,starts[i]) and not contains(i,approaches[i]),f'P{i+1}: actual spawn/near-side approach is outside the passage volume')
        check(contains(i,landings[i]),f'P{i+1}: authored/previously tested far-side landing lies inside passage volume')
        below=tuple(v-(sizes[i][1]/2+1 if a==1 else 0) for a,v in enumerate(relative[i]))
        check(not contains(i,below),f'P{i+1}: nearby lower-level XZ position rejected by height')
    for i,j in itertools.combinations(range(5),2):
        check(any(abs(centers[i][a]-centers[j][a])>(sizes[i][a]+sizes[j][a])/2 for a in range(3)),f'P{i+1}/P{j+1}: passage volumes do not overlap')
    check(abs((relative[4][0]-sizes[4][0]/2)-(-15.5))<.0001,'Wider P5 volume preserves original near-side boundary at terrain x=-15.5; door is at x=-18.6')
    # Validate UI rectangles using the serialized anchor/size geometry at both requested aspects.
    def layout(blocks,rootrt,width,height,composition=False):
        scale=min(width/1920,height/1080);vw,vh=width/scale,height/scale;rects={rootrt:(0,0,vw,vh)}
        def walk(rt):
            left,bottom,pw,ph=rects[rt]
            for child in child_ids(blocks[rt]):
                q=blocks[child];a=vector(q,'m_AnchorMin');z=vector(q,'m_AnchorMax');pos=vector(q,'m_AnchoredPosition');size=vector(q,'m_SizeDelta');pivot=vector(q,'m_Pivot')
                cw,ch=pw*(z[0]-a[0])+size[0],ph*(z[1]-a[1])+size[1]
                if composition and child==9000000002:cw=min(pw,ph*1672/941);ch=cw*941/1672
                x=left+pw*(a[0]+(z[0]-a[0])*pivot[0])+pos[0]-pivot[0]*cw
                y=bottom+ph*(a[1]+(z[1]-a[1])*pivot[1])+pos[1]-pivot[1]*ch
                rects[child]=(x,y,cw,ch);walk(child)
        walk(rootrt);return rects,(vw,vh)
    for width,height in [(1920,1080),(1920,1200),(1280,720),(1440,900)]:
        rects,(vw,vh)=layout(t,1325455071,width,height,True)
        for rt in [1544790733,735874439,9000000005]:
            x,y,w,h=rects[rt];check(x>=0 and y>=0 and x+w<=vw+.01 and y+h<=vh+.01,f'{width}x{height}: title image/button rect {rt} within screen')
        rects,(vw,vh)=layout(m,9000000007,width,height)
        for rt,(x,y,w,h) in rects.items():
            if rt==9000000007:continue
            check(x>=0 and y>=0 and x+w<=vw+.01 and y+h<=vh+.01,f'{width}x{height}: HUD rect {rt} within screen')
    check(Path('C:/Windows/Fonts/malgun.ttf').is_file(),'Windows release target has Malgun Gothic available for runtime Korean dynamic font')
    check('path: Assets/Scenes/Everflow_Map.unity' in Path('ProjectSettings/EditorBuildSettings.asset').read_text(),'Actual gameplay scene is in saved build settings')
    layout_report=[{'stage':i+1,'world_center':centers[i],'terrain_relative_center':relative[i],'size':sizes[i]} for i in range(6)]
    (OUT/'passage-layout.json').write_text(json.dumps(layout_report,indent=2),encoding='utf-8')
    summary={'kind':'Static saved YAML/reference/geometry validation; no Unity execution or physical play implied','checks':len(checks),'pass':True,'changed_original_map_blocks':changed,'sprite_png_sha256':{str(p).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in assets}}
    (OUT/'static-results.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
    print(json.dumps(summary,indent=2))
finally:
    (OUT/'static-results.txt').write_text('\n'.join(checks)+'\n',encoding='utf-8')
