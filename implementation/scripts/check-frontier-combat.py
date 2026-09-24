#!/usr/bin/env python3
"""Compile/run actual combat + Frontier observer sources using local Mono/Roslyn.
This checks simulation integration; it does not compile Unity UI, meshes or shaders.
"""
import argparse
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
runtime = Path('/Library/Frameworks/Mono.framework/Versions/Current')
parser.add_argument('--mono', default=str(runtime / 'Commands/mono'))
parser.add_argument('--csc', default=str(runtime / 'lib/mono/4.5/csc.exe'))
args = parser.parse_args()
if not Path(args.mono).is_file() or not Path(args.csc).is_file():
    parser.error('Mono/Roslyn not found; pass --mono and --csc for your installation.')
root = Path(__file__).resolve().parents[2]
sources = sorted((root / 'engine/Runtime').rglob('*.cs'))
sources += [root / name for name in (
    'client/Assets/View/WaveClock.cs',
    'client/Assets/View/WaveSnapshot.cs',
    'client/Assets/Frontier/FrontierFormation.cs',
    'client/Assets/View/PresentationCues.cs',
    'implementation/tools/FrontierCombatCheck.cs',
)]
with tempfile.TemporaryDirectory(prefix='broodline-frontier-') as temp:
    executable = Path(temp) / 'FrontierCombatCheck.exe'
    subprocess.run([args.mono, args.csc, '-nologo', '-langversion:preview',
                    '-out:' + str(executable), *map(str, sources)], check=True)
    subprocess.run([args.mono, str(executable)], check=True)
