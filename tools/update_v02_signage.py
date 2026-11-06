"""Surgical text/presentation-only migration of the saved Unity YAML scene.

No scene builder, gameplay object, collider or gameplay component is regenerated.
Idempotent: exits if the five versioned board components already exist. The Editor
equivalent is LabSignageSetup.RefreshOpen. Original text IDs used by live status
writers are retained and moved into the boards.
"""
from pathlib import Path
import copy
import datetime
import hashlib
import json
import math
import re
import shutil
import uuid

ROOT = Path(__file__).resolve().parents[1]
SCENE = ROOT / "Assets/Scenes/Everflow_V02_PuzzleLab.unity"
PRESENTATION = ROOT / "Assets/EverflowPuzzleLab/Presentation"


def meta(path, folder=False):
    path = Path(path)
    target = Path(str(path) + ".meta")
    if target.exists():
        return re.search(r"guid: (\w+)", target.read_text())[1]
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "EVERFLOW:" + path.relative_to(ROOT).as_posix()).hex
    if folder:
        content = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
    elif path.suffix == ".cs":
        content = "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n"
    elif path.suffix == ".mat":
        content = "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n"
    else:
        content = "TextScriptImporter:\n  externalObjects: {}\n"
    target.write_text(f"fileFormatVersion: 2\nguid: {guid}\n" + content, encoding="utf-8")
    return guid


def parse(text):
    return {int(i): [int(k), body] for k, i, body in re.findall(r"^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)", text, re.M | re.S)}


def field(body, key):
    return re.search(r"^  " + re.escape(key) + r": (.*)$", body, re.M)[1]


def reference(body, key):
    return int(re.search(r"fileID: (-?\d+)", field(body, key))[1])


def scalar(body, key, value):
    return re.sub(r"^  " + re.escape(key) + r": .*?$", "  " + key + ": " + str(value), body, count=1, flags=re.M)


def vector(values, keys="xyz"):
    return "{" + ", ".join(f"{k}: {v:.7g}" for k, v in zip(keys, values)) + "}"


def decode_vector(body, key):
    return [float(x) for x in re.findall(r": (-?[\d.eE+]+)", field(body, key))]


