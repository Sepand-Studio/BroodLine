"""Offline editable Vetch mesh study. No runtime generation or image-to-mesh claims.

Writes a skinned, vertex-material mesh interchange file for Unity's candidate
importer. Coordinates are Unity model space (+X forward, Y up). Run with Python 3.
install_runtime.py copies this source to the game's Resources path.
"""
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'client/Assets/Frontier/ProductionCandidates/Vetch'
TAU = math.tau


def add(a, b): return tuple(x+y for x, y in zip(a, b))
def mul(a, s): return tuple(x*s for x in a)
def unit(v):
    length = math.sqrt(sum(x*x for x in v))
    return mul(v, 1/length) if length else (0, 1, 0)
def cross(a, b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def color(h): return [int(h[i:i+2], 16)/255 for i in (0, 2, 4)] + [1]
def vec(v): return dict(zip(('x', 'y', 'z'), (round(x, 6) for x in v)))


class Mesh:
    def __init__(self, identity, kind):
        self.data = dict(version=1, id=identity, kind=kind, vertices=[], normals=[],
                         colors=[], polish=[], bone0=[], bone1=[], blend=[], triangles=[])

    def vertex(self, p, n, c, polish, bone=0, other=0, blend=0):
        d = self.data
        i = len(d['vertices'])
        d['vertices'].append(vec(p)); d['normals'].append(vec(unit(n)))
        d['colors'].append(dict(zip(('r', 'g', 'b', 'a'), color(c))))
        d['polish'].append(polish); d['bone0'].append(bone)
        d['bone1'].append(other); d['blend'].append(round(blend, 6))
        return i

    def tri(self, a, b, c): self.data['triangles'].extend((a, b, c))

    def ellipsoid(self, center, radii, shade, polish=.16, bone=0, rings=10, sides=20, skin=False):
        start = len(self.data['vertices'])
        for j in range(rings+1):
            phi = math.pi*j/rings
            for i in range(sides+1):
                theta = TAU*i/sides
                q = (math.sin(phi)*math.cos(theta), math.cos(phi), math.sin(phi)*math.sin(theta))
                p = add(center, tuple(q[k]*radii[k] for k in range(3)))
                n = tuple(q[k]/radii[k] for k in range(3))
                # A continuous weighted face/body surface, not a detached neck.
                face = max(0, min(1, (p[0]-.22)/.55))*.80 if skin else 0
                tone = shade
                if skin:
                    boundary = .40 + .065*math.sin(theta*3)**2
                    tone = 'E6D2AA' if p[1] < boundary else ('5994A3' if p[0] < -.4 else '70A9B5')
                self.vertex(p, n, tone, polish, bone, 1 if skin else bone, face)
        for j in range(rings):
            for i in range(sides):
                a = start+j*(sides+1)+i; b = a+sides+1
                if j: self.tri(a, a+1, b)
                if j < rings-1: self.tri(a+1, b+1, b)

    def tube(self, points, widths, shade, polish=.25, bone=0, sides=10):
        start = len(self.data['vertices'])
        for j, p in enumerate(points):
            before = points[max(0, j-1)]; after = points[min(len(points)-1, j+1)]
            tangent = unit(tuple(after[k]-before[k] for k in range(3)))
            ref = (0, 0, 1) if abs(tangent[2]) < .9 else (0, 1, 0)
            u = unit(cross(tangent, ref)); v = cross(tangent, u)
            for i in range(sides):
                a = TAU*i/sides
                n = add(mul(u, math.cos(a)), mul(v, math.sin(a)))
                self.vertex(add(p, mul(n, widths[j])), n, shade, polish, bone)
        for j in range(len(points)-1):
            for i in range(sides):
                a=start+j*sides+i; b=start+j*sides+(i+1)%sides
                self.tri(a,b,a+sides); self.tri(b,b+sides,a+sides)
        # Caps keep silhouettes closed from either trait socket.
        for j, reverse in ((0, True),(len(points)-1, False)):
            center=self.vertex(points[j], (0,-1 if reverse else 1,0), shade, polish,bone)
            for i in range(sides):
                a=start+j*sides+i; b=start+j*sides+(i+1)%sides
                self.tri(center,b,a) if reverse else self.tri(center,a,b)

    def plate(self, x, size, shade):
        # Beveled shield with a mineral inlay, three discrete layers.
        outline=[(.95,0),(.48,-.82),(-.43,-.82),(-.95,0),(-.43,.82),(.48,.82)]
        start=len(self.data['vertices'])
        for scale,y,c in ((1,-.025,'274C5A'),(1,.018,'386A79'),(.79,.058,shade)):
            for a,b in outline:
                self.vertex((x+a*size,y,b*.255), (a*.15,1,b*.15), c,.36)
        for ring in range(2):
            for i in range(6):
                a=start+ring*6+i; b=start+ring*6+(i+1)%6
                self.tri(a,b,a+6);self.tri(b,b+6,a+6)
        top=self.vertex((x,.083,0),(0,1,0),shade,.32)
        bottom=self.vertex((x,-.025,0),(0,-1,0),'274C5A',.2)
        for i in range(6):
            self.tri(top,start+12+i,start+12+(i+1)%6)
            self.tri(bottom,start+(i+1)%6,start+i)

    def save(self, directory=OUT):
        directory.mkdir(parents=True, exist_ok=True)
        path=directory/(self.data['id']+'.frontiermesh')
        path.write_text(json.dumps(self.data,separators=(',',':'))+'\n')
        print(f'{path.relative_to(ROOT)}: {len(self.data["vertices"])} vertices, {len(self.data["triangles"])//3} triangles')


def vetch():
    m=Mesh('vetch','body')
    # Continuous low dome: no permanent Carapace plates or Cinder geometry.
    m.ellipsoid((-.04,.51,0),(.91,.49,.65),'70A9B5',rings=16,sides=36,skin=True)
    m.ellipsoid((.62,.355,0),(.335,.20,.43),'E6D2AA',bone=1,rings=10,sides=24)
    # Four planted limbs with dark toe caps, following the shared rig pivots.
    for bone,p in enumerate(((.35,.23,.45),(.35,.23,-.45),(-.57,.22,.43),(-.57,.22,-.43)),2):
        m.ellipsoid(p,(.25,.235,.235),'609BA9',bone=bone,rings=10,sides=16)
        m.ellipsoid(add(p,(.045,-.13,0)),(.28,.10,.24),'4C8191',bone=bone,rings=6,sides=16)
        for toe in (-1,0,1):
            m.ellipsoid(add(p,(.23,-.15,toe*.13)),(.09,.077,.075),'294553',.45,bone,6,10)
    # Eyes are skinned to the eyelid bones so existing blink animation applies.
    for side,bone in ((-1,6),(1,7)):
        z=side*.275
        m.ellipsoid((.797,.633,z),(.09,.147,.115),'386A78',bone=1,rings=8,sides=14)
        m.ellipsoid((.824,.633,z),(.079,.124,.094),'FFF0D2',.65,bone,8,14)
        m.ellipsoid((.883,.632,z+side*.012),(.032,.098,.069),'975622',.78,bone,8,14)
        m.ellipsoid((.904,.638,z+side*.014),(.024,.077,.049),'152633',.94,bone,8,14)
        m.ellipsoid((.925,.680,z-side*.012),(.009,.022,.016),'FFFAE5',.98,bone,5,8)
        m.ellipsoid((.942,.475,side*.085),(.012,.014,.020),'294553',.12,1,5,8)
        m.ellipsoid((.778,.757,z),(.115,.038,.12),'70A9B5',bone=1,rings=6,sides=12)
    smile=[]
    for i in range(17):
        t=i/8-1;y=.344+t*t*.055;z=t*.33
        x=.62+.335*math.sqrt(max(.01,1-((y-.355)/.2)**2-(z/.43)**2))
        smile.append((x+.002,y,z))
    m.tube(smile,[.007]*17,'7F6952',bone=1,sides=6)
    # Sparse raised pebble clusters on the dome. Geometry follows the actual
    # surface; broad value groups read at phone size without a baked light map.
    for row in range(5):
        phi=.30+row*.205
        for col in range(14):
            theta=TAU*(col+(row%2)*.5)/14
            q=(math.sin(phi)*math.cos(theta),math.cos(phi),math.sin(phi)*math.sin(theta))
            p=add((-.04,.51,0),(q[0]*.918,q[1]*.499,q[2]*.658))
            if p[0]>.50: continue
            m.ellipsoid(p,(.045,.015,.039),'639FAB' if col%3 else '7BB0BA',.24,0,3,6)
    return m


def cinder():
    m=Mesh('cinder','part')
    for i in range(3):
        x=(i-1)*.31; h=.34+i*.08
        m.ellipsoid((x,-.02,0),(.145,.065,.12),'63494A',.22,rings=6,sides=12)
        m.tube([(x,-.01,0),(x+.035,h*.38,0),(x-.005,h*.75,0),(x-.12,h,0)],
               [.118,.105,.06,.003],'E87F51',.47,sides=14)
        # Warm inset ridge; no additive halo needed to read the tip.
        m.tube([(x+.083,.055,-.035),(x+.115,h*.4,-.025),(x+.04,h*.72,-.015),(x-.12,h,0)],
               [.023,.025,.017,.002],'FFC070',.60,sides=7)
    return m


def carapace():
    m=Mesh('carapace','part')
    for i in range(3): m.plate((i-1)*.28,.205,'DFDCC4' if i==1 else 'B9C7BD')
    return m


if __name__=='__main__':
    meshes=[vetch(),cinder(),carapace()]
    for m in meshes: m.save()
    total=sum(len(m.data['triangles'])//3 for m in meshes)
    assert total<=10000, total
    print('Vetch + Cinder + Carapace:', total, 'triangles; candidate, not visually accepted')
