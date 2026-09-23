#!/usr/bin/env python3
"""Create and verify a source handoff for the Frontier proof's current changes."""
from pathlib import Path
import subprocess
import tempfile
import zipfile

root = Path(__file__).resolve().parents[2]


def git(*args, **kwargs):
    return subprocess.run(['git', *args], cwd=root, stdout=subprocess.PIPE,
                          stderr=subprocess.PIPE, **kwargs)


base = git('rev-parse', 'HEAD', check=True).stdout.decode().strip()
files = [
    'client/Assets/Editor/TestHarness/EditModeRunner.cs',
    'client/Assets/Editor/TestHarness/Broodline.TestHarness.asmdef',
    'client/Assets/Frontier.meta',
    'client/Assets/Editor/FrontierProofBuilder.cs',
    'client/Assets/Editor/FrontierProofBuilder.cs.meta',
    'implementation/scripts/build-frontier-proof.sh',
    'implementation/scripts/check-frontier-combat.py',
    'implementation/scripts/package-frontier-proof.py',
    'implementation/tools/FrontierCombatCheck.cs',
    'implementation/2026-09-23-frontier-proof.md',
    'specs/plans/broodline_visual_upgrade_plan.md',
]
for folder in ('client/Assets/Frontier', 'specs/Designs/visual-upgrade-mockups-v1'):
    files += [str(p.relative_to(root)) for p in sorted((root / folder).rglob('*'))
              if p.is_file() and 'Generated' not in p.relative_to(root / folder).parts]
files = sorted(set(files))
patch = bytearray()
with tempfile.TemporaryDirectory(prefix='frontier-package-check-') as temp:
    for name in files:
        original = git('show', base + ':' + name)
        if original.returncode == 0:
            destination = Path(temp) / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(original.stdout)
            patch += git('diff', '--binary', base, '--', name, check=True).stdout
        else:
            diff = git('diff', '--no-index', '--binary', '--', '/dev/null', name)
            if diff.returncode != 1:
                raise RuntimeError('Could not package ' + name + ': ' + diff.stderr.decode())
            patch += diff.stdout
    if not patch:
        raise RuntimeError('No changes relative to HEAD; sync the committed repository instead.')
    for flags in (['--check'], []):
        subprocess.run(['git', 'apply', *flags, '--whitespace=error-all', '-'],
                       cwd=temp, input=patch, check=True)
    for name in files:
        if (Path(temp) / name).read_bytes() != (root / name).read_bytes():
            raise RuntimeError('Patch round-trip mismatch: ' + name)

readme = f'''Broodline Living Frontier - source handoff, second presentation pass
Base commit: {base}

Includes the proof source, reproducible combat checks, visual plan, and approved
concept references. The PNGs are concept art, not runtime screenshots. This is
not a compiled or Unity-verified build.

Extract this ZIP outside your repository. From a compatible checkout's root:

  git apply --check /path/to/frontier-proof.patch
  git apply /path/to/frontier-proof.patch

Run the second command only if the first succeeds. This is a complete patch
from the base commit above, not an incremental patch over the first proof ZIP.
If an earlier package is already applied, sync the updated working tree or
resolve the overlapping changes; do not force the patch over existing work.

Open client/ in Unity 6000.6.0f1 and allow import to finish. Select:
  Broodline > Frontier Proof > Open
Set Game view to 430 x 932 and press Play.

Read implementation/2026-09-23-frontier-proof.md for the full validation guide.
The package round-trip matched all {len(files)} selected files byte for byte.
Unity compilation, rendering, layout and device profiling are still pending.
'''
output = root / 'implementation/results/frontier/broodline-frontier-proof-source.zip'
output.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as archive:
    archive.writestr('README.txt', readme)
    archive.writestr('frontier-proof.patch', patch)
    archive.write(root / 'implementation/2026-09-23-frontier-proof.md', 'validation-guide.md')
with zipfile.ZipFile(output) as archive:
    if archive.testzip() is not None:
        raise RuntimeError('ZIP integrity check failed')
print(f'PASS: {len(files)} files round-tripped; strict whitespace and ZIP checks passed.\n{output}')