def update():
    original = SCENE.read_bytes()
    text = original.decode("utf-8-sig").replace("\r\n", "\n")
    docs = parse(text)
    before = copy.deepcopy(docs)
    config = json.loads((PRESENTATION / "Signage.json").read_text(encoding="utf-8"))
    for source in (ROOT / "Assets/EverflowPuzzleLab").rglob("*.cs"):
        meta(source)
    meta(PRESENTATION, True)
    meta(PRESENTATION / "Signage.json")
    board_guid = meta(ROOT / "Assets/EverflowPuzzleLab/Runtime/LabInfoBoard.cs")
    short_guid = meta(ROOT / "Assets/EverflowPuzzleLab/Runtime/LabShortLabel.cs")
    if sum(board_guid in b for k, b in docs.values() if k == 114) == 5:
        print("UNCHANGED: five existing boards; no scene or duplicate objects written.")
        return
    backup = ROOT / "Backups" / ("PuzzleLab-Signage-" + datetime.datetime.now().strftime("%Y%m%d-%H%M%S"))
    backup.mkdir(parents=True)
    shutil.copy2(SCENE, backup / SCENE.name)
    next_id = 8700000000

    def allocate():
        nonlocal next_id
        next_id += 1
        assert next_id not in docs
        return next_id

    go_to_transform = {reference(b, "m_GameObject"): i for i, (k, b) in docs.items() if k == 4}
    text_templates = {k: None for k in (1, 4, 23, 102)}
    tm_id = next(i for i, (k, b) in docs.items() if k == 102)
    template_go = reference(docs[tm_id][1], "m_GameObject")
    text_templates[1] = template_go
    for i, (k, b) in docs.items():
        if k in (4, 23, 102) and reference(b, "m_GameObject") == template_go:
            text_templates[k] = i
    mesh_id = next(i for i, (k, b) in docs.items() if k == 33 and "fileID: 10202," in b)
    cube_go = reference(docs[mesh_id][1], "m_GameObject")
    cube_templates = {1: cube_go, 33: mesh_id}
    for i, (k, b) in docs.items():
        if k in (4, 23) and reference(b, "m_GameObject") == cube_go:
            cube_templates[k] = i

    def children(transform):
        b = docs[transform][1]
        chunk = re.search(r"  m_Children:(.*?)(?=  m_Father:)", b, re.S)[1]
        return [int(x) for x in re.findall(r"fileID: (\d+)", chunk)]

    def set_children(transform, values):
        chunk = "  m_Children:" + ("\n" + "".join(f"  - {{fileID: {i}}}\n" for i in values) if values else " []\n")
        docs[transform][1] = re.sub(r"  m_Children:.*?(?=  m_Father:)", lambda _: chunk, docs[transform][1], flags=re.S)

    def reparent(transform, parent):
        old = reference(docs[transform][1], "m_Father")
        if old:
            set_children(old, [i for i in children(old) if i != transform])
        docs[transform][1] = scalar(docs[transform][1], "m_Father", f"{{fileID: {parent}}}")
        set_children(parent, children(parent) + [transform])

    def pose(transform, pos, scale=(1, 1, 1)):
        for key, value in [("m_LocalPosition", vector(pos)), ("m_LocalScale", vector(scale)), ("m_LocalRotation", vector((0, 0, 0, 1), "xyzw"))]:
            docs[transform][1] = scalar(docs[transform][1], key, value)

    def clone(templates, name, parent, pos=(0, 0, 0), scale=(1, 1, 1)):
        ids = {k: allocate() for k in templates}
        mapping = {old: ids[k] for k, old in templates.items()}
        for k, old in templates.items():
            body = re.sub(r"fileID: (-?\d+)", lambda m: "fileID: " + str(mapping.get(int(m[1]), int(m[1]))), before[old][1])
            docs[ids[k]] = [k, body]
        go = ids[1]
        body = docs[go][1]
        body = re.sub(r"  m_Component:.*?(?=  m_Layer:)", lambda _: "  m_Component:\n" + "".join(f"  - component: {{fileID: {v}}}\n" for k, v in ids.items() if k != 1), body, flags=re.S)
        body = scalar(body, "m_Name", json.dumps(name, ensure_ascii=False))
        body = scalar(scalar(body, "m_Layer", 2), "m_IsActive", 1)
        docs[go][1] = body
        docs[ids[4]][1] = scalar(docs[ids[4]][1], "m_Father", "{fileID: 0}")
        set_children(ids[4], [])
        pose(ids[4], pos, scale)
        if parent:
            reparent(ids[4], parent)
        if 23 in ids:
            for key in ("m_CastShadows", "m_ReceiveShadows", "m_DynamicOccludee"):
                docs[ids[23]][1] = scalar(docs[ids[23]][1], key, 0)
        return ids

    def group(name, parent, pos=(0, 0, 0)):
        return clone({1: template_go, 4: text_templates[4]}, name, parent, pos)[4]

    def mono(go, guid, name, fields):
        i = allocate()
        docs[i] = [114, f"MonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: Assembly-CSharp::EverflowPuzzleLab.{name}\n" + fields]
        docs[go][1] = re.sub(r"(?=  m_Layer:)", f"  - component: {{fileID: {i}}}\n", docs[go][1], count=1)
        return i

    def text_style(i, content, size, color, anchor=0):
        b = docs[i][1]
        b = re.sub(r"  m_Text:.*?(?=  m_OffsetZ:)", lambda _: "  m_Text: " + json.dumps(content, ensure_ascii=False) + "\n", b, flags=re.S)
        for key, value in [("m_FontSize", size), ("m_CharacterSize", .1), ("m_LineSpacing", .85), ("m_Anchor", anchor), ("m_Alignment", 0 if anchor == 0 else 1), ("m_RichText", 0)]:
            b = scalar(b, key, value)
        rgba = sum(round(v * 255) << (8 * n) for n, v in enumerate(color))
        b = re.sub(r"rgba: \d+", "rgba: " + str(rgba), b)
        docs[i][1] = b

    def make_text(parent, name, content, pos, size, color, reuse=None):
        if reuse is None:
            ids = clone(text_templates, name, parent, pos)
        else:
            go = reference(docs[reuse][1], "m_GameObject")
            ids = {1: go, 4: go_to_transform[go], 102: reuse, 23: next(i for i, (k, b) in docs.items() if k == 23 and reference(b, "m_GameObject") == go)}
            reparent(ids[4], parent)
            pose(ids[4], pos)
            docs[go][1] = scalar(scalar(docs[go][1], "m_Name", json.dumps(name)), "m_Layer", 2)
        text_style(ids[102], content, size, color)
        return ids

    mat_guids = {}
    material_template = (ROOT / "Assets/EverflowPuzzleLab/Materials/Lab stone.mat").read_text(encoding="utf-8-sig")
    for name, color in [("Sign dark", (.035, .065, .09)), ("Sign header", (.055, .12, .16)), ("Sign rule", (.23, .45, .52))]:
        path = PRESENTATION / (name + ".mat")
        mat_guids[name] = meta(path)
        if not path.exists():
            material = material_template.replace("m_Name: Lab stone", "m_Name: " + name).replace("933532a4fcc9baf4fa0491de14d08ed7", "650dd9526735d5b46b79224bc6e94025")
            material = re.sub(r"(_BaseColor|_Color): \{[^}]+\}", lambda m: m[1] + ": {r: %g, g: %g, b: %g, a: 1}" % color, material)
            path.write_text(material, encoding="utf-8")

    areas = [i for i, (k, b) in docs.items() if k == 114 and "::EverflowPuzzleLab.LabArea\n" in b]
    areas.sort(key=lambda i: (4 if field(docs[i][1], "title").startswith("D1") else int(field(docs[i][1], "title")[1:3]) - 1))
    board_count = 0
    white, muted, aux, green = (1, 1, 1, 1), (.52, .72, .78, 1), (.74, .82, .87, 1), (.65, .88, .78, 1)
    for entry, area_id in zip(config["boards"], areas):
        area_body = docs[area_id][1]
        area_go = reference(area_body, "m_GameObject")
        area_transform = go_to_transform[area_go]
        # Descendant discovery uses the original hierarchy, not naming/scene regeneration.
        descendants = set()
        def walk(t):
            descendants.add(t)
            for child in children(t): walk(child)
        walk(area_transform)
        old_texts = [i for i, (k, b) in list(docs.items()) if k == 102 and go_to_transform.get(reference(b, "m_GameObject")) in descendants]
        presentation = group("Presentation", area_transform)
        archive = group("Legacy Text (inactive)", presentation)
        docs[reference(docs[archive][1], "m_GameObject")][1] = scalar(docs[reference(docs[archive][1], "m_GameObject")][1], "m_IsActive", 0)
        labels = group("Short Labels", presentation)
        board = group("Board " + entry["key"], presentation, entry["position"])
        w, h = entry["size"]
        def box(name, p, size, mat):
            ids = clone(cube_templates, name, board, p, size)
            docs[ids[23]][1] = re.sub(r"  m_Materials:.*?(?=  m_StaticBatchInfo:)", lambda _: f"  m_Materials:\n  - {{fileID: 2100000, guid: {mat_guids[mat]}, type: 2}}\n", docs[ids[23]][1], flags=re.S)
            return ids[23]
        background_renderer=box("Background", (0, 0, 0), (w, h, .04), "Sign dark")
        box("Title band", (0, h / 2 - .38, -.027), (w - .04, .72, .01), "Sign header")
        accent_renderer=box("Left accent", (-w / 2 + .04, 0, -.027), (.05, h - .06, .01), "Sign rule")
        x = -w / 2 + .3
        underground = entry["index"] == 4
        gy, my, cy, sy = (2.2, 1.55, -.65, -1.86) if underground else (1.7, .82, -.80, -1.80)
        section_specs = [("01 Title", entry["title"], h / 2 - .22, 36, white), ("02 Goal heading", "목표", gy, 16, muted), ("03 Goal", entry["goal"], gy - .25, 24, (.9, .94, .96, 1)), ("04 Methods heading", "테스트 방법", my, 16, muted), ("05 Methods", entry["methods"], my - .25, 22, (.9, .94, .96, 1)), ("06 Controls heading", "조작 · 실패 복구", cy, 16, muted), ("07 Controls", entry["controls"], cy - .23, 18, aux), ("08 Status heading", "현재 상태", sy, 16, muted)]
        section_ids = {}
        detail_renderers = [background_renderer,accent_renderer]
        for name, content, y, size, color in section_specs:
            ids = make_text(board, name, content, (x, y, -.06), size, color)
            section_ids[name] = ids[102]
            if name != "01 Title": detail_renderers.append(ids[23])
        sign_id = reference(area_body, "sign")
        ids = make_text(board, "09 Area status", "복원 진행 중" if underground else "진행 중 · 목표 미도달", (x, sy - .22, -.06), 18, green, sign_id)
        detail_renderers.append(ids[23])
        circuit_id = 0
        if underground:
            circuit_id = reference(docs[reference(area_body, "canal")][1], "readout")
            ids = make_text(board, "10 Circuit status", config["statusPreview"], (x, -2.34, -.06), 18, green, circuit_id)
            detail_renderers.append(ids[23])
        detail_renderers.append(box("Status divider", (0, sy + .09, -.035), (w - .6, .012, .01), "Sign rule"))
        fields = f"  area: {{fileID: {area_id}}}\n"
        for key, name in [("title", "01 Title"), ("goal", "03 Goal"), ("methods", "05 Methods"), ("controls", "07 Controls")]:
            fields += f"  {key}: {{fileID: {section_ids[name]}}}\n"
        fields += f"  status: {{fileID: {sign_id}}}\n  circuitStatus: {{fileID: {circuit_id}}}\n  detailRenderers:\n" + "".join(f"  - {{fileID: {i}}}\n" for i in detail_renderers) + "  detailDistance: 30\n"
        mono(reference(docs[board][1], "m_GameObject"), board_guid, "LabInfoBoard", fields)
        targets = {field(b, "label").strip("'"): (reference(b, "m_GameObject"), b) for i, (k, b) in docs.items() if k == 114 and "::EverflowPuzzleLab.LabTarget\n" in b and go_to_transform.get(reference(b, "m_GameObject")) in descendants}
        for old_id in old_texts:
            if old_id in (sign_id, circuit_id): continue
            go = reference(docs[old_id][1], "m_GameObject")
            transform = go_to_transform[go]
            name = field(docs[go][1], "m_Name").strip("'")
            label = next((l for l in config["labels"] if name == "Sign - " + l["source"] and l["source"] in targets), None)
            if label:
                target_go, target_body = targets[label["source"]]
                target_t = go_to_transform[target_go]
                # Existing targets are area-local, except the internal water target.
                p = decode_vector(docs[target_t][1], "m_LocalPosition")
                parent_t = reference(docs[target_t][1], "m_Father")
                if parent_t != area_transform:
                    scale = decode_vector(docs[parent_t][1], "m_LocalScale")
                    pp = decode_vector(docs[parent_t][1], "m_LocalPosition")
                    p = [pp[n] + p[n] * scale[n] for n in range(3)]
                offset = 1.15 if field(target_body, "acceptsFlow") == "1" else .55 if field(target_body, "water") == "1" else .85
                p[1] += offset
                reparent(transform, labels); pose(transform, p)
                docs[go][1] = scalar(scalar(docs[go][1], "m_Name", json.dumps("Label - " + label["source"])), "m_Layer", 2)
                text_style(old_id, label["text"], 18, (.67, .83, .86, 1), 4)
                mono(go, short_guid, "LabShortLabel", f"  area: {{fileID: {area_id}}}\n  follow: {{fileID: {target_t}}}\n  offset: {vector((0, offset, 0))}\n  visibleDistance: 9\n")
            else:
                reparent(transform, archive)
                docs[go][1] = scalar(docs[go][1], "m_IsActive", 0)
        board_count += 1
    assert board_count == 5
    # All pre-existing non-presentation components, colliders and numerical poses remain byte-for-byte.
    text_gos = {reference(b, "m_GameObject") for k, b in before.values() if k == 102}
    pose_checks = 0
    for i, (k, old) in before.items():
        if k in (1, 4, 23, 102) and (i in text_gos if k == 1 else reference(old, "m_GameObject") in text_gos): continue
        if k == 4:
            for key in ("m_LocalPosition", "m_LocalRotation", "m_LocalScale", "m_Father"):
                assert field(old, key) == field(docs[i][1], key), (i, key)
            pose_checks += 1
        else:
            assert old == docs[i][1], ("non-presentation changed", i, k)
    result = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + "".join(f"--- !u!{k} &{i}\n{body}" for i, (k, body) in docs.items())
    # Validate every scene-local fileID, ignoring asset references and builtins.
    for i, (k, body) in docs.items():
        for match in re.finditer(r"\{fileID: (-?\d+)([^}]*)\}", body):
            ref = int(match[1])
            if ref and "guid:" not in match[2]: assert ref in docs, (i, ref)
    SCENE.write_text(result, encoding="utf-8", newline="\n")
    report = {"scene": str(SCENE.relative_to(ROOT)), "backup": str(backup.relative_to(ROOT)), "before_sha256": hashlib.sha256(original).hexdigest(), "after_sha256": hashlib.sha256(SCENE.read_bytes()).hexdigest(), "boards": board_count, "protected_geometry_poses": pose_checks, "gameplay_components_colliders_unchanged": True, "local_file_references": "pass", "unity_editor_play_visual": "not executed; restricted Windows account cannot reach interactive Licensing Client"}
    out = ROOT / "docs/puzzle-lab/signage-verification.json"
    out.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    update()
