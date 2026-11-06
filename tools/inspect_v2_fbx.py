"""Read-only binary FBX geometry inspection; coordinates are analytical, not Unity physics.

Supports the scalar/array records used by the team's Blender FBX export.
"""
from pathlib import Path
import struct
import zlib
import json
import math

ROOT = Path(__file__).resolve().parents[1]
MODEL = ROOT / 'Assets/EF_Handoff_v2/EF_Map_v2.fbx'


def read_fbx(path=MODEL):
    data = path.read_bytes()
    assert data.startswith(b'Kaydara FBX Binary  \x00\x1a\x00')
    version = struct.unpack_from('<I', data, 23)[0]
    wide = version >= 7500
    header = '<QQQB' if wide else '<IIIB'
    size = struct.calcsize(header)

    def node(offset):
        end, count, length, namesize = struct.unpack_from(header, data, offset)
        if not end:
            return None, offset + size
        offset += size
        name = data[offset:offset+namesize].decode(); offset += namesize
        props = []
        for _ in range(count):
            kind = chr(data[offset]); offset += 1
            formats = {'Y': 'h', 'C': '?', 'I': 'i', 'F': 'f', 'D': 'd', 'L': 'q'}
            if kind in formats:
                fmt = '<' + formats[kind]
                value = struct.unpack_from(fmt, data, offset)[0]; offset += struct.calcsize(fmt)
            elif kind in 'SR':
                n = struct.unpack_from('<I', data, offset)[0]; offset += 4
                value = data[offset:offset+n]; offset += n
                if kind == 'S': value = value.decode('utf-8', 'replace')
            elif kind in 'fdlibc':
                n, encoded, nbytes = struct.unpack_from('<III', data, offset); offset += 12
                raw = data[offset:offset+nbytes]; offset += nbytes
                if encoded: raw = zlib.decompress(raw)
                value = struct.unpack('<' + str(n) + {'f':'f','d':'d','l':'q','i':'i','b':'?','c':'b'}[kind], raw)
            else:
                raise ValueError(kind)
            props.append(value)
        children = []
        while offset < end - size:
            child, offset = node(offset)
            if child is None: break
            children.append(child)
        return (name, props, children), end

    nodes = []; offset = 27
    while offset < len(data):
        n, offset = node(offset)
        if n is None: break
        nodes.append(n)
    return nodes


def properties(node):
    return {p[1][0]: p[1][4:] for c in node[2] if c[0]=='Properties70' for p in c[2] if p[0]=='P'}


def transform_point(v, p):
    order=('xyz','xzy','yzx','yxz','zxy','zyx')[p.get('RotationOrder',[0])[0]]
    for key in ('PreRotation','PostRotation','GeometricTranslation','GeometricRotation'):
        assert all(abs(x)<1e-6 for x in p.get(key,[0,0,0])), key
    assert p.get('GeometricScaling',[1,1,1]) == [1,1,1]
    sp=p.get('ScalingPivot',[0,0,0]); rp=p.get('RotationPivot',[0,0,0])
    so=p.get('ScalingOffset',[0,0,0]); ro=p.get('RotationOffset',[0,0,0])
    scale=p.get('Lcl Scaling',[1,1,1]); t=p.get('Lcl Translation',[0,0,0])
    v=[(v[i]-sp[i])*scale[i]+sp[i]+so[i]-rp[i] for i in range(3)]
    rotation=p.get('Lcl Rotation',[0,0,0])
    for character in order:
        axis='xyz'.index(character); angle=rotation[axis]
        a,b=(axis+1)%3,(axis+2)%3; s,c=math.sin(math.radians(angle)),math.cos(math.radians(angle))
        v[a],v[b]=c*v[a]-s*v[b],s*v[a]+c*v[b]
    return tuple(v[i]+rp[i]+ro[i]+t[i] for i in range(3))


def inspect():
    nodes = read_fbx()
    settings = properties(next(n for n in nodes if n[0]=='GlobalSettings'))
    objects = next(n for n in nodes if n[0]=='Objects')[2]
    connections = [n[1] for n in next(n for n in nodes if n[0]=='Connections')[2] if n[0]=='C']
    models = {n[1][0]: n for n in objects if n[0]=='Model'}
    geometries = {n[1][0]: n for n in objects if n[0]=='Geometry'}
    parent = {p[1]:p[2] for p in connections if p[0]=='OO'}
    rows = []
    # Export uses Y-up centimetres. Apply FBX pivots before handedness conversion.
    unit = settings['UnitScaleFactor'][0] / 100
    for gid,g in geometries.items():
        mid = parent[gid]; model = models[mid]; ps = properties(model)
        vertices = next(c[1][0] for c in g[2] if c[0]=='Vertices')
        points = list(zip(vertices[0::3],vertices[1::3],vertices[2::3]))
        chain=[]; at=mid
        while at in models:
            chain.append(models[at]); at=parent.get(at,0)
        for m in chain:
            p=properties(m)
            points=[transform_point(v,p) for v in points]
        # Unity converts FBX right-handed X to left-handed X.
        points=[(-v[0]*unit,v[1]*unit,v[2]*unit) for v in points]
        name=model[1][1].split('\x00')[0]
        rows.append(dict(name=name,model_id=mid,geometry_id=gid,
                         path='/'.join(m[1][1].split('\x00')[0] for m in reversed(chain)),
                         min=[min(v[i] for v in points) for i in range(3)],
                         max=[max(v[i] for v in points) for i in range(3)],
                         vertices=len(points),properties=ps,points=points,
                         indices=next(c[1][0] for c in g[2] if c[0]=='PolygonVertexIndex')))
    return settings, rows


def triangles(row):
    polygon=[]
    for i in row['indices']:
        polygon.append(-i-1 if i<0 else i)
        if i<0:
            # Reverse winding after X reflection. Export is triangulated/convex quads.
            for k in range(1,len(polygon)-1):
                yield tuple(row['points'][j] for j in (polygon[0],polygon[k+1],polygon[k]))
            polygon=[]


def heights(row,x,z):
    result=[]
    if not (row['min'][0]-1e-5<=x<=row['max'][0]+1e-5 and row['min'][2]-1e-5<=z<=row['max'][2]+1e-5): return result
    for a,b,c in triangles(row):
        d=(b[2]-c[2])*(a[0]-c[0])+(c[0]-b[0])*(a[2]-c[2])
        if abs(d)<1e-9: continue
        u=((b[2]-c[2])*(x-c[0])+(c[0]-b[0])*(z-c[2]))/d
        v=((c[2]-a[2])*(x-c[0])+(a[0]-c[0])*(z-c[2]))/d
        if u>=-1e-6 and v>=-1e-6 and u+v<=1+1e-6:
            ba=[b[i]-a[i] for i in range(3)]; ca=[c[i]-a[i] for i in range(3)]
            normal=[ba[1]*ca[2]-ba[2]*ca[1],ba[2]*ca[0]-ba[0]*ca[2],ba[0]*ca[1]-ba[1]*ca[0]]
            length=math.sqrt(sum(n*n for n in normal))
            result.append((u*a[1]+v*b[1]+(1-u-v)*c[1],normal[1]/length))
    return result


if __name__=='__main__':
    settings,rows=inspect()
    print(json.dumps(settings))
    for r in rows:
        print(r['name'], 'min',r['min'],'max',r['max'],'vertices',r['vertices'])
