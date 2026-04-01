"""Phase 3: Merge — Combine per-file analysis into unified metadata."""

import argparse
import json
import os
import glob as globmod

import llm_client


def _load_prompt(name: str) -> str:
    prompt_path = os.path.join(os.path.dirname(__file__), "prompts", name)
    with open(prompt_path) as f:
        return f.read()


def run(analysis_dir: str, output_path: str):
    # Load all analysis results
    analysis_files = sorted(globmod.glob(os.path.join(analysis_dir, "*.json")))
    print(f"[Merge] Loading {len(analysis_files)} analysis files")

    analyses = []
    for af in analysis_files:
        with open(af) as f:
            analyses.append(json.load(f))

    user_msg = (
        f"Per-file analysis results ({len(analyses)} files):\n\n"
        f"```json\n{json.dumps(analyses, ensure_ascii=False, indent=2)}\n```"
    )

    gen_prompt = _load_prompt("merge_generator.txt")
    rev_prompt = _load_prompt("merge_reviewer.txt")

    print("  Running Merge Agent (Generator + Reviewer)...")
    result = llm_client.generate_and_review(
        generator_system=gen_prompt,
        generator_user=user_msg,
        reviewer_system=rev_prompt,
        context_for_reviewer=user_msg,
    )

    os.makedirs(os.path.dirname(output_path) or ".", exist_ok=True)
    with open(output_path, "w") as f:
        json.dump(result, f, indent=2, ensure_ascii=False)

    # Summary
    print(f"  Packets: {len(result.get('packets', []))}")
    print(f"  Nested types: {len(result.get('nested_types', []))}")
    print(f"  Output: {output_path}")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Phase 3: Merge")
    parser.add_argument("--analysis", required=True, help="Analysis directory")
    parser.add_argument("--output", default="jobs/latest/metadata.json")
    args = parser.parse_args()
    run(args.analysis, args.output)
