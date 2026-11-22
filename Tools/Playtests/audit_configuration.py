"""Read-only serialized-configuration audit. Does not launch Unity or modify assets."""
import json
import re
import struct
from datetime import datetime, timezone
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[2]
checks = []


def read(path):
    return (PROJECT / path).read_text(encoding="utf-8-sig")


def check(name, condition, detail=""):
    checks.append({"name": name, "passed": bool(condition), "detail": detail})


def guid(path):
    return re.search(r"^guid: (\w+)", read(str(path) + ".meta"), re.M)[1]


def array_block(text, name):
    match = re.search(r"^  " + re.escape(name) + r":\s*\n((?:  -[^\n]*\n)*)", text, re.M)
    return match[1] if match else ""


def refs(text, name):
    return re.findall(r"guid: (\w+)", array_block(text, name))


def objects(text):
    return {match[1]: match[2] for match in re.finditer(
        r"^--- !u!\d+ &(\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)", text, re.M | re.S)}


def required_component(text, script, fields):
    records = objects(text)
    script_guid = guid(script)
    matches = [body for body in records.values() if "guid: " + script_guid + "," in body]
    check(script + " component occurs once", len(matches) == 1)
    if len(matches) != 1:
        return
    for field in fields:
        match = re.search(r"^  " + re.escape(field) + r": \{fileID: (\d+)\}", matches[0], re.M)
        check(script + ": " + field, match is not None and match[1] in records)


scene = read("Assets/Prefabs/Scenes/SampleScene.unity")
npc = read("Assets/Prefabs/NPC.prefab")
boxes = {}
for path in sorted((PROJECT / "Assets/BoxConfigs").glob("*.asset")):
    relative = path.relative_to(PROJECT)
    data = read(relative)
    boxes[guid(relative)] = {"name": path.stem, "id": int(re.search(r"^  id: (\d+)", data, re.M)[1])}

shelf_refs = refs(scene, "boxDatas")
npc_refs = refs(npc, "boxDatas")
check("Seven shelves have seven valid clothing assets", len(shelf_refs) == 7 and all(g in boxes for g in shelf_refs))
check("Shelf and customer clothing index order matches", shelf_refs == npc_refs)
available_ids = {boxes[g]["id"] for g in shelf_refs if g in boxes}
check("Clothing IDs are unique", len(available_ids) == len(shelf_refs))
material_count = len(array_block(npc, "clothesMaterial").splitlines())
check("Every clothing ID fits the appearance material array", material_count > 0 and all(0 <= n < material_count for n in available_ids))

orders = {}
for path in sorted((PROJECT / "Assets/ClothesConfigs").glob("*.asset")):
    relative = path.relative_to(PROJECT)
    data = read(relative)
    encoded = re.search(r"^  canID: ([^\r\n]*)", data, re.M)
    value = encoded[1].strip() if encoded else ""
    if value.startswith("["):
        accepted = json.loads(value)
    else:
        raw = bytes.fromhex(value)
        accepted = [n[0] for n in struct.iter_unpack("<i", raw)] if len(raw) % 4 == 0 else []
    orders[guid(relative)] = {"name": path.stem, "id": int(re.search(r"^  id: (\d+)", data, re.M)[1]), "accepted": accepted}

order_refs = refs(npc, "orderDatas")
check("All fifteen customer order assets resolve", len(order_refs) == 15 and all(g in orders for g in order_refs))
check("Customer order IDs are unique", len({orders[g]["id"] for g in order_refs if g in orders}) == len(order_refs))
for order_guid in order_refs:
    if order_guid not in orders:
        continue
    order = orders[order_guid]
    check(order["name"] + " can be fulfilled", bool(order["accepted"]) and set(order["accepted"]) <= available_ids, str(order["accepted"]))

required_component(scene, "Assets/Scripts/ShopHUD.cs", ["moneyText", "timerText", "dayText"])
required_component(scene, "Assets/Scripts/ShopMenuStyle.cs", ["pausePanel", "settingsPanel", "guidePanel", "reportPanel"])
required_component(npc, "Assets/Scripts/NPCUI.cs", ["chatTMP", "body", "chatAnimator", "interactionAnim", "moodFrame", "buttonFrame", "chatFrame", "giveButton", "giveButtonAnim", "moodImage", "moodBar"])

build = read("ProjectSettings/EditorBuildSettings.asset")
for name in ("Intro", "SampleScene"):
    path = "Assets/Prefabs/Scenes/" + name + ".unity"
    check(name + " is an enabled build scene", "- enabled: 1\n    path: " + path in build and (PROJECT / path).is_file())

result = {"utc": datetime.now(timezone.utc).isoformat(), "scope": "Serialized references and data consistency only; no runtime or build verification", "passed": sum(c["passed"] for c in checks), "total": len(checks), "checks": checks}
output = PROJECT / "QAOutput/ConfigurationAudit.json"
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({"passed": result["passed"], "total": result["total"], "failures": [c for c in checks if not c["passed"]], "report": str(output)}, ensure_ascii=False))
raise SystemExit(0 if result["passed"] == result["total"] else 1)
