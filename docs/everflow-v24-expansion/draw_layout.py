from pathlib import Path
import json
import math
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(ROOT / "tools"))
from inspect_v2_fbx import inspect, triangles, heights

layout = json.loads((HERE / "layout.json").read_text(encoding="utf-8"))
points = layout["points"]
_, meshes = inspect()
terrain = next(m for m in meshes if m["name"] == "T_Terrain")

INK = "#192a3b"
MUTED = "#556778"
GRID = "#dce6ed"
BG = "#f2f6f9"
WHITE = "#ffffff"
BLUE = "#167895"
WATER = "#1479ac"
ORANGE = "#a74815"
COLORS = {"walk": "#566577", "required": ORANGE, "shortcut": "#6934a6",
          "optional": "#9b5e17", "return": "#34764e"}

def font(size, bold=False):
    return ImageFont.truetype(f"C:/Windows/Fonts/malgun{'bd' if bold else ''}.ttf", size)

def label(d, xy, value, size=20, fill=INK, bold=False, anchor=None):
    d.text(xy, value, fill=fill, font=font(size, bold), anchor=anchor)

def heading(d, index, title, subtitle):
    label(d, (52, 34), f"EVERFLOW   /   v2.4   /   {index}", 20, BLUE, True)
    label(d, (52, 67), title, 46, INK, True)
    label(d, (54, 131), subtitle, 20, MUTED)

def polyline(d, values, color, width, dashed=False):
    if not dashed:
        d.line(values, fill=color, width=width, joint="curve")
        return
    for (x0, y0), (x1, y1) in zip(values, values[1:]):
        length = math.hypot(x1 - x0, y1 - y0)
        for start in range(0, int(length), 14):
            end = min(length, start+8)
            d.line([(x0+(x1-x0)*start/length, y0+(y1-y0)*start/length),
                    (x0+(x1-x0)*end/length, y0+(y1-y0)*end/length)], fill=color, width=width)

def marker(d, xy, code, offset, color=INK):
    x, y = xy
    dx, dy = offset
    tx, ty = x+dx, y+dy
    d.line([(x,y),(tx,ty)], fill=color, width=2)
    d.ellipse((x-5,y-5,x+5,y+5), fill=color, outline=WHITE, width=1)
    f = font(17, True)
    w = d.textlength(code, font=f)
    d.rounded_rectangle((tx-w/2-7,ty-13,tx+w/2+7,ty+13), 5, fill=WHITE, outline=color, width=2)
    label(d,(tx,ty-1),code,17,color,True,"mm")

def plan():
    im = Image.new("RGB", (1560, 1230), BG)
    d = ImageDraw.Draw(im)
    heading(d, "01", "기존 지형 위 확장 평면도", "Unity 월드 XZ   /   +X 오른쪽   /   -Z 위쪽   /   격자 10m")
    x0,z0,s = -110,-95,8.6
    def xy(x,z):
        return (95+(x-x0)*s, 208+(z-z0)*s)
    def project(p):
        return xy(p[0], p[2])
    start=xy(-110,-95); end=xy(40,15)
    d.rectangle((*start,*end),fill=WHITE,outline="#a9bfcd",width=2)
    d.rectangle((*xy(0,-72),*xy(36,-21)),fill="#dbedf4")
    for tri in sorted(triangles(terrain),key=lambda t:sum(p[1] for p in t)):
        high=sum(p[1] for p in tri)/3
        if high < -27:
            continue
        level=max(0, min(1, (high+27)/48))
        color=tuple(round(a+(b-a)*level) for a,b in zip((239,242,241),(189,205,204)))
        d.polygon([project(p) for p in tri], fill=color)
    for x in range(-100,41,10):
        d.line([xy(x,-95),xy(x,15)],fill=GRID,width=1)
        label(d,(xy(x,0)[0],193),str(x),15,MUTED,False,"mm")
    for z in range(-90,11,10):
        d.line([xy(-110,z),xy(40,z)],fill=GRID,width=1)
        label(d,(75,xy(0,z)[1]),str(z),15,MUTED,False,"rm")
    for (a,b),line in [(([-102,-92],[12,12]),"#7d909c"), (([0,-72],[36,-21]),BLUE)]:
        x,z=a; u,v=b
        p=[xy(x,z),xy(u,z),xy(u,v),xy(x,v),xy(x,z)]
        polyline(d,p,line,2,True)
    label(d,xy(17,-69), "동쪽 확장",20,BLUE,True,"mm")
    water=[project(p) for p in layout["hydrology"]["waterLevels"]]
    polyline(d,water,WHITE,7)
    polyline(d,water,WATER,4)
    for path in layout["paths"]:
        vals=[project(points[p] if isinstance(p,str) else p) for p in path["points"]]
        polyline(d,vals,WHITE,9)
        polyline(d,vals,COLORS[path["type"]],5, path["type"] in ("shortcut","optional","return"))
    offsets = {"S":(-36,-10),"A1":(-40,-22),"A2":(-12,27),"A3":(7,24),"J":(-1,-23),
               "B":(3,-22),"C":(1,23),"D":(-22,-5),"E":(-29,-14),"G":(18,-12),
               "H":(-18,-15),"W":(-24,14),"P5":(10,-22),"P6":(-25,-4),"P7":(24,-6),
               "P8":(24,4),"P9":(20,15)}
    for code,offset in offsets.items():
        marker(d, project(points[code]), code.removeprefix("P"), offset, INK)
    for code in ["B_orb","C_orb","D_orb","E_orb"]:
        x,y=project(points[code])
        d.ellipse((x-5,y-5,x+5,y+5),fill="#dcac22",outline=WHITE,width=1)
    legend=[("일반 경로",COLORS["walk"]),("능력 횡단",ORANGE),("지름길",COLORS["shortcut"]),
            ("회복 후 복귀",COLORS["return"]),("숨은 장소",COLORS["optional"]),("본류",WATER),("구체","#dcac22")]
    for i,(name,c) in enumerate(legend):
        x=115+i*166
        d.line([(x,1192),(x+24,1192)],fill=c,width=5)
        label(d,(x+32,1192),name,16,INK,False,"lm")
    d.line([(1175,1010),(1347,1010)],fill=INK,width=3)
    for x,value in [(1175,"0"),(1261,"10"),(1347,"20m")]:
        d.line([(x,1004),(x,1016)],fill=INK,width=2)
        label(d,(x,1030),value,15,INK,False,"mm")
    im.save(HERE / "plan.png")

