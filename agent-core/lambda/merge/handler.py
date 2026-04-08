"""Merge Lambda — Combine per-file analysis into unified metadata."""

import json
import s3_helper
import llm_client
import prompt_loader


def handler(event, context):
    job_id = event["job_id"]

    keys = s3_helper.list_keys(f"jobs/{job_id}/analysis/")
    analyses = [s3_helper.read_json(k) for k in keys if k.endswith(".json")]

    user_msg = (
        f"Per-file analysis results ({len(analyses)} files):\n\n"
        f"```json\n{json.dumps(analyses, ensure_ascii=False, indent=2)}\n```"
    )

    gen_prompt = prompt_loader.load("merge_generator.txt")
    rev_prompt = prompt_loader.load("merge_reviewer.txt")

    out = llm_client.generate_and_review(gen_prompt, user_msg, rev_prompt, rev_context=user_msg)

    result = out["result"]
    if isinstance(result, list):
        result = {"packets": result, "nested_types": []}
    s3_helper.write_json(f"jobs/{job_id}/metadata.json", result)

    return {
        "job_id": job_id,
        "packet_count": len(result.get("packets", [])),
        "nested_type_count": len(result.get("nested_types", [])),
        "approved": out["approved"],
        "rounds": out["rounds"],
    }
