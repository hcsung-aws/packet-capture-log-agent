"""Phase 1: Discovery — Scan source directory and identify packet-related files."""

import argparse
import json
import os
import sys

import llm_client

SAMPLE_LINES = 80
SKIP_EXTENSIONS = {".exe", ".dll", ".obj", ".pdb", ".lib", ".o", ".so", ".png", ".jpg", ".gif", ".ico", ".zip", ".gz"}
CODE_EXTENSIONS = {".h", ".hpp", ".c", ".cpp", ".cc", ".cxx", ".cs", ".java", ".py", ".go", ".rs", ".proto", ".fbs"}


def scan_source_dir(source_dir: str) -> list[dict]:
    """Collect source files with first N lines as sample."""
    files = []
    for root, _, filenames in os.walk(source_dir):
        for fname in filenames:
            ext = os.path.splitext(fname)[1].lower()
            if ext in SKIP_EXTENSIONS or ext not in CODE_EXTENSIONS:
                continue
            fpath = os.path.join(root, fname)
            rel = os.path.relpath(fpath, source_dir)
            try:
                with open(fpath, "r", encoding="utf-8", errors="replace") as f:
                    lines = []
                    for i, line in enumerate(f):
                        if i >= SAMPLE_LINES:
                            break
                        lines.append(line.rstrip())
                size = os.path.getsize(fpath)
                files.append({"path": rel, "size": size, "sample": "\n".join(lines)})
            except Exception:
                pass
    return files


def _load_prompt(name: str) -> str:
    prompt_path = os.path.join(os.path.dirname(__file__), "prompts", name)
    with open(prompt_path) as f:
        return f.read()


def run(source_dir: str, output_path: str):
    print(f"[Discovery] Scanning {source_dir}")
    files = scan_source_dir(source_dir)
    print(f"  Found {len(files)} source files")

    # Build user message: file list with samples
    file_descriptions = []
    for f in files:
        file_descriptions.append(f"=== {f['path']} ({f['size']} bytes) ===\n{f['sample']}\n")
    user_msg = f"Source directory contains {len(files)} files:\n\n" + "\n".join(file_descriptions)

    gen_prompt = _load_prompt("discovery_generator.txt")
    rev_prompt = _load_prompt("discovery_reviewer.txt")

    print("  Running Discovery Agent (Generator + Reviewer)...")
    result = llm_client.generate_and_review(
        generator_system=gen_prompt,
        generator_user=user_msg,
        reviewer_system=rev_prompt,
        context_for_reviewer=user_msg,
    )

    os.makedirs(os.path.dirname(output_path) or ".", exist_ok=True)
    with open(output_path, "w") as f:
        json.dump(result, f, indent=2, ensure_ascii=False)
    print(f"  Output: {output_path}")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Phase 1: Discovery")
    parser.add_argument("--source", required=True, help="Source directory path")
    parser.add_argument("--output", default="jobs/latest/discovery.json")
    args = parser.parse_args()
    run(args.source, args.output)