def section():
    im=Image.new("RGB",(1560,1120),BG)
    d=ImageDraw.Draw(im)
    heading(d,"02","시야와 물길의 단면","높이는 Unity 월드 Y   /   본류 가로축은 수로 중심선을 따라 잰 누적 수평 거리")
    def chart(top,bottom,ymin,ymax,length,ystep,tick):
        left,right=120,1480
        x=lambda m:left+(right-left)*m/length
        y=lambda n:bottom-(bottom-top)*(n-ymin)/(ymax-ymin)
        d.rectangle((left,top,right,bottom),fill=WHITE,outline="#a9bfcd",width=2)
        for v in range(math.ceil(ymin/ystep)*ystep,math.floor(ymax/ystep)*ystep+1,ystep):
            d.line([(left,y(v)),(right,y(v))],fill=GRID,width=1)
            label(d,(105,y(v)),f"{v:+d}",15,MUTED,False,"rm")
        for v in range(0,int(length)+1,tick):
            d.line([(x(v),top),(x(v),bottom)],fill=GRID,width=1)
            label(d,(x(v),bottom+17),f"{v}m",14,MUTED,False,"mm")
        return x,y
    label(d,(52,180),"A–A′   S 전망대에서 G 풍차까지",24,INK,True)
    a,b=points["S"],points["G_hub"]
    length=math.hypot(b[0]-a[0],b[2]-a[2])
    x,y=chart(222,478,-5,33,60,5,10)
    samples=[]
    for step in range(81):
        t=step/80
        tx,tz=a[0]+(b[0]-a[0])*t,a[2]+(b[2]-a[2])*t
        ground=max((h for h,n in heights(terrain,tx,tz) if n>.05),default=-5)
        samples.append((x(length*t),y(max(-5,ground))))
    d.polygon([(x(0),y(-5)),*samples,(x(length),y(-5))],fill="#cedbdc")
    polyline(d,samples,"#7c969a",3)
    polyline(d,[(x(0),y(9.6)),(x(length),y(26.5))],BLUE,4,True)
    d.line([(x(0),y(8)),(x(5),y(8))],fill=ORANGE,width=5)
    d.line([(x(length),y(15.1)),(x(length),y(26.5))],fill=ORANGE,width=5)
    marker(d,(x(0),y(8)),"S  +8.0",(49,29))
    marker(d,(x(length),y(26.5)),"풍차 축  +26.5",(-73,-18))
    marker(d,(x(length),y(15.1)),"G  +15.1",(-69,26))
    label(d,(400,258),"기존 T_Terrain 단면",17,MUTED)
    label(d,(751,258),"S 눈높이에서 G 축을 보는 방향",17,BLUE)
    label(d,(52,529),"B–B′   H 수원에서 W 물레방아까지",24,INK,True)
    water=layout["hydrology"]["waterLevels"]
    run=[0]
    for a,b in zip(water,water[1:]):
        run.append(run[-1]+math.hypot(b[0]-a[0],b[2]-a[2]))
    x,y=chart(574,855,-20,25,math.ceil(run[-1]/10)*10,5,10)
    profile=[(x(k),y(p[1])) for k,p in zip(run,water)]
    polyline(d,profile,WATER,5)
    for i,spec in {0:("H  +20.55",68,-9),1:("G 공급  +15.23",20,37),
                   2:("본류 출구  +14.4",72,-37),5:("신설 수로  +6.0",3,-28),
                   7:("협곡 낙수  -11.9",23,29),9:("W 유입  -12.3",-33,-24),
                   10:("W 하부 수면  -17.82",-83,23)}.items():
        code,dx,dy=spec
        marker(d,profile[i],code,(dx,dy),WATER)
    label(d,(52,925),"얼음 수조 B   /   별도 지류",24,INK,True)
    h=layout["hydrology"]["B"]
    for i,(name,key) in enumerate([("바닥","bottomY"),("누수구","breachSillY"),("초기 수위","initialWaterY"),
                                   ("정원 수로","spillSillY"),("봉합 후 수위","solvedWaterY"),("얼음 상단","iceTopY"),("벽 상단","wallTopY")]):
        xx=53+i*210
        d.rounded_rectangle((xx,971,xx+192,1067),9,fill=WHITE,outline=GRID,width=2)
        label(d,(xx+16,987),name,16,MUTED)
        label(d,(xx+16,1016),f"{h[key]:+.2f}m",25,BLUE,True)
    im.save(HERE / "sections.png")

if __name__ == "__main__":
    plan()
    section()

