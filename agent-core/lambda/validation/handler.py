"""Validation Lambda — Schema validation + LLM auto-correction."""

import json
import s3_helper
import llm_client

VALID_TYPES = {
    "uint8", "int8", "uint16", "int16", "uint32", "int32",
    "uint64", "int64", "float", "double", "bool", "string", "bytes",
}


def _validate(protocol):
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


def handler(event, context):
    job_id = event["job_id"]
    max_fixes = event.get("max_fix_rounds", 2)

    protocol = s3_helper.read_json(f"jobs/{job_id}/protocol.json")

    for i in range(max_fixes + 1):
        errors = _validate(protocol)
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
        protocol = llm_client.invoke_json(
            "Fix the errors in this protocol JSON. Return the COMPLETE fixed JSON.",
            fix_msg,
        )

    s3_helper.write_json(f"jobs/{job_id}/protocol.json", protocol)
    s3_helper.write_json(f"jobs/{job_id}/validation_errors.json", errors)
    return {"job_id": job_id, "valid": False, "errors": errors, "fix_rounds": max_fixes}
