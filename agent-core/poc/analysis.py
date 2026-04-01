"""Phase 2: Analysis — Extract per-file metadata from identified source files."""

import argparse
import json
import os

import llm_client

# Only analyze files with these roles (contain actual packet structure info)
ANALYSIS_ROLES = {"packet_definitions", "constants", "serialization"}


def _load_prompt(name: str) -> str:
    prompt_path = os.path.join(os.path.dirname(__file__), "prompts", name)
    with open(prompt_path) as f:
        return f.read()


def _read_file(source_dir: str, rel_path: str) -> str:
    fpath = os.path.join(source_dir, rel_path)
    with open(fpath, "r", encoding="utf-8", errors="replace") as f:
        return f.read()


def run(discovery_path: str, source_dir: str, output_dir: str):
    with open(discovery_path) as f:
        discovery = json.load(f)

    # Filter to files worth analyzing in detail
    files_to_analyze = [
        rf for rf in discovery["relevant_files"]
        if rf["role"] in ANALYSIS_ROLES
    ]
    print(f"[Analysis] {len(files_to_analyze)} files to analyze")

    gen_prompt = _load_prompt("analysis_generator.txt")
    rev_prompt = _load_prompt("analysis_reviewer.txt")

    os.makedirs(output_dir, exist_ok=True)
    results = []

    for rf in files_to_analyze:
        path = rf["path"]
        safe_name = path.replace("/", "_").replace("\\", "_").replace(".", "_")
        out_path = os.path.join(output_dir, f"{safe_name}.json")

        # Skip already analyzed files
        if os.path.exists(out_path):
            print(f"  Skipping (exists): {path}")
            with open(out_path) as f:
                results.append(json.load(f))
            continue

        print(f"  Analyzing: {path}")
        source_content = _read_file(source_dir, path)
        user_msg = f"File: {path}\nRole: {rf['role']}\n\n```\n{source_content}\n```"

        result = llm_client.generate_and_review(
            generator_system=gen_prompt,
            generator_user=user_msg,
            reviewer_system=rev_prompt,
            context_for_reviewer=f"Original source:\n```\n{source_content}\n```",
        )

        with open(out_path, "w") as f:
            json.dump(result, f, indent=2, ensure_ascii=False)
        results.append(result)
        print(f"    → {out_path}")

    print(f"[Analysis] Done: {len(results)} files analyzed")
    return results


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Phase 2: Analysis")
    parser.add_argument("--discovery", required=True, help="Discovery JSON path")
    parser.add_argument("--source", required=True, help="Source directory path")
    parser.add_argument("--output", default="jobs/latest/analysis")
    args = parser.parse_args()
    run(args.discovery, args.source, args.output)
