"""Phase 5: Validation — Schema validation + LLM-based auto-correction."""

import argparse
import json
import os

import llm_client

VALID_PRIMITIVE_TYPES = {
    "uint8", "int8", "uint16", "int16", "uint32", "int32",
    "uint64", "int64", "float", "double", "bool", "string", "bytes",
}


def validate_protocol(protocol: dict) -> list[str]:
    """Rule-based schema validation. Returns list of error strings."""
    errors = []

    # Check top-level structure
    if "protocol" not in protocol:
        errors.append("Missing 'protocol' section")
        return errors
    if "packets" not in protocol:
        errors.append("Missing 'packets' section")
        return errors

    proto = protocol["protocol"]
    header = proto.get("header", {})

    # Header checks
    if not header.get("size_field"):
        errors.append("Header missing 'size_field'")
    if not header.get("type_field"):
        errors.append("Header missing 'type_field'")
    for hf in header.get("fields", []):
        if "offset" not in hf:
            errors.append(f"Header field '{hf.get('name')}' missing 'offset'")

    # Collect defined type names
    defined_types = {t["name"] for t in protocol.get("types", [])}

    # Packet checks
    seen_type_values = {}
    for pkt in protocol["packets"]:
        name = pkt.get("name", "?")

        # Type value must be integer
        tv = pkt.get("type")
        if not isinstance(tv, int):
            errors.append(f"Packet '{name}': type must be integer, got {type(tv).__name__}")
        elif tv in seen_type_values:
            errors.append(f"Packet '{name}': duplicate type value {tv} (also {seen_type_values[tv]})")
        else:
            seen_type_values[tv] = name

        # Direction
        if pkt.get("direction") not in ("C2S", "S2C"):
            errors.append(f"Packet '{name}': invalid direction '{pkt.get('direction')}'")

        # Fields
        for field in pkt.get("fields", []):
            _validate_field(field, name, defined_types, errors)

    # Type definitions
    for tdef in protocol.get("types", []):
        for field in tdef.get("fields", []):
            _validate_field(field, f"type:{tdef['name']}", defined_types, errors)

    return errors


def _validate_field(field: dict, context: str, defined_types: set, errors: list):
    fname = field.get("name", "?")
    ftype = field.get("type", "")

    if ftype == "string" and "length" not in field:
        errors.append(f"{context}.{fname}: string missing 'length'")
    elif ftype == "bytes" and "length" not in field:
        errors.append(f"{context}.{fname}: bytes missing 'length'")
    elif ftype == "array":
        if "element" not in field:
            errors.append(f"{context}.{fname}: array missing 'element'")
        elif field["element"] not in VALID_PRIMITIVE_TYPES and field["element"] not in defined_types:
            errors.append(f"{context}.{fname}: array element '{field['element']}' not defined")
        if "length" not in field and "count_field" not in field:
            errors.append(f"{context}.{fname}: array needs 'length' or 'count_field'")
    elif ftype not in VALID_PRIMITIVE_TYPES and ftype not in defined_types:
        errors.append(f"{context}.{fname}: unknown type '{ftype}'")


def _load_prompt(name: str) -> str:
    prompt_path = os.path.join(os.path.dirname(__file__), "prompts", name)
    with open(prompt_path) as f:
        return f.read()


def run(protocol_path: str, max_fix_rounds: int = 2):
    with open(protocol_path) as f:
        protocol = json.load(f)

    for i in range(max_fix_rounds + 1):
        errors = validate_protocol(protocol)
        if not errors:
            print(f"[Validation] ✓ Passed ({len(protocol.get('packets', []))} packets, {len(protocol.get('types', []))} types)")
            # Save validated version
            with open(protocol_path, "w") as f:
                json.dump(protocol, f, indent=2, ensure_ascii=False)
            return protocol

        print(f"[Validation] Round {i + 1}: {len(errors)} errors")
        for e in errors[:10]:
            print(f"  - {e}")

        if i >= max_fix_rounds:
            print(f"[Validation] ⚠ {len(errors)} errors remain after {max_fix_rounds} fix rounds")
            break

        # LLM auto-fix
        print("  Requesting LLM fix...")
        fix_prompt = (
            "You are a JSON protocol fixer. Fix the following errors in the protocol JSON.\n"
            "Return the COMPLETE fixed protocol JSON.\n"
            "Do NOT change anything that isn't broken."
        )
        fix_msg = (
            f"Errors:\n" + "\n".join(f"- {e}" for e in errors) +
            f"\n\nProtocol JSON:\n```json\n{json.dumps(protocol, ensure_ascii=False, indent=2)}\n```"
        )
        protocol = llm_client.invoke_json(fix_prompt, fix_msg)

    # Save even with errors
    with open(protocol_path, "w") as f:
        json.dump(protocol, f, indent=2, ensure_ascii=False)
    return protocol


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Phase 5: Validation")
    parser.add_argument("--protocol", required=True, help="Protocol JSON path")
    parser.add_argument("--fix-rounds", type=int, default=2)
    args = parser.parse_args()
    run(args.protocol, args.fix_rounds)
