"""Structural checks for the candidate sources and portable review models.

These checks do not assert aesthetic quality, Unity import, or device performance.
"""
import json
import math
import struct
from pathlib import Path
from build_vetch import OUT, ROOT
from install_runtime import ASSETS, BODIES, PARTS, RUNTIME, SOURCE, guid
from rigs import RIGS


def check_sources():
    counts={}
    paths=list(OUT.glob('*.frontiermesh'))+list((OUT.parent/'Traits').glob('*.frontiermesh'))
    paths+=list((OUT.parent/'Founders').glob('*.frontiermesh'))
    for path in sorted(paths):
        d=json.loads(path.read_text());n=len(d['vertices'])
        for stream in ('normals','colors','polish','bone0','bone1','blend'):
            assert len(d[stream])==n,(path,stream)
        assert d['version']==1 and d['id']==path.stem
        assert len(d['triangles'])%3==0
        assert all(0<=i<n for i in d['triangles'])
        for stream in ('vertices','normals','colors'):
            assert all(math.isfinite(x) for v in d[stream] for x in v.values())
        assert all(0<=b<=1 for b in d['blend'])
        limit=len(RIGS[d['id']]) if d['kind']=='body' else 1
        assert all(0<=b<limit for b in d['bone0']+d['bone1'])
        if d['kind']=='body':
            assert set(d['bone0'])>=set(range(1,len(RIGS[d['id']]))), 'limbs and eye bones must be used'
            lo=[min(v[a] for v in d['vertices']) for a in ('x','y','z')]
            hi=[max(v[a] for v in d['vertices']) for a in ('x','y','z')]
            if d['id']=='vetch':assert hi[0]-lo[0]>1.5*(hi[1]-lo[1]), 'Vetch must stay a low dome'
            assert lo[1]>=-.07, 'feet must remain near the ground plane'
        counts[d['id']]=len(d['triangles'])//3
    assert counts.keys()==set(RIGS)|{'cinder','carapace','taunt','splash','sprint',
                           'litter','reach','pierce','regrow','burrow','screen','chill'}
    traits=counts.keys()-set(RIGS)
    for species in RIGS:
        for first in traits:
            for second in traits:
                assert counts[species]+counts[first]+counts[second]<=10000
    print('PASS source mesh streams, finite values, indices, six rigs, all 864 ordered body/trait pairings:',counts)


def check_glbs():
    model_root=ROOT/'specs/Designs/visual-production-v1/source-v1/models'
    for path in sorted(list(model_root.glob('*.glb'))+list((model_root/'parts').glob('*.glb'))):
        raw=path.read_bytes();magic,version,length=struct.unpack_from('<III',raw)
        assert (magic,version,length)==(0x46546c67,2,len(raw))
        size,kind=struct.unpack_from('<II',raw,12);assert kind==0x4e4f534a
        d=json.loads(raw[20:20+size]);offset=20+size
        binary_size,kind=struct.unpack_from('<II',raw,offset);assert kind==0x004e4942
        assert offset+8+binary_size==len(raw)
        assert binary_size>=d['buffers'][0]['byteLength']
        for view in d['bufferViews']:
            assert view['byteOffset']%4==0
            assert view['byteOffset']+view['byteLength']<=binary_size
        for a in d['accessors']:
            width={'SCALAR':1,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
            component={5126:4,5123:2,5125:4}[a['componentType']]
            assert a['count']*width*component==d['bufferViews'][a['bufferView']]['byteLength']
        if path.stem in set(RIGS)|{'cinderplate'}:
            species='vetch' if path.stem=='cinderplate' else path.stem
            assert len(d['skins'])==1 and len(d['skins'][0]['joints'])==len(RIGS[species])
            assert {a['name'] for a in d['animations']}=={'idle','walk','attack','hit','reveal'}
            assert [d['nodes'][i]['name'] for i in d['skins'][0]['joints']]==[
                bone[0] for bone in RIGS[species]]
            assert all(c['target']['node'] in d['skins'][0]['joints']
                       for a in d['animations'] for c in a['channels'])
        print('PASS GLB container, buffers, accessor sizes and rig/clips:',path.name)


def check_runtime_install():
    expected = {}
    for name in BODIES:
        expected[RUNTIME/'Creatures'/(name+'.frontiermesh')] = (
            SOURCE/('Vetch' if name == 'vetch' else 'Founders')/(name+'.frontiermesh'))
    for name in PARTS:
        expected[RUNTIME/'Parts'/(name+'.frontiermesh')] = (
            SOURCE/('Vetch' if name in ('cinder','carapace') else 'Traits')/(name+'.frontiermesh'))
    assert set(RUNTIME.rglob('*.frontiermesh')) == set(expected)
    for target, source in expected.items():
        assert target.read_bytes() == source.read_bytes(), 'installed art is stale: '+str(target)
        meta = target.with_name(target.name+'.meta').read_text()
        assert 'guid: '+guid(target.relative_to(ASSETS)) in meta
        assert 'ScriptedImporter:' in meta
    all_meta = list((ROOT/'client/Assets').rglob('*.meta'))
    ids = [next((line.partition(': ')[2].strip() for line in p.read_text().splitlines()
                 if line.startswith('guid: ')), None) for p in all_meta]
    assert None not in ids and len(ids) == len(set(ids)), 'Unity GUID collision or missing GUID'
    print('PASS installed runtime bodies/parts, source parity and distinct Unity GUIDs:',len(expected))


if __name__=='__main__':
    check_sources();check_glbs();check_runtime_install()
