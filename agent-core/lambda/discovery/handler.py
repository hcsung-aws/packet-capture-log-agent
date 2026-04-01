"""Discovery Lambda — Identify packet-related files from source."""

import json
import s3_helper
import llm_client
import prompt_loader

SAMPLE_LINES = 80
SKIP_EXT = {".exe", ".dll", ".obj", ".pdb", ".lib", ".o", ".so", ".png", ".jpg", ".zip", ".gz"}
CODE_EXT = {".h", ".hpp", ".c", ".cpp", ".cc", ".cxx", ".cs", ".java", ".py", ".go", ".rs", ".proto", ".fbs"}


def handler(event, context):
    job_id = event["job_id"]
    prefix = f"jobs/{job_id}/source/"

    # List source files from S3
    keys = s3_helper.list_keys(prefix)
    files = []
    for key in keys:
        ext_lower = key[key.rfind("."):].lower() if "." in key else ""
        if ext_lower in SKIP_EXT or ext_lower not in CODE_EXT:
            continue
        content = s3_helper.read_text(key)
        rel = key[len(prefix):]
        lines = content.split("\n")[:SAMPLE_LINES]
        files.append({"path": rel, "size": len(content), "sample": "\n".join(lines)})

    file_desc = [f"=== {f['path']} ({f['size']} bytes) ===\n{f['sample']}\n" for f in files]
    user_msg = f"Source directory contains {len(files)} files:\n\n" + "\n".join(file_desc)

    gen_prompt = prompt_loader.load("discovery_generator.txt")
    rev_prompt = prompt_loader.load("discovery_reviewer.txt")

    out = llm_client.generate_and_review(gen_prompt, user_msg, rev_prompt, rev_context=user_msg)

    result = out["result"]
    s3_helper.write_json(f"jobs/{job_id}/discovery.json", result)

    return {
        "job_id": job_id,
        "file_count": len(files),
        "relevant_count": len(result.get("relevant_files", [])),
        "relevant_files": result.get("relevant_files", []),
        "approved": out["approved"],
        "rounds": out["rounds"],
    }
