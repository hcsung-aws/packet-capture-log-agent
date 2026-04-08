"""Validation Lambda — Schema validation + pattern check + metadata cross-ref."""

import json
import s3_helper
import llm_client

VALID_TYPES = {
    "uint8", "int8", "uint16", "int16", "uint32", "int32",
    "uint64", "int64", "float", "double", "bool", "string", "bytes",
}


def _validate(protocol, metadata=None):
    errors = []
    if "protocol" not in protocol or "packets" not in protocol:
        return ["Missing 'protocol' or 'packets' section"]

    header = protocol["protocol"].get("header", {})
    if not header.get("size_field"):
        errors.append("Header missing 'size_field'")
    if not header.get("type_field"):
        errors.append("Header missing 'type_field'")

    defined = {t["name"] for t in protocol.get("types", [])}
    seen = {}
    for pkt in protocol["packets"]:
        n = pkt.get("name", "?")
        tv = pkt.get("type")
        if not isinstance(tv, int):
            errors.append(f"{n}: type must be int")
        elif tv in seen:
            errors.append(f"{n}: duplicate type {tv}")
        else:
            seen[tv] = n
        if pkt.get("direction") not in ("C2S", "S2C"):
            errors.append(f"{n}: invalid direction")
        for f in pkt.get("fields", []):
            _check_field(f, n, defined, errors)
    for td in protocol.get("types", []):
        for f in td.get("fields", []):
            _check_field(f, f"type:{td['name']}", defined, errors)

    # Pattern check: detect suspicious sequential type values
    _check_type_pattern(protocol["packets"], errors)

    # Cross-reference with metadata if available
    if metadata:
        _cross_check_metadata(protocol, metadata, errors)

    return errors


def _check_field(field, ctx, defined, errors):
    ft = field.get("type", "")
    fn = field.get("name", "?")
    if ft == "string" and "length" not in field:
        errors.append(f"{ctx}.{fn}: string needs length")
    elif ft == "array":
        if "element" not in field:
            errors.append(f"{ctx}.{fn}: array needs element")
        elif field["element"] not in VALID_TYPES and field["element"] not in defined:
            errors.append(f"{ctx}.{fn}: unknown element '{field['element']}'")
        if "length" not in field and "count_field" not in field:
            errors.append(f"{ctx}.{fn}: array needs length/count_field")
    elif ft not in VALID_TYPES and ft not in defined:
        errors.append(f"{ctx}.{fn}: unknown type '{ft}'")


def _check_type_pattern(packets, errors):
    """Detect fabricated sequential type values."""
    types = sorted(p["type"] for p in packets if isinstance(p.get("type"), int))
    if len(types) < 3:
        return

    # Check if all types are perfectly sequential (1,2,3... or N,N+1,N+2...)
    if types == list(range(types[0], types[0] + len(types))):
        errors.append(
            f"SUSPICIOUS: all {len(types)} packet types are sequential "
            f"({types[0]}..{types[-1]}). Likely fabricated, not from source."
        )
        return

    # Check C2S and S2C separately
    c2s = sorted(p["type"] for p in packets if p.get("direction") == "C2S" and isinstance(p.get("type"), int))
    s2c = sorted(p["type"] for p in packets if p.get("direction") == "S2C" and isinstance(p.get("type"), int))
    for label, vals in [("C2S", c2s), ("S2C", s2c)]:
        if len(vals) >= 5 and vals == list(range(vals[0], vals[0] + len(vals))):
            errors.append(
                f"SUSPICIOUS: {label} types are sequential ({vals[0]}..{vals[-1]}). "
                f"Likely fabricated — real protocols have gaps between categories."
            )


def _cross_check_metadata(protocol, metadata, errors):
    """Cross-reference protocol type values against merge metadata."""
    meta_packets = {p.get("type_name"): p for p in metadata.get("packets", [])}
    for pkt in protocol["packets"]:
        name = pkt.get("name")
        mp = meta_packets.get(name)
        if not mp:
            continue
        meta_val = mp.get("type_value", "")
        if not meta_val:
            continue
        # Convert metadata hex to int for comparison
        try:
            if isinstance(meta_val, str) and meta_val.startswith("0x"):
                expected = int(meta_val, 16)
            else:
                expected = int(meta_val)
            if pkt["type"] != expected:
                errors.append(
                    f"{name}: type {pkt['type']} doesn't match metadata {meta_val} ({expected})"
                )
        except (ValueError, TypeError):
            pass


def handler(event, context):
    job_id = event["job_id"]
    max_fixes = event.get("max_fix_rounds", 2)

    protocol = s3_helper.read_json(f"jobs/{job_id}/protocol.json")

    # Load metadata for cross-reference
    metadata = None
    try:
        metadata = s3_helper.read_json(f"jobs/{job_id}/metadata.json")
    except Exception:
        pass

    for i in range(max_fixes + 1):
        errors = _validate(protocol, metadata)
        if not errors:
            s3_helper.write_json(f"jobs/{job_id}/protocol.json", protocol)
            return {
                "job_id": job_id, "valid": True,
                "packet_count": len(protocol.get("packets", [])),
                "type_count": len(protocol.get("types", [])),
                "fix_rounds": i,
            }

        if i >= max_fixes:
            break

        fix_msg = (
            f"Errors:\n" + "\n".join(f"- {e}" for e in errors) +
            f"\n\nProtocol JSON:\n```json\n{json.dumps(protocol, ensure_ascii=False, indent=2)}\n```"
        )
        if metadata:
            fix_msg += f"\n\nSource metadata for reference:\n```json\n{json.dumps(metadata, ensure_ascii=False, indent=2)}\n```"
        protocol = llm_client.invoke_json(
            "Fix the errors in this protocol JSON. Use the source metadata to correct type values. Return the COMPLETE fixed JSON.",
            fix_msg,
        )

    s3_helper.write_json(f"jobs/{job_id}/protocol.json", protocol)
    s3_helper.write_json(f"jobs/{job_id}/validation_errors.json", errors)
    return {"job_id": job_id, "valid": False, "errors": errors, "fix_rounds": max_fixes}
