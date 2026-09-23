#!/usr/bin/env python3
"""Export actual before/after C# meshes to an offline, interactive geometry preview.

Requires Mono/Roslyn and Git history. Does not validate Unity, shaders or animation.
The baseline is the last personality-only pass, not an artist's concept image.
"""
import argparse
from pathlib import Path
import subprocess
import tempfile

root = Path(__file__).resolve().parents[2]
runtime = Path('/Library/Frameworks/Mono.framework/Versions/Current')
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--mono', default=str(runtime / 'Commands/mono'))
parser.add_argument('--csc', default=str(runtime / 'lib/mono/4.5/csc.exe'))
parser.add_argument('--baseline', default='ae35c7c')
parser.add_argument('--output', type=Path, default=root / 'implementation/results/frontier/vetch-redesign.html')
args = parser.parse_args()

def method(source, name):
    start = source.index('        static ' + name)
    opening = source.index('{', start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

old = subprocess.check_output(['git', 'show', args.baseline + ':client/Assets/Frontier/Art/FrontierArt.cs'], cwd=root, text=True)
source = '''using UnityEngine;
namespace Broodline.Frontier { public static class PreviousVetch {
static Color Hex(string v) { ColorUtility.TryParseHtmlString(v,out var c);return c; }
static readonly Color Cream=Hex("#f4dfb9"),Ink=Hex("#253345"),Gold=Hex("#c99a49"),Teal=Hex("#6ba7c0");
static readonly Vector3[] GroundBones={Vector3.zero,new Vector3(.5f,.47f,0),new Vector3(.32f,.24f,.32f),new Vector3(.32f,.24f,-.32f),new Vector3(-.34f,.24f,.32f),new Vector3(-.34f,.24f,-.32f)};
'''
source += '\n'.join(method(old, name) for name in ('void Vetch(', 'void Eyes(', 'void FaceDetails(', 'Vector3 FacePoint(', 'void Smile('))
source += '\npublic static void Build(FrontierMesh b) { Vetch(b); } }}'
with tempfile.TemporaryDirectory(prefix='frontier-geometry-') as temp:
    temp = Path(temp)
    baseline = temp / 'PreviousVetch.cs'
    baseline.write_text(source)
    executable = temp / 'Export.exe'
    subprocess.run([args.mono, args.csc, '-nologo', '-langversion:preview', '-r:System.Numerics.dll',
                    '-r:System.Web.Extensions.dll', '-out:' + str(executable), str(baseline),
                    str(root / 'implementation/tools/FrontierGeometryExport.cs'),
                    str(root / 'implementation/tools/FrontierUnityMath.cs'),
                    *map(str, [root / ('client/Assets/Frontier/' + name + '.cs') for name in ['FrontierRigDefinition','FrontierFace','FrontierEmber','FrontierPale','FrontierSkitter','FrontierHollow','FrontierLoam']]),
                    str(root / 'client/Assets/Frontier/FrontierMesh.cs'),
                    str(root / 'client/Assets/Frontier/FrontierParts.cs'),
                    str(root / 'client/Assets/Frontier/Art/FrontierVetch.cs')], check=True)
    geometry = temp / 'geometry.json'
    subprocess.run([args.mono, str(executable), str(geometry)], check=True)
    template = (root / 'implementation/tools/frontier-geometry-preview.html').read_text()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(template.replace('/*MESH_DATA*/[]', geometry.read_text()))
    print(args.output)
