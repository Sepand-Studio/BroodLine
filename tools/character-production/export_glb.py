"""Export editable candidate meshes, rig and review clips to portable glTF 2.0.

Preview clips are for DCC review. Unity continues to use FrontierPose and its
pause/reduced-motion/reaction state; no Animator is added to the game prefab.
"""
import json
import math
import struct
from pathlib import Path
from build_vetch import ROOT, OUT
from rigs import RIGS

DEST=ROOT/'specs/Designs/visual-production-v1/source-v1/models'

class GLB:
    def __init__(self):
        self.bin=bytearray()
        self.doc=dict(asset={'version':'2.0','generator':'BroodLine candidate authoring'},
                      scene=0,scenes=[{'nodes':[0]}],nodes=[{'name':'Vetch review','children':[]}],
                      meshes=[],materials=[{'name':'Layered vertex material',
                         'pbrMetallicRoughness':{'metallicFactor':0,'roughnessFactor':.48}}],
                      bufferViews=[],accessors=[])

    def accessor(self, values, kind, component=5126):
        size={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[kind]
        while len(self.bin)%4:self.bin.append(0)
        offset=len(self.bin)
        fmt={5126:'f',5123:'H',5125:'I'}[component]
        flat=[x for value in values for x in value] if size>1 else values
        self.bin.extend(struct.pack('<'+fmt*len(flat),*flat))
        view=len(self.doc['bufferViews'])
        self.doc['bufferViews'].append({'buffer':0,'byteOffset':offset,'byteLength':len(self.bin)-offset})
        a={'bufferView':view,'componentType':component,'count':len(values),'type':kind}
        if kind in ('SCALAR','VEC3'):
            rows=[[v] for v in values] if size==1 else values
            a['min']=[min(v[i] for v in rows) for i in range(size)]
            a['max']=[max(v[i] for v in rows) for i in range(size)]
        self.doc['accessors'].append(a)
        return len(self.doc['accessors'])-1

    def mesh(self, source, skin=False):
        def xyz(v):return (v['x'],v['y'],-v['z'])
        def linear(c):return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4
        attributes={'POSITION':self.accessor([xyz(v) for v in source['vertices']],'VEC3'),
                    'NORMAL':self.accessor([xyz(v) for v in source['normals']],'VEC3'),
                    'COLOR_0':self.accessor([(linear(c['r']),linear(c['g']),linear(c['b']),1) for c in source['colors']],'VEC4')}
        if skin:
            attributes['JOINTS_0']=self.accessor([(a,b,0,0) for a,b in zip(source['bone0'],source['bone1'])],'VEC4',5123)
            attributes['WEIGHTS_0']=self.accessor([(1-b,b,0,0) for b in source['blend']],'VEC4')
        indices=[]
        for i in range(0,len(source['triangles']),3):
            a,b,c=source['triangles'][i:i+3];indices.extend((a,c,b))
        self.doc['meshes'].append({'name':source['id'],'primitives':[{'attributes':attributes,
            'indices':self.accessor(indices,'SCALAR',5123),'material':0}]})
        return len(self.doc['meshes'])-1

    def node(self, value):
        self.doc['nodes'].append(value)
        return len(self.doc['nodes'])-1

    def rig(self,bones):
        matrices=[]
        for i,(name,parent,p) in enumerate(bones):
            base=(0,0,0) if parent<0 else bones[parent][2]
            node=self.node({'name':name,'translation':[p[0]-base[0],p[1]-base[1],-(p[2]-base[2])],'children':[]})
            self.doc['nodes'][0 if parent<0 else parent+1]['children'].append(node)
            matrices.append([1,0,0,0,0,1,0,0,0,0,1,0,-p[0],-p[1],p[2],1])
        self.doc['skins']=[{'name':'Frontier '+bones[0][0]+' rig','joints':list(range(1,len(bones)+1)),
                            'skeleton':1,'inverseBindMatrices':self.accessor(matrices,'MAT4')}]

    def clips(self,bones):
        animations=[]
        for name,duration in [('idle',2.6),('walk',1.0),('attack',.28),('hit',.16),('reveal',1.6)]:
            times=[duration*i/32 for i in range(33)]
            time_accessor=self.accessor(times,'SCALAR')
            anim={'name':name,'samplers':[],'channels':[]}
            def track(node,path,values,kind):
                sampler=len(anim['samplers'])
                anim['samplers'].append({'input':time_accessor,'output':self.accessor(values,kind),'interpolation':'LINEAR'})
                anim['channels'].append({'sampler':sampler,'target':{'node':node,'path':path}})
            if name in ('idle','walk'):
                track(1,'scale',[(1,1+.018*math.sin(t/duration*math.tau),1) for t in times],'VEC3')
            if name=='walk':
                for i,(bone_name,_,_) in enumerate(bones):
                    if not bone_name.startswith(('leg','wing','middle','rear','tail','front')):continue
                    values=[]
                    for t in times:
                        a=math.radians(18)*math.sin(t*math.tau+(i%2)*math.pi)/2
                        values.append((0,0,math.sin(a),math.cos(a)))
                    track(i+1,'rotation',values,'VEC4')
            if name in ('attack','hit','reveal'):
                degrees={'attack':9,'hit':-7,'reveal':6}[name]
                values=[]
                for t in times:
                    a=math.radians(degrees)*math.sin(t/duration*math.pi)/2
                    values.append((0,0,math.sin(a),math.cos(a)))
                head=next(i for i,(bone_name,_,_) in enumerate(bones) if bone_name=='head')
                track(head+1,'rotation',values,'VEC4')
            animations.append(anim)
        self.doc['animations']=animations

    def save(self,path):
        self.doc['buffers']=[{'byteLength':len(self.bin)}]
        raw=json.dumps(self.doc,separators=(',',':')).encode()
        raw+=b' '*((-len(raw))%4);self.bin+=b'\0'*((-len(self.bin))%4)
        data=struct.pack('<III',0x46546c67,2,28+len(raw)+len(self.bin))
        data+=struct.pack('<II',len(raw),0x4e4f534a)+raw
        data+=struct.pack('<II',len(self.bin),0x004e4942)+self.bin
        path.write_bytes(data)
        print(path.relative_to(ROOT),len(data),'bytes')


def main():
    DEST.mkdir(parents=True,exist_ok=True)
    sources={id:json.loads((OUT/(id+'.frontiermesh')).read_text()) for id in ('vetch','cinder','carapace')}
    for assembled in (False,True):
        g=GLB();g.rig(RIGS['vetch'])
        body=g.node({'name':'Vetch base','mesh':g.mesh(sources['vetch'],True),'skin':0})
        g.doc['nodes'][0]['children'].append(body)
        if assembled:
            for id,p,rotation in [('cinder',[-.18,1,0],[0,0,0,1]),
                                  ('carapace',[-.17,.51,.635],[math.sqrt(.5),0,0,math.sqrt(.5)])]:
                node=g.node({'name':id,'mesh':g.mesh(sources[id]),'translation':p,'rotation':rotation})
                g.doc['nodes'][1]['children'].append(node)
        g.clips(RIGS['vetch']);g.save(DEST/('cinderplate.glb' if assembled else 'vetch.glb'))
    founder_dir=ROOT/'client/Assets/Frontier/ProductionCandidates/Founders'
    for path in sorted(founder_dir.glob('*.frontiermesh')):
        data=json.loads(path.read_text());id=data['id'];g=GLB();g.rig(RIGS[id])
        body=g.node({'name':id+' base','mesh':g.mesh(data,True),'skin':0})
        g.doc['nodes'][0]['children'].append(body)
        g.clips(RIGS[id]);g.save(DEST/(id+'.glb'))
    for id in ('cinder','carapace'):
        g=GLB();node=g.node({'name':id,'mesh':g.mesh(sources[id])})
        g.doc['nodes'][0]['children'].append(node);g.save(DEST/(id+'.glb'))
    trait_dir=ROOT/'client/Assets/Frontier/ProductionCandidates/Traits'
    part_dest=DEST/'parts';part_dest.mkdir(parents=True,exist_ok=True)
    for path in sorted(trait_dir.glob('*.frontiermesh')):
        data=json.loads(path.read_text())
        g=GLB();node=g.node({'name':data['id'],'mesh':g.mesh(data)})
        g.doc['nodes'][0]['children'].append(node)
        g.save(part_dest/(data['id']+'.glb'))


if __name__=='__main__':main()
