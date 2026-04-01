"""Generation Lambda — Convert metadata to protocol JSON."""

import json
import s3_helper
import llm_client
import prompt_loader


def handler(event, context):
    job_id = event["job_id"]

    metadata = s3_helper.read_json(f"jobs/{job_id}/metadata.json")

    # Load schema doc if available
    schema_doc = ""
    try:
        schema_doc = s3_helper.read_text("schema/PROTOCOL_SCHEMA.md")
    except Exception:
        pass

    gen_prompt = prompt_loader.load("generation_generator.txt")
    rev_prompt = prompt_loader.load("validation_reviewer.txt")

    user_msg = f"Unified metadata:\n```json\n{json.dumps(metadata, ensure_ascii=False, indent=2)}\n```"
    if schema_doc:
        user_msg += f"\n\nTarget schema reference:\n```markdown\n{schema_doc}\n```"

    rev_ctx = (
        f"Source metadata has {len(metadata.get('packets', []))} packets, "
        f"{len(metadata.get('nested_types', []))} nested types."
    )

    out = llm_client.generate_and_review(gen_prompt, user_msg, rev_prompt, rev_context=rev_ctx)

    result = out["result"]
    s3_helper.write_json(f"jobs/{job_id}/protocol.json", result)

    return {
        "job_id": job_id,
        "packet_count": len(result.get("packets", [])),
        "type_count": len(result.get("types", [])),
        "approved": out["approved"],
        "rounds": out["rounds"],
    }
