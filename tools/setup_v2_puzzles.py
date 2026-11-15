"""Duplicate the current user-modified exploration scene, changing only its host.

The composition is scene-local, built on Play using imported mesh bounds. No
team FBX, existing script, original scene, title or Build Settings is modified.
"""
from pathlib import Path
import hashlib
import json
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
SCENE = ROOT / 'Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity'
SOURCE = ROOT / 'Assets/Scenes/Everflow_Map_v2_test.unity'
REPORTS = ROOT / 'docs/v2-puzzle-validation'


def meta(path, scene=False):
    target = Path(str(path) + '.meta')
    if target.exists():
        return re.search(r'^guid: (\w+)', target.read_text(), re.M)[1]
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'everflow-v23-puzzle/' + path.relative_to(ROOT).as_posix()).hex
    importer = 'DefaultImporter' if scene else 'MonoImporter'
    data = f'fileFormatVersion: 2\nguid: {guid}\n{importer}:\n  externalObjects: {{}}\n'
    if not scene:
        data += '  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n'
    data += '  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    target.write_text(data, encoding='utf-8', newline='\n')
    return guid


def main():
    REPORTS.mkdir(parents=True, exist_ok=True)
    for folder in ('Runtime', 'Editor'):
        for p in (ROOT / 'Assets/EverflowMap' / folder).glob('EFV2*.cs'):
            meta(p)
    script = ROOT / 'Assets/EverflowMap/Runtime/EFV2PuzzleWorld.cs'
    guid = meta(script)
    if SCENE.exists():
        print('Preserved existing puzzle scene; no overwrite.')
        return
    text = SOURCE.read_text(encoding='utf-8-sig')
    old = re.search(r'  m_Script: \{fileID: 11500000, guid: (\w+), type: 3\}\n  m_Name: ?\n  m_EditorClassIdentifier: Assembly-CSharp::EverflowMap.EFMapExploration', text)
    assert old, 'Expected current exploration host'
    text = text[:old.start()] + old[0].replace(old[1], guid).replace('::EverflowMap.EFMapExploration', '::EverflowMap.EFV2PuzzleWorld') + text[old.end():]
    text = text.replace('m_Name: V2 Exploration\n', 'm_Name: EVERFLOW V2.3 Puzzle World\n')
    SCENE.write_text(text, encoding='utf-8', newline='\n')
    new_guid = meta(SCENE, scene=True)
    source_guid = re.search(r'^guid: (\w+)', Path(str(SOURCE)+'.meta').read_text(), re.M)[1]
    assert new_guid != source_guid
    (REPORTS/'scene-copy.json').write_text(json.dumps({
        'source': SOURCE.relative_to(ROOT).as_posix(), 'source_sha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
        'scene': SCENE.relative_to(ROOT).as_posix(), 'source_guid': source_guid, 'puzzle_guid': new_guid,
        'strategy': 'Independent scene asset; existing FBX/player/camera instance content retained; exploration host replaced with scene-local v2 puzzle composition.',
        'actual_unity_open_or_play': False,
    }, indent=2), encoding='utf-8')
    print('Created', SCENE.relative_to(ROOT), 'GUID', new_guid)


if __name__ == '__main__':
    main()
