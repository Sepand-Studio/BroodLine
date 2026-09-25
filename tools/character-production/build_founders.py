"""Offline skinned body candidates for the other five founders.

The rigs exactly match FrontierRigDefinition names/order/hierarchy/positions.
Source art informs palette and silhouette; final character polish is pending.
"""
from build_vetch import Mesh, ROOT
from build_traits import prism
from rigs import RIGS

OUT=ROOT/'client/Assets/Frontier/ProductionCandidates/Founders'


def eye(m,p,bone,iris,hide):
    x,y,z=p
    parent=RIGS[m.data['id']][bone][1]
    m.ellipsoid((x-.045,y,z),(.128,.124,.125),hide,.18,parent,8,12)
    m.ellipsoid((x+.005,y,z),(.057,.085,.074),'F8EDD8',.65,bone,8,12)
    m.ellipsoid((x+.043,y,z),(.025,.069,.052),iris,.77,bone,8,12)
    m.ellipsoid((x+.063,y+.005,z),(.014,.053,.037),'182533',.92,bone,7,10)
    m.ellipsoid((x+.075,y+.038,z-.015),(.008,.016,.012),'FFFFFF',.95,bone,5,8)
    m.ellipsoid((x-.025,y+.085,z),(.115,.038,.118),hide,.18,parent,6,11)


def ember():
    m=Mesh('ember','body')
    m.ellipsoid((0,.66,0),(.25,.35,.22),'DC7C6E',.20,0,14,20)
    m.ellipsoid((.145,.63,0),(.105,.29,.17),'F1DFC0',.12,0,12,18)
    m.ellipsoid((-.035,.42,0),(.26,.18,.24),'BD625B',.16,0,11,18)
    m.tube([(.03,.77,0),(.10,.97,0),(.18,1.16,0)], [.11,.085,.075],'D77A69',.22,1,12)
    m.tube([(.105,.79,-.086),(.18,1.04,-.066)], [.042,.023],'F1DFC0',.12,1,8)
    m.ellipsoid((.25,1.17,0),(.22,.17,.16),'E78C76',.23,2,12,18)
    m.ellipsoid((.38,1.115,0),(.13,.075,.12),'F1DFC0',.16,2,8,14)
    prism(m,[(.08,1.22),(.03,1.34),(.24,1.58),(.42,1.49),(.29,1.40)],-.085,.085,'A95853','F1A453',2)
    for b in (9,10):eye(m,RIGS['ember'][b][2],b,'A26426','E78C76')
    for leg,knee,side in ((3,4,1),(5,6,-1)):
        z=side*.17
        m.ellipsoid((-.035,.39,z),(.13,.20,.12),'CC7468',.16,leg,9,14)
        m.tube([(.06,.22,side*.19),(.08,.08,side*.19)], [.09,.06],'D7846C',.16,knee,10)
        m.ellipsoid((.12,.065,side*.19),(.16,.07,.115),'AC5957',.26,knee,7,13)
        for toe in (-1,0,1):m.ellipsoid((.25,.04,side*.19+toe*.075),(.055,.04,.038),'382F38',.48,knee,5,8)
    for arm,side in ((7,1),(8,-1)):
        m.tube([(.065,.83,side*.20),(.25,.70,side*.25),(.30,.67,side*.25)],
               [.065,.040,.013],'CE7367',.20,arm,8)
    m.tube([(-.18,.44,0),(-.43,.36,0),(-.72,.41,0),(-.96,.59,0)],
           [.17,.12,.07,.004],'A75850',.19,0,11)
    return m


def skitter():
    m=Mesh('skitter','body')
    m.ellipsoid((-.07,.46,0),(.39,.17,.23),'D0A357',.13,0,13,20)
    m.ellipsoid((-.10,.39,0),(.35,.10,.20),'ECD8A7',.11,0,10,16)
    m.ellipsoid((.31,.47,0),(.19,.13,.17),'DAAD69',.17,1,12,18)
    m.ellipsoid((.44,.40,0),(.105,.055,.12),'F0E0BD',.11,1,8,12)
    for b in (14,15):eye(m,RIGS['skitter'][b][2],b,'805122','DAAD69')
    for pair in range(3):
        for side in (1,-1):
            leg=2+pair*4+(0 if side==1 else 2);knee=leg+1
            x=.20-pair*.22
            m.ellipsoid((x,.40,side*.22),(.075,.09,.075),'BE8D48',.16,leg,7,10)
            m.tube([(x,.40,side*.22),(x+(1-pair)*.17,.30,side*.47)],
                   [.066,.045],'CA9A50',.17,leg,9)
            m.tube([(x+(1-pair)*.17,.30,side*.47),(x+(1-pair)*.23,.04,side*.53)],
                   [.049,.024],'AA7C42',.15,knee,8)
            m.ellipsoid((x+(1-pair)*.23,.035,side*.53),(.09,.035,.06),'493C32',.26,knee,5,9)
    return m


