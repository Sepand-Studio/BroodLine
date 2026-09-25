"""Software render the actual candidate geometry, not the generated concept PNGs.

This is a static geometry review, explicitly not a Unity screenshot or GPU benchmark.
Requires numpy and Pillow. Camera and vertex-material lighting are fixed for comparisons.
"""
import json
import math
import numpy as np
from PIL import Image
from build_vetch import ROOT, OUT

DEST=ROOT/'specs/Designs/visual-production-v1/source-v1/review'
PAIRS={'vetch':('taunt','carapace'),'ember':('cinder','splash'),
       'skitter':('sprint','litter'),'hollow':('reach','pierce'),
       'loam':('regrow','burrow'),'pale':('screen','chill')}
SOCKETS={
 'vetch':{'carapace':((-.17,.51,-.635),1.,True),'taunt':((-.18,1,0),1.,False),
          'cinder':((-.18,1,0),1.,False)},
 'ember':{'cinder':((-.22,.90,0),.70,False),'splash':((-.09,.62,-.255),.62,True)},
 'skitter':{'sprint':((-.06,.64,0),.51,False),'litter':((-.14,.47,-.27),.40,True)},
 'hollow':{'reach':((-.20,1.05,0),.52,False),'pierce':((-.22,.85,-.235),.46,True)},
 'loam':{'regrow':((-.08,.55,0),.64,False),'burrow':((.06,.30,-.34),.55,True)},
 'pale':{'screen':((-.12,.74,0),.58,False),'chill':((-.08,.42,-.26),.53,True)}}


def source(id,species):
    body=id in ('vetch','ember','skitter','hollow','loam','pale')
    base=OUT if id in ('vetch','cinder','carapace') else OUT.parent/('Founders' if body else 'Traits')
    d=json.loads((base/(id+'.frontiermesh')).read_text())
    points=np.array([[v['x'],v['y'],v['z']] for v in d['vertices']])
    normals=np.array([[v['x'],v['y'],v['z']] for v in d['normals']])
    if not body:
        origin,scale,flank=SOCKETS[species][id]
        points*=scale
        if flank:
            # Unity Quaternion.Euler(-90,0,0), then flank translation.
            rot=np.array([[1,0,0],[0,0,1],[0,-1,0]])
            points=points@rot.T;normals=normals@rot.T
        points+=origin
    colors=np.array([[c['r'],c['g'],c['b']] for c in d['colors']])
    return points,normals,colors,np.array(d['triangles']).reshape(-1,3),np.array(d['polish'])


def render(ids,width,height,silhouette=False):
    view=np.array([3.,1.65,-4.]);view/=np.linalg.norm(view)
    right=np.cross([0,1,0],view);right/=np.linalg.norm(right)
    up=np.cross(view,right)
    matrix=np.stack([right,up,view])
    batches=[source(id,ids[0]) for id in ids]
    projected=np.concatenate([b[0]@matrix.T for b in batches])
    ranges=projected.max(axis=0)-projected.min(axis=0)
    scale=min(width*.84/max(.01,ranges[0]),height*(.82 if silhouette else .55)/max(.01,ranges[1]))
    center=(projected.max(axis=0)+projected.min(axis=0))/2
    buffer=np.zeros((height,width,4),dtype=np.uint8)
    depth=np.full((height,width),-np.inf)
    light=np.array([-.3,.8,-.55]);light/=np.linalg.norm(light)
    half=light+view;half/=np.linalg.norm(half)
    for points,normals,colors,triangles,polish in batches:
        pos=points@matrix.T
        pos[:,0]=width/2+(pos[:,0]-center[0])*scale
        pos[:,1]=height/2-(pos[:,1]-center[1])*scale
        for tri in triangles:
            p=pos[tri];n=normals[tri];c=colors[tri];s=polish[tri]
            x0=max(0,int(np.floor(p[:,0].min())));x1=min(width-1,int(np.ceil(p[:,0].max())))
            y0=max(0,int(np.floor(p[:,1].min())));y1=min(height-1,int(np.ceil(p[:,1].max())))
            if x0>x1 or y0>y1:continue
            den=(p[1,1]-p[2,1])*(p[0,0]-p[2,0])+(p[2,0]-p[1,0])*(p[0,1]-p[2,1])
            if abs(den)<1e-8:continue
            yy,xx=np.mgrid[y0:y1+1,x0:x1+1];xx=xx+.5;yy=yy+.5
            a=((p[1,1]-p[2,1])*(xx-p[2,0])+(p[2,0]-p[1,0])*(yy-p[2,1]))/den
            b=((p[2,1]-p[0,1])*(xx-p[2,0])+(p[0,0]-p[2,0])*(yy-p[2,1]))/den
            weights=np.stack([a,b,1-a-b],axis=-1)
            z=weights@p[:,2]
            current=depth[y0:y1+1,x0:x1+1]
            mask=(weights.min(axis=-1)>=0)&(z>current)
            if not mask.any():continue
            current[mask]=z[mask]
            normal=weights@n
            normal/=np.maximum(1e-8,np.linalg.norm(normal,axis=-1,keepdims=True))
            diffuse=np.clip(normal@light,0,1)
            shine=(np.clip(normal@half,0,1)**(12+108*(weights@s)))*(.035+.5*(weights@s)**2)
            lighting=np.array([.43,.52,.62])+diffuse[...,None]*np.array([.55,.45,.32])
            rgb=np.clip((weights@c)*lighting+shine[...,None],0,1)*255
            region=buffer[y0:y1+1,x0:x1+1]
            region[mask,:3]=0 if silhouette else rgb[mask].astype(np.uint8)
            region[mask,3]=255
    return Image.fromarray(buffer)


if __name__=='__main__':
    DEST.mkdir(parents=True,exist_ok=True)
    requests=[(species+'-base',[species]) for species in PAIRS]
    requests +=[(species+'-after',[species,*traits]) for species,traits in PAIRS.items()]
    requests.append(('cinderplate',['vetch','cinder','carapace']))
    for name,ids in requests:
        for width,height in ((430,932),(402,874),(390,844),(360,640)):
            render(ids,width,height).save(DEST/(f'{name}-{width}x{height}.png'))
        render(ids,40,40,True).save(DEST/(name+'-40px.png'))
        render(ids,780,780).save(DEST/(name+'-geometry.png'))
        print(name,'geometry review rendered')
