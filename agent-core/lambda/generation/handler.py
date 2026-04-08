"""Generation Lambda — Deterministic metadata→protocol + LLM for semantics only."""

import json
import s3_helper
import llm_client
import prompt_loader

TYPE_MAP = {
    "uint8_t": "uint8", "int8_t": "int8", "uint8": "uint8", "int8": "int8",
    "uint16_t": "uint16", "int16_t": "int16", "uint16": "uint16", "int16": "int16",
    "uint32_t": "uint32", "int32_t": "int32", "uint32": "uint32", "int32": "int32",
    "uint64_t": "uint64", "int64_t": "int64", "uint64": "uint64", "int64": "int64",
    "float": "float", "double": "double", "bool": "uint8",
    "byte": "uint8", "sbyte": "int8",
    "ushort": "uint16", "short": "int16",
    "uint": "uint32", "int": "int32",
    "ulong": "uint64", "long": "int64",
}

COUNT_SUFFIXES = ("count", "Count", "Cnt")


def _convert_type_value(val):
    if isinstance(val, int):
        return val
    val = str(val).strip()
    return int(val, 16) if val.startswith("0x") or val.startswith("0X") else int(val)


def _convert_field(field, nested_names):
    name = field["name"]
    cpp_type = field.get("cpp_type", field.get("type", ""))
    array_size = field.get("array_size")

    if cpp_type == "char" and array_size:
        return {"name": name, "type": "string", "length": array_size}
    if cpp_type in TYPE_MAP and not array_size:
        return {"name": name, "type": TYPE_MAP[cpp_type]}
    if cpp_type in nested_names and array_size:
        return {"name": name, "type": "array", "length": array_size, "element": cpp_type}
    if cpp_type in nested_names:
        return {"name": name, "type": cpp_type}

    mapped = TYPE_MAP.get(cpp_type, cpp_type)
    if array_size:
        return {"name": name, "type": "array", "length": array_size, "element": mapped}
    return {"name": name, "type": mapped}


def _apply_count_field(fields):
    result = []
    for i, f in enumerate(fields):
        if f.get("type") == "array" and "length" in f and i > 0:
            prev = result[-1]
            if prev["type"] in ("uint8", "uint16", "uint32"):
                if prev["name"].endswith(COUNT_SUFFIXES) or prev["name"] in ("count",):
                    f = dict(f)
                    del f["length"]
                    f["count_field"] = prev["name"]
        result.append(f)
    return result


def _convert_header(meta_header):
    fields = []
    offset = 0
    for f in meta_header.get("fields", []):
        cpp_type = f.get("type", f.get("cpp_type", "uint16_t"))
        ptype = TYPE_MAP.get(cpp_type, "uint16")
        size = {"uint8": 1, "int8": 1, "uint16": 2, "int16": 2,
                "uint32": 4, "int32": 4, "uint64": 8, "int64": 8}.get(ptype, 2)
        fields.append({"name": f["name"], "type": ptype, "offset": offset})
        offset += size
    return {
        "size_field": meta_header.get("size_field", "length"),
        "type_field": meta_header.get("type_field", "type"),
        "fields": fields,
    }


def _build_phases(packets):
    """Deterministic phase grouping by high byte of type value."""
    category_names = {}
    for pkt in packets:
        if pkt["direction"] != "C2S":
            continue
        cat = pkt["type"] >> 8
        name = pkt["name"]
        # Infer phase name from first packet in category
        if cat not in category_names:
            # CS_LOGIN → Login, CS_CHAR_LIST → Character, CS_MOVE → Gameplay
            parts = name.replace("CS_", "").split("_")
            label = {
                "LOGIN": "Login", "LOGOUT": "Login",
                "CHAR": "Enter Game", "ATTENDANCE": "Attendance",
                "MOVE": "Gameplay", "ATTACK": "Gameplay",
                "CHAT": "Gameplay", "ITEM": "Gameplay",
                "SHOP": "Shop", "QUEST": "Quest",
                "PARTY": "Party", "HEARTBEAT": "System",
            }.get(parts[0], parts[0].title())
            category_names[cat] = label

    # Group categories by phase name
    phase_map = {}
    for cat, label in sorted(category_names.items()):
        phase_map.setdefault(label, []).append(cat)
    return phase_map


def _deterministic_convert(metadata):
    nested_types = metadata.get("nested_types", [])
    nested_names = {t["name"] for t in nested_types}

    types = []
    for nt in nested_types:
        fields = [_convert_field(f, nested_names) for f in nt.get("fields", [])]
        types.append({"name": nt["name"], "type": "struct", "fields": fields})

    packets = []
    for pkt in metadata.get("packets", []):
        raw_fields = [_convert_field(f, nested_names) for f in pkt.get("fields", [])]
        fields = _apply_count_field(raw_fields)
        packets.append({
            "type": _convert_type_value(pkt["type_value"]),
            "name": pkt["type_name"],
            "direction": pkt["direction"],
            "fields": fields,
        })

    return {
        "protocol": {
            "name": metadata.get("protocol_name", "Game Protocol"),
            "version": "1.0",
            "endian": metadata.get("endian", "little"),
            "pack": metadata.get("pack", 1),
            "header": _convert_header(metadata.get("header", {})),
        },
        "phases": _build_phases(packets),
        "types": types,
        "packets": packets,
    }


def _generate_semantics(metadata, protocol):
    """Use LLM only for semantics (interaction_sources, state_conditions, proximity_actions)."""
    packet_summary = json.dumps(
        [{"name": p["name"], "direction": p["direction"],
          "fields": [f["name"] for f in p["fields"]]} for p in protocol["packets"]],
        ensure_ascii=False,
    )
    prompt = prompt_loader.load("semantics_generator.txt")
    try:
        result = llm_client.invoke_json(prompt, f"Packets:\n{packet_summary}")
        if isinstance(result, dict):
            return result
    except Exception:
        pass
    return {}


def handler(event, context):
    job_id = event["job_id"]
    metadata = s3_helper.read_json(f"jobs/{job_id}/metadata.json")

    # Deterministic conversion
    protocol = _deterministic_convert(metadata)

    # LLM only for semantics
    semantics = _generate_semantics(metadata, protocol)
    if semantics:
        protocol["semantics"] = semantics

    s3_helper.write_json(f"jobs/{job_id}/protocol.json", protocol)

    return {
        "job_id": job_id,
        "packet_count": len(protocol.get("packets", [])),
        "type_count": len(protocol.get("types", [])),
        "approved": True,
        "rounds": 0,
    }