def hollow():
    m=Mesh('hollow','body')
    m.ellipsoid((-.14,.88,0),(.30,.19,.20),'7967B8',.17,0,12,18)
    m.ellipsoid((-.03,.78,0),(.17,.075,.16),'E9DFDF',.10,0,8,14)
    m.tube([(.06,.86,0),(.30,1.04,0),(.63,1.17,0)],
           [.085,.068,.05],'7864B5',.22,1,12)
    m.tube([(.09,.78,-.04),(.31,.97,-.04),(.61,1.10,-.04)],
           [.029,.025,.013],'E9DFDF',.11,1,7)
    m.ellipsoid((.68,1.15,0),(.18,.12,.12),'8574C4',.25,2,10,16)
    m.ellipsoid((.80,1.11,0),(.08,.05,.08),'E9DFDF',.13,2,7,12)
    for b in (7,8):eye(m,RIGS['hollow'][b][2],b,'755184','8574C4')
    for leg,knee,side in ((3,4,1),(5,6,-1)):
        m.tube([(-.10,.78,side*.14),(-.19,.39,side*.17)],
               [.060,.037],'6C5B9F',.14,leg,9)
        m.tube([(-.19,.39,side*.17),(-.18,.045,side*.18)],
               [.037,.023],'7D6AB0',.16,knee,8)
        m.ellipsoid((-.12,.035,side*.18),(.11,.035,.055),'3C394F',.21,knee,5,9)
    m.tube([(-.39,.87,0),(-.50,.92,0)], [.08,.002],'9D8BCD',.18,0,8)
    return m


def loam():
    m=Mesh('loam','body')
    sizes={1:1,2:.93,3:.78,4:1.04}
    for bone in (3,2,1,4):
        p=RIGS['loam'][bone][2];s=sizes[bone]
        m.ellipsoid(p,(.28*s,.255*s,.33*s),'76AE7D',.14,bone,11,17)
        m.ellipsoid((p[0]-.075,p[1]+.105*s,0),(.21*s,.19*s,.31*s),'4E805D',.18,bone,8,14)
        m.ellipsoid((p[0]+.025,p[1]-.14*s,0),(.23*s,.09*s,.27*s),'E6D8AA',.10,bone,7,13)
    m.ellipsoid((.67,.31,0),(.28,.21,.29),'8AC08B',.14,5,12,20)
    m.ellipsoid((.80,.21,0),(.16,.09,.24),'E6D8AA',.11,5,8,14)
    for b in (6,7):eye(m,RIGS['loam'][b][2],b,'734D25','8AC08B')
    return m


def pale():
    m=Mesh('pale','body')
    m.ellipsoid((-.03,.49,0),(.34,.22,.23),'65869F',.22,0,11,18)
    m.ellipsoid((.05,.36,0),(.26,.10,.19),'E8DFC7',.11,0,8,14)
    m.ellipsoid((.32,.55,0),(.20,.14,.16),'A8C6CE',.26,1,10,16)
    m.ellipsoid((.43,.49,0),(.11,.07,.13),'E8DFC7',.13,1,7,12)
    for b in (6,7):eye(m,RIGS['pale'][b][2],b,'526F8C','A8C6CE')
    for side,wing,tip in ((1,2,3),(-1,4,5)):
        def span(z):return z*side
        # Articulated curved wing membrane, with a dark spar and a rounded tip.
        outline=[(-.03,.65,.14),(-.09,.80,.42),(-.07,.84,.77),(-.02,.90,1.18),
                 (.06,.79,1.10),(.18,.67,.66),(.24,.58,.22)]
        base=len(m.data['vertices'])
        for x,y,z in outline:
            weight=max(0,min(1,(z-.62)/.43))
            m.vertex((x,y,span(z)),(0,1,0),'B7D5DE',.18,wing,tip,weight)
        for i in range(1,len(outline)-1):
            m.tri(base,base+i,base+i+1)
            m.tri(base,base+i+1,base+i)
        m.tube([(-.03,.66,span(.15)),(-.09,.80,span(.42)),(-.07,.84,span(.77))],
               [.052,.047,.036],'4B6D89',.26,wing,10)
        m.tube([(-.07,.84,span(.77)),(-.02,.90,span(1.18))],
               [.038,.006],'557A98',.28,tip,9)
        for z in (.40,.66):
            m.tube([(-.08,.77,span(z)),(.19,.62,span(z+.08))],
                   [.018,.006],'E0EDE9',.26,wing,6)
    return m


if __name__=='__main__':
    for constructor in (ember,skitter,hollow,loam,pale):
        m=constructor();m.save(OUT)
        assert len(m.data['triangles'])//3<8000,m.data['id']
        bones=set(m.data['bone0'])
        assert bones>=set(range(1,len(RIGS[m.data['id']]))),(m.data['id'],bones)
