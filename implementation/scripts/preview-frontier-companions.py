#!/usr/bin/env python3
"""Export and audit the six actual companion builders. Unity validation is separate."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile

root = Path(__file__).resolve().parents[2]
runtime = Path('/Library/Frameworks/Mono.framework/Versions/Current')
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--mono', default=str(runtime / 'Commands/mono'))
parser.add_argument('--csc', default=str(runtime / 'lib/mono/4.5/csc.exe'))
parser.add_argument('--output', type=Path, default=root / 'implementation/results/frontier/companions.html')
parser.add_argument('--production-baselines', type=Path, default=root / 'implementation/results/frontier/production-baselines')
args = parser.parse_args()
baseline_ref = 'cef49fa'  # Freeze the reviewed Vetch and pre-redesign Ember/Pale.
old = subprocess.check_output(['git', 'show', baseline_ref + ':client/Assets/Frontier/FrontierArt.cs'], cwd=root, text=True)
def method(name):
    start = old.index('        static ' + name)
    end = old.index('{', start) + 1
    depth = 1
    while depth:
        depth += (old[end] == '{') - (old[end] == '}')
        end += 1
    return old[start:end]

baseline = '''using UnityEngine;
namespace Broodline.Frontier { public static class PreviousCompanions {
static Color Hex(string v) { ColorUtility.TryParseHtmlString(v,out var c);return c; }
static readonly Color Cream=Hex("#f4dfb9"),Ink=Hex("#253345"),Gold=Hex("#c99a49"),Coral=Hex("#e5867a"),Frost=Hex("#c6cede");
static readonly Vector3[] EmberBones={Vector3.zero,new Vector3(.16f,1.01f,0),new Vector3(0,.26f,.16f),new Vector3(0,.26f,-.16f),new Vector3(.15f,.77f,.19f),new Vector3(.15f,.77f,-.19f)};
'''
baseline += '\n'.join(method(n) for n in ['void Ember(', 'void Pale(', 'void Eyes(', 'void FaceDetails(', 'Vector3 FacePoint(', 'void Smile('])
baseline += '\npublic static void Build(FrontierMesh b,string id) { if(id=="ember")Ember(b);else Pale(b); } }}'
vetch = subprocess.check_output(['git', 'show', baseline_ref + ':client/Assets/Frontier/FrontierVetch.cs'], cwd=root, text=True).replace('class FrontierVetch', 'class BaselineVetch')
names = ['FrontierMesh', 'FrontierRigDefinition', 'FrontierPose', 'FrontierFace', 'FrontierParts', 'FrontierVetch', 'FrontierEmber', 'FrontierPale', 'FrontierSkitter', 'FrontierHollow', 'FrontierLoam']
with tempfile.TemporaryDirectory(prefix='frontier-companions-') as temp:
    temp = Path(temp)
    (temp / 'Baseline.cs').write_text(baseline)
    (temp / 'BaselineVetch.cs').write_text(vetch)
    executable = temp / 'Export.exe'
    subprocess.run([args.mono, args.csc, '-nologo', '-langversion:preview', '-r:System.Numerics.dll', '-r:System.Web.Extensions.dll',
                    '-out:' + str(executable), str(temp / 'Baseline.cs'), str(temp / 'BaselineVetch.cs'),
                    str(root / 'implementation/tools/FrontierUnityMath.cs'), str(root / 'implementation/tools/FrontierCompanionExport.cs'),
                    *[str(root / ('client/Assets/Frontier/' + n + '.cs')) for n in names]], check=True)
    geometry = temp / 'cast.json'
    subprocess.run([args.mono, str(executable), str(geometry)], check=True)
    cast = json.loads(geometry.read_text())
    for row in cast['species']:
        path = args.production_baselines / (row['id'] + '-baseline.json')
        if row['baseline'] is None and path.is_file():
            row['baseline'] = json.loads(path.read_text())
            row['baselineLabel'] = 'Production prefab baseline (Unity export)'
    args.output.parent.mkdir(parents=True, exist_ok=True)
    template = (root / 'implementation/tools/frontier-companions-preview.html').read_text()
    args.output.write_text(template.replace('/*CAST_DATA*/{}', json.dumps(cast, separators=(',', ':'))))
    print(args.output)
