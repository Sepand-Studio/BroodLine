#!/usr/bin/env python3
"""Build a browser map review from the current Unity region catalog."""

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "client/Assets/Model/Catalogs/RegionCatalog.cs"
OUTPUT = ROOT / "implementation/reviews/phase10-map-graph.html"


def slug(name):
    return name.lower().replace("the ", "").replace(" ", "-")


source = SOURCE.read_text()
rows = re.findall(r'^\s*R\("([^"]+)",\s*Band\.(\w+),\s*(\d+),\s*(.*?)\),?\s*$', source, re.M)
nodes = [dict(id=slug(name), name=name, band=band, lanes=int(lanes),
              neighbours=[slug(n) for n in re.findall(r'"([^"]+)"', neighbours)])
         for name, band, lanes, neighbours in rows]
gates_section = source.split("public static readonly IReadOnlyList<(string A, string B)> Gates", 1)[1].split("};", 1)[0]
gates = [list(pair) for pair in re.findall(r'\("([^"]+)", "([^"]+)"\)', gates_section)]
assert len(nodes) == 30 and len(gates) == 8
edges = {tuple(sorted((node["id"], neighbour))) for node in nodes for neighbour in node["neighbours"]}
assert len(edges) == 43

page = r'''<!doctype html>
<html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Broodline · World Map graph review</title>
<style>
:root{--paper:#f5ebd9;--card:#fff8eb;--deep:#263345;--ink:#302d40;--muted:#686177;--violet:#6b4ec2;--brass:#b98b45;--gold:#e2c07a}
*{box-sizing:border-box}body{margin:0;background:#18232d;color:var(--ink);font:14px/1.35 system-ui,-apple-system,sans-serif}
.intro{max-width:850px;margin:20px auto;color:#eee6d5;padding:0 22px}.intro h1{font-size:25px;margin:0}.intro p{color:#bac6c7}
.phone{width:390px;max-width:calc(100vw - 24px);height:844px;margin:20px auto 35px;background:var(--paper);border:8px solid #101820;border-radius:32px;overflow:auto;box-shadow:0 20px 50px #0008;padding:24px 18px}
.eyebrow{font-size:10px;font-weight:800;letter-spacing:.15em;color:var(--violet);text-transform:uppercase}.title{font-size:25px;font-weight:850;margin:2px 0 18px}.card{background:var(--card);border:1px solid #ded0b7;border-radius:16px;padding:13px;margin-bottom:12px;box-shadow:0 5px 0 #ae9d7e22}.card h2{font-size:14px;margin:0 0 8px;text-transform:uppercase}
.here{font-size:11px;color:var(--muted);font-weight:800;margin-bottom:9px}.viewport{width:280px;height:280px;position:relative;margin:0 auto 10px;border:2px solid var(--brass);border-radius:14px;overflow:hidden;background:#ded8ba url('../../client/Assets/UI/Resources/Art/map/frontier-atlas.png') center/cover}
.ring{position:absolute;border:1px solid #e2c07a88;border-radius:50%;pointer-events:none}.ring.inner{width:86px;height:86px;left:97px;top:97px}.ring.mid{width:168px;height:168px;left:56px;top:56px}.ring.outer{width:244px;height:244px;left:18px;top:18px}
svg{position:absolute;inset:0;width:280px;height:280px;pointer-events:none}.edge{stroke:#555365;stroke-opacity:.5;stroke-width:2}.edge.gate{stroke:var(--brass);stroke-opacity:.85}.edge.route{stroke:var(--gold);stroke-opacity:1;stroke-width:4}
.dot{position:absolute;width:32px;height:32px;border:2px solid #635b70;border-radius:50%;background:var(--card);color:var(--ink);font:800 13px system-ui;cursor:pointer;padding:0}.dot.gate{border-radius:7px;border-color:var(--brass)}.dot.here{background:var(--violet);border-color:#49348d;color:#fff}.dot.route{background:var(--gold);border-color:var(--brass)}.dot.selected{outline:2px solid #fff2c5}.dot.muted{opacity:.75}
.legend{font-size:11px;text-align:center;color:var(--muted);line-height:1.5;margin:6px 0 12px}.panel{background:var(--deep);color:var(--paper);border:1px solid var(--brass);border-radius:11px;padding:11px}.panel strong{display:block;color:var(--gold);font-size:18px}.panel p{margin:3px 0 8px}.route-copy{font-size:11px;color:var(--gold);font-weight:800}.names{font-size:11px;line-height:1.45;overflow-wrap:anywhere;margin-top:4px}.panel button{margin-top:10px;border:0;border-radius:8px;background:#eee7f9;color:#5b45ae;padding:10px 13px;font-weight:800}
.foot{color:var(--muted);font-size:11px;margin:11px 2px}.counts{color:#bfc7c7;text-align:center;font-size:11px;margin-bottom:30px}
</style>
<div class="intro"><h1>World Map · authored connections</h1><p>Offline composition preview generated from the current Unity region catalog. Tap a region to inspect the shortest route. Terrain and graph data are project assets; this is not a Unity capture. Live richness, controllers and alerts are intentionally absent because the current API does not supply them.</p></div>
<div class="phone"><div class="eyebrow">The Frontier</div><div class="title">World Map</div><div class="card"><h2>The three reaches</h2><div class="here">ARK POSITION · HOLDFAST</div>
<div class="viewport" id="atlas"><div class="ring inner"></div><div class="ring mid"></div><div class="ring outer"></div><svg id="edges" viewBox="0 0 280 280"></svg></div>
<div class="legend">A · Ark &nbsp; □ · reach gate<br>I–III · lanes &nbsp; 1–N · route hops</div>
<div class="panel"><strong id="name">Holdfast</strong><p id="detail">Your Ark is here · 1 lane</p><div class="route-copy" id="summary">ARK POSITION</div><div class="names" id="route">Your journey begins here.</div><button id="action">Open region</button></div></div>
<div class="card"><h2>From the Ark</h2><p class="foot">Tap a marker to trace its borders. The bright path uses the shortest travel time; brass links cross reach gates.</p></div></div>
<div class="counts">30 regions · 43 borders · 8 reach gates · source-linked review</div>
<script>
const nodes=__NODES__, gates=__GATES__, byId=new Map(nodes.map(n=>[n.id,n]));
const gateSet=new Set(gates.map(g=>g.slice().sort().join('|'))), grouped=['Inner','Mid','Outer'].map(b=>nodes.filter(n=>n.band===b));
const positions=new Map();grouped.forEach((group,band)=>group.forEach((node,i)=>{const a=i*2*Math.PI/group.length-Math.PI/2,r=[43,84,122][band];positions.set(node.id,{x:140+Math.cos(a)*r,y:140+Math.sin(a)*r})}));
const edgeSvg=document.querySelector('#edges'),atlas=document.querySelector('#atlas'),links=new Map(),dots=new Map();
for(const node of nodes)for(const other of node.neighbours){if(node.id>=other)continue;const key=[node.id,other].join('|'),a=positions.get(node.id),b=positions.get(other),line=document.createElementNS('http://www.w3.org/2000/svg','line');line.setAttribute('x1',a.x);line.setAttribute('y1',a.y);line.setAttribute('x2',b.x);line.setAttribute('y2',b.y);line.classList.add('edge');if(gateSet.has(key))line.classList.add('gate');edgeSvg.append(line);links.set(key,line)}
for(const node of nodes){const p=positions.get(node.id),button=document.createElement('button');button.className='dot'+(node.id==='holdfast'?' here':'')+(node.neighbours.some(n=>gateSet.has([node.id,n].sort().join('|')))?' gate':'');button.style.left=(p.x-16)+'px';button.style.top=(p.y-16)+'px';button.title=node.name+' · '+node.lanes+' lane'+(node.lanes===1?'':'s');button.onclick=()=>select(node.id);atlas.append(button);dots.set(node.id,button)}
function shortest(target){const distance=new Map([['holdfast',0]]),previous=new Map(),remaining=new Set(nodes.map(n=>n.id));while(remaining.size){let current=null,best=Infinity;for(const id of remaining){const d=distance.get(id)??Infinity;if(d<best){best=d;current=id}}if(!current)break;remaining.delete(current);if(current===target)break;for(const neighbour of byId.get(current).neighbours){const cost=best+(gateSet.has([current,neighbour].sort().join('|'))?50:25);if(cost<(distance.get(neighbour)??Infinity)){distance.set(neighbour,cost);previous.set(neighbour,current)}}}const path=[];for(let id=target;id;id=previous.get(id)){path.unshift(id);if(id==='holdfast')break}return {path,minutes:distance.get(target)}}
function travel(minutes){return minutes<60?minutes+' min away':Math.floor(minutes/60)+' h '+(minutes%60?minutes%60+' min ':'')+'away'}
function select(id){const node=byId.get(id),{path,minutes}=shortest(id),step=new Map(path.map((p,i)=>[p,i]));for(const [key,line] of links){const [a,b]=key.split('|');line.classList.toggle('route',step.has(a)&&step.has(b)&&Math.abs(step.get(a)-step.get(b))===1)}for(const [name,dot] of dots){const region=byId.get(name);dot.textContent=name==='holdfast'?'A':step.has(name)?step.get(name):['I','II','III'][region.lanes-1];dot.classList.toggle('route',step.has(name)&&name!=='holdfast');dot.classList.toggle('selected',name===id);dot.classList.toggle('muted',!step.has(name))}document.querySelector('#name').textContent=node.name;document.querySelector('#detail').textContent=(id==='holdfast'?'Your Ark is here':'From Holdfast')+' · '+node.lanes+' lane'+(node.lanes===1?'':'s');document.querySelector('#summary').textContent=id==='holdfast'?'ARK POSITION':(path.length-1)+' HOPS · '+travel(minutes).toUpperCase();document.querySelector('#route').textContent=id==='holdfast'?'Your journey begins here.':path.map(p=>byId.get(p).name).join('  ›  ');document.querySelector('#action').textContent=id==='holdfast'?'Open region':'View region'}
select('holdfast');
</script></html>'''

OUTPUT.write_text(page.replace("__NODES__", json.dumps(nodes)).replace("__GATES__", json.dumps(gates)))
print(f"Wrote {OUTPUT.relative_to(ROOT)} from {len(nodes)} regions and {len(edges)} edges")
