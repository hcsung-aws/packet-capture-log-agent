"""Analysis Lambda — Extract metadata from a single source file."""

import json
import s3_helper
import llm_client
import prompt_loader

ANALYSIS_ROLES = {"packet_definitions", "constants", "serialization"}


def handler(event, context):
    job_id = event["job_id"]
    file_info = event["file"]  # {path, role}
    path = file_info["path"]
    role = file_info["role"]

    if role not in ANALYSIS_ROLES:
        return {"job_id": job_id, "path": path, "skipped": True, "reason": f"role '{role}' not analyzed"}

    source = s3_helper.read_text(f"jobs/{job_id}/source/{path}")
    user_msg = f"File: {path}\nRole: {role}\n\n```\n{source}\n```"

    gen_prompt = prompt_loader.load("analysis_generator.txt")
    rev_prompt = prompt_loader.load("analysis_reviewer.txt")

    out = llm_client.generate_and_review(
        gen_prompt, user_msg, rev_prompt,
        rev_context=f"Original source:\n```\n{source}\n```",
    )

    safe = path.replace("/", "_").replace("\\", "_").replace(".", "_")
    s3_helper.write_json(f"jobs/{job_id}/analysis/{safe}.json", out["result"])

    return {
        "job_id": job_id,
        "path": path,
        "skipped": False,
        "approved": out["approved"],
        "rounds": out["rounds"],
    }
