"""Offline comparison with the immediate pre-network scene; not a Play verdict."""
from pathlib import Path
import datetime
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs/water-validation/network'

def parse(text):
    return {int(i): (int(k), b) for k, i, b in re.findall(
        r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)', text, re.M | re.S)}

def main():
    checks = []
    def check(ok, label):
        if not ok:
            raise AssertionError(label)
        checks.append(label)
        print('PASS', label)

    before = parse((OUT / 'before.unity.snapshot').read_text(encoding='utf-8-sig'))
    scene = ROOT / 'Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity'
    after_text = scene.read_text(encoding='utf-8-sig')
    after = parse(after_text)
    check(set(before) <= set(after), 'Every existing scene object/component fileID retained')
    physics = {54, 64, 65, 135, 136, 143}
    check(all(after[i] == v for i, v in before.items() if v[0] in physics),
          'Existing physics components byte-identical to immediate scene baseline')
    check(all(after[i] == v for i, v in before.items() if v[0] == 20),
          'Camera components byte-identical to immediate scene baseline')
    meta = (ROOT / 'Assets/EverflowMap/Runtime/EFV2WaterIntegration.cs.meta').read_text()
    water_guid = re.search(r'^guid: (\w+)', meta, re.M)[1]
    check(all(after[i] == v for i, v in before.items()
              if v[0] == 114 and water_guid not in v[1]),
          'Existing non-water MonoBehaviour configuration byte-identical')

    def prefab_values(blocks):
        values = {}
        for fid, (kind, body) in blocks.items():
            if kind != 1001:
                continue
            pattern = r'- target: (\{[^\n]+\})\n\s+propertyPath: ([^\n]+)\n\s+value: ([^\n]*)\n\s+objectReference: ([^\n]+)'
            for target, key, value, ref in re.findall(pattern, body):
                values[(fid, target, key.strip("'\""))] = (value, ref)
        return values
    old, new = prefab_values(before), prefab_values(after)
    changed = [key for key in old.keys() | new.keys() if old.get(key) != new.get(key)]
    check(all(key[2] == 'm_Mesh' or key[2].startswith('m_Materials.') or key[2] == 'm_Enabled'
              for key in changed), 'Prefab overrides change only mesh/material/renderer visibility')
    for fid, (_, body) in after.items():
        for value, tail in re.findall(r'\{fileID: (-?\d+)([^}]*)\}', body):
            check_id = int(value)
            assert not check_id or 'guid:' in tail or check_id in after, (fid, value)
    check(True, 'All local scene references resolve')
    check(after_text.count('  networkSurface: 1') == 39, 'All 39 original segment records converted')

    guids = {}
    for path in (ROOT / 'Assets').rglob('*.meta'):
        match = re.search(r'^guid: (\w+)', path.read_text(encoding='utf-8-sig', errors='replace'), re.M)
        if match:
            guids.setdefault(match[1], []).append(path)
    own = list((ROOT / 'Assets/EverflowWater/Network').rglob('*.meta'))
    own += list((ROOT / 'Assets/EverflowMap').rglob('EFWater*.cs.meta'))
    own += [ROOT / 'Assets/EverflowWater/Shaders/EF_WaterNetwork.shader.meta']
    check(all(len(guids[re.search(r'^guid: (\w+)', p.read_text(), re.M)[1]]) == 1 for p in own),
          'All network asset/script GUIDs unique')
    shader = (ROOT / 'Assets/EverflowWater/Shaders/EF_WaterNetwork.shader').read_text()
    check('_Time' not in shader and '_WaterTime' in shader and '_FlowOffset' in shader,
          'Network shader uses local pauseable phase, never global _Time')
    check('_Frozen' in shader and '_Arrival' in shader and '_Impact2' in shader,
          'Network shader supports frozen state, arrival and multiple impacts')
    result = dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  kind='Offline only; separate Play reports required', passed=True, checks=checks,
                  scene_sha256=hashlib.sha256(scene.read_bytes()).hexdigest())
    (OUT / 'offline-preservation.json').write_text(json.dumps(result, indent=2), encoding='utf-8')

if __name__ == '__main__':
    main()
