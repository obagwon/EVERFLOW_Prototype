"""Local analysis only; never changes source scenes, models or importers."""
from pathlib import Path
import csv, json, hashlib, struct, zlib

out = Path('docs/map-integration')
rows = list(csv.DictReader((out / 'terrain-height.csv').open()))
x = [float(r['x']) for r in rows]
z = [float(r['z']) for r in rows]
y = [float(r['y']) for r in rows]
width, height, scale = 680, 610, 5
pixels=bytearray([30,35,45])*(width*height)
def dot(a,b,color,size=5):
    px,pz=int((a+60)*scale),int((50-b)*scale)
    for dz in range(size):
        for dx in range(size):
            k=((pz+dz)*width+px+dx)*3
            if 0 <= px+dx < width and 0 <= pz+dz < height: pixels[k:k+3]=bytes(color)
for a,b,c in zip(x,z,y):
    v=max(0,min(1,(c+3)/29))
    dot(a,b,(int(50+190*v),int(95+140*v),int(80+120*v)))
points = {'flower': (18,32), 'vine': (31,23), 'debris1': (44,11), 'debris3': (44,1),
          'slate':(44,-31), 'bridge':(28.7,-42), 'leaf':(-19.4,-47), 'slideEnd':(-24,-6),
          'door':(-18.6,5), 'root5':(-19.3,8.6), 'wheel':(0,0), 'source6':(2.7,-23.4)}
for label,(a,b) in points.items():
    dot(a,b,(255,40,20),7)
    nearby=[(float(r['x'])-a)**2+(float(r['z'])-b)**2 for r in rows]
    k=min(range(len(rows)),key=lambda i:nearby[i])
    print(label, rows[k])
def chunk(t,d): return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
raw=b''.join(b'\x00'+pixels[i*width*3:(i+1)*width*3] for i in range(height))
(out/'terrain-height.png').write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',width,height,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b''))
baseline = out / 'preserved-assets-sha256.json'
paths=list(Path('Assets/map_ver1').rglob('*'))
paths += [Path(p) for p in ['Assets/Scenes/Everflow_Map.unity','Assets/Scenes/Everflow_Map.unity.meta','Assets/Scenes/Everflow_World_Graybox.unity','Assets/Scenes/Everflow_World_Graybox.unity.meta','Assets/EverflowMVP/Scenes/Everflow_6Puzzles.unity','Assets/EverflowMVP/Scenes/Everflow_6Puzzles.unity.meta']]
if not baseline.exists():
    baseline.write_text(json.dumps({str(p).replace('\\','/'): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths if p.is_file()},indent=2))
grid={(int(float(r['x'])),int(float(r['z']))):float(r['y']) for r in rows}
print('Height sections: x=-40..60 step2, z=44..-64 step2; . valley, 0 start, 3 ledge, 7 P2/P3, A P5, F P6, H ramp, T P4')
print('    '+''.join(str(abs(a)//10%10) for a in range(-40,61,2)))
print('    '+''.join(str(abs(a)%10) for a in range(-40,61,2)))
for b in range(44,-65,-2):
    def char(a,b):
        v=grid.get((a,b),-99)
        if v < -90: return ' '
        if v < -1: return '.'
        if v < 2: return '0'
        if v < 5: return '3'
        if v < 9: return '7'
        if v < 13: return 'A'
        if v < 17: return 'F'
        if v < 23: return 'H'
        return 'T'
    print(f'{b:3} '+''.join(char(a,b) for a in range(-40,61,2)))
