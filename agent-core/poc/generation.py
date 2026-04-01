"""Phase 4: Generation — Convert unified metadata to protocol JSON."""

import argparse
import json
import os

import llm_client

SCHEMA_PATH = os.path.join(
    os.path.dirname(__file__), "..", "..", "docs", "PROTOCOL_SCHEMA.md"
)


def _load_prompt(name: str) -> str:
    prompt_path = os.path.join(os.path.dirname(__file__), "prompts", name)
    with open(prompt_path) as f:
        return f.read()


def run(metadata_path: str, output_path: str):
    with open(metadata_path) as f:
        metadata = json.load(f)

    # Load schema doc for context
    schema_doc = ""
    if os.path.exists(SCHEMA_PATH):
        with open(SCHEMA_PATH) as f:
            schema_doc = f.read()

    gen_prompt = _load_prompt("generation_generator.txt")
    rev_prompt = _load_prompt("validation_reviewer.txt")

    user_msg = f"Unified metadata:\n```json\n{json.dumps(metadata, ensure_ascii=False, indent=2)}\n```"
    if schema_doc:
        user_msg += f"\n\nTarget schema reference:\n```markdown\n{schema_doc}\n```"

    print("[Generation] Running Generation Agent (Generator + Reviewer)...")
    result = llm_client.generate_and_review(
        generator_system=gen_prompt,
        generator_user=user_msg,
        reviewer_system=rev_prompt,
        context_for_reviewer=f"Source metadata has {len(metadata.get('packets', []))} packets, "
                             f"{len(metadata.get('nested_types', []))} nested types.",
    )

    os.makedirs(os.path.dirname(output_path) or ".", exist_ok=True)
    with open(output_path, "w") as f:
        json.dump(result, f, indent=2, ensure_ascii=False)

    # Summary
    packets = result.get("packets", [])
    types = result.get("types", [])
    print(f"  Packets: {len(packets)}, Types: {len(types)}")
    print(f"  Output: {output_path}")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Phase 4: Generation")
    parser.add_argument("--metadata", required=True, help="Unified metadata JSON")
    parser.add_argument("--output", default="jobs/latest/protocol.json")
    args = parser.parse_args()
    run(args.metadata, args.output)
