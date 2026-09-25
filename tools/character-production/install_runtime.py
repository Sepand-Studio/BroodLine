"""Install the mesh candidates at FrontierArt's runtime resource paths.

The editable meshes remain in ProductionCandidates. Unity's ScriptedImporter
turns each copied .frontiermesh into a GameObject with embedded mesh/material.
"""
import json
import shutil
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / 'client/Assets/Frontier'
SOURCE = ASSETS / 'ProductionCandidates'
RUNTIME = ASSETS / 'Resources/Frontier'
IMPORTER_META = ROOT / 'client/Assets/Editor/FrontierMeshImporter.cs.meta'
BODIES = ('vetch', 'ember', 'skitter', 'hollow', 'loam', 'pale')
PARTS = ('carapace', 'taunt', 'cinder', 'splash', 'sprint', 'litter',
         'reach', 'pierce', 'regrow', 'burrow', 'screen', 'chill')


def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'broodline/frontier/runtime/' + path.as_posix()).hex


def folder(path):
    path.mkdir(parents=True, exist_ok=True)
    meta = path.with_name(path.name + '.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: ' + guid(path.relative_to(ASSETS))
                        + '\nfolderAsset: yes\nDefaultImporter:\n'
                        + '  externalObjects: {}\n  userData:\n'
                        + '  assetBundleName:\n  assetBundleVariant:\n')


def install(source, target, expected_kind, importer_guid):
    data = json.loads(source.read_text())
    if data['id'] != target.stem or data['kind'] != expected_kind or data['version'] != 1:
        raise ValueError('Unexpected mesh identity: ' + str(source))
    shutil.copyfile(source, target)
    target.with_name(target.name + '.meta').write_text(
        'fileFormatVersion: 2\nguid: ' + guid(target.relative_to(ASSETS))
        + '\nScriptedImporter:\n  internalIDToNameTable: []\n'
        + '  externalObjects: {}\n  serializedVersion: 2\n'
        + '  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
        + '  script: {fileID: 11500000, guid: ' + importer_guid + ', type: 3}\n')


def main():
    importer_guid = next(line.partition(': ')[2].strip() for line in IMPORTER_META.read_text().splitlines()
                         if line.startswith('guid: '))
    for directory in (ASSETS / 'Resources', RUNTIME, RUNTIME / 'Creatures', RUNTIME / 'Parts'):
        folder(directory)
    for name in BODIES:
        folder_name = 'Vetch' if name == 'vetch' else 'Founders'
        install(SOURCE / folder_name / (name + '.frontiermesh'),
                RUNTIME / 'Creatures' / (name + '.frontiermesh'), 'body', importer_guid)
    for name in PARTS:
        folder_name = 'Vetch' if name in ('carapace', 'cinder') else 'Traits'
        install(SOURCE / folder_name / (name + '.frontiermesh'),
                RUNTIME / 'Parts' / (name + '.frontiermesh'), 'part', importer_guid)
    print('Installed 6 founder bodies and 12 independent trait parts for Resources.Load<GameObject>.')


if __name__ == '__main__':
    main()
