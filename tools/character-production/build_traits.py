"""Editable first-pass modular geometry for the other ten inherited traits.

Every part is in socket-local coordinates, origin/unit scale, extending in +Y.
These are candidates for review in both slots on all six species, not final art.
"""
import math
from build_vetch import Mesh, ROOT

OUT=ROOT/'client/Assets/Frontier/ProductionCandidates/Traits'


def prism(m, outline, z0, z1, side, front, bone=0):
    a=len(m.data['vertices'])
    for z,c,n in ((z0,side,(0,0,-1)),(z1,front,(0,0,1))):
        for x,y in outline:m.vertex((x,y,z),n,c,.28,bone)
    N=len(outline)
    for i in range(1,N-1):
        m.tri(a,a+i+1,a+i)
        m.tri(a+N,a+N+i,a+N+i+1)
    for i in range(N):
        j=(i+1)%N
        m.tri(a+i,a+j,a+N+i)
        m.tri(a+j,a+N+j,a+N+i)


def taunt():
    m=Mesh('taunt','part')
    for side in (-1,1):
        z=side*.17
        m.ellipsoid((0,-.035,z),(.13,.055,.11),'6C5A46',.28,rings=5,sides=10)
        m.tube([(0,0,z),(.065,.14,z+side*.035),(.11,.29,z+side*.08),(.14,.35,z+side*.14)],
               [.095,.072,.036,.002],'D6AD62',.46,sides=10)
    return m


def splash():
    m=Mesh('splash','part')
    m.ellipsoid((0,-.045,0),(.29,.07,.24),'713F48',.25,rings=6,sides=12)
    for i in (-1,0,1):
        m.ellipsoid((i*.19,.12+(.06 if i==0 else 0),(.045 if i else -.015)),
                    (.13,.16,.13),'A94E5A' if i else 'C35B61',.35,rings=9,sides=12)
    return m


def sprint():
    m=Mesh('sprint','part')
    for side in (-1,1):
        z=side*.17
        prism(m,[(.15,-.04),(.10,.14),(-.30,.36),(-.18,.20),(-.06,.02)],
              z-.024,z+.024,'6D501E','E6A640')
        m.tube([(.14,-.03,z),(.04,.14,z),(-.30,.36,z)],
               [.033,.027,.003],'544734',.24,sides=6)
    return m


def litter():
    m=Mesh('litter','part')
    m.ellipsoid((0,-.025,0),(.34,.08,.28),'776745',.15,rings=7,sides=14)
    positions=[(-.15,.06,-.11),(0,.10,-.12),(.16,.06,-.10),(-.08,.07,.12),(.11,.06,.12)]
    for i,p in enumerate(positions):
        m.ellipsoid(p,(.075,.105,.075),'F1E3C4' if i%2 else 'FFF2D7',.38,rings=7,sides=10)
    return m


def reach():
    m=Mesh('reach','part')
    m.ellipsoid((-.09,-.02,0),(.14,.06,.11),'423471',.2,rings=5,sides=10)
    m.tube([(-.09,0,0),(.025,.13,0),(.17,.31,0),(.29,.45,0)],
           [.09,.075,.05,.022],'684DAA',.45,sides=10)
    m.tube([(.29,.45,0),(.44,.59,0)], [.052,.002],'D9B46B',.68,sides=9)
    return m


def pierce():
    m=Mesh('pierce','part')
    for i in (-1,0,1):
        z=i*.14
        m.ellipsoid((i*.07,-.025,z),(.07,.04,.07),'5A687B',.2,rings=5,sides=9)
        height=.43 if i==0 else .31
        m.tube([(i*.07,0,z),(i*.09,height*.50,z),(i*.12,height,z)],
               [.068,.043,.002],'C7DDE3' if i==0 else '8DABBB',.46,sides=8)
    return m


def regrow():
    m=Mesh('regrow','part')
    m.ellipsoid((0,-.03,0),(.11,.05,.10),'48714B',.14,rings=5,sides=9)
    m.tube([(0,-.02,0),(0,.15,0),(0,.36,0)], [.047,.037,.017],'538E62',.20,sides=8)
    for side in (-1,1):
        z=side*.015
        prism(m,[(0,.22),(side*.12,.28),(side*.31,.33),(side*.22,.43),(side*.09,.42)],
              z-.016,z+.016,'3D7855','92C67F' if side>0 else '74B376')
    return m


def burrow():
    m=Mesh('burrow','part')
    prism(m,[(-.18,-.055),(.14,-.055),(.15,.07),(-.18,.07)],-.13,.13,'5B5548','817660')
    prism(m,[(-.02,.05),(.24,.05),(.23,.17),(-.02,.17)],-.105,.105,'6E604E','A39071')
    prism(m,[(.17,.15),(.35,.20),(.48,.32),(.35,.29),(.18,.26)],-.09,.09,'786648','CEAE78')
    return m


def screen():
    m=Mesh('screen','part')
    m.ellipsoid((0,-.015,0),(.12,.065,.14),'63818D',.2,rings=5,sides=10)
    tips=[(-.33,.33),(-.12,.46),(.12,.46),(.33,.33)]
    for i,(x,y) in enumerate(tips):
        m.tube([(0,0,0),(x*.58,y*.57,0),(x,y,0)],
               [.032,.029,.012],'D5E8E5',.37,sides=6)
        m.ellipsoid((x,y,0),(.032,.032,.038),'EDF4E6',.38,rings=5,sides=7)
        if i:
            previous=tips[i-1]
            prism(m,[(0,.015),(previous[0],previous[1]),(x,y)],-.012,.012,
                  '568DA9','9FCADB')
    return m


def chill():
    m=Mesh('chill','part')
    for i,(x,h,z) in enumerate([(-.18,.27,-.04),(0,.43,0),(.20,.34,.02)]):
        m.ellipsoid((x,-.02,z),(.075,.04,.075),'55798B',.2,rings=5,sides=8)
        prism(m,[(x-.085,0),(x+.09,0),(x+.045,h*.70),(x,h),(x-.055,h*.57)],
              z-.045,z+.045,'6BABC6','C5E8EE' if i==1 else '9ACBDE')
    return m


if __name__=='__main__':
    for constructor in (taunt,splash,sprint,litter,reach,pierce,regrow,burrow,screen,chill):
        mesh=constructor();mesh.save(OUT)
        assert len(mesh.data['triangles'])//3<1000,mesh.data['id']
