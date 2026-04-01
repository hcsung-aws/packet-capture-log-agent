"""Pipeline — Run all phases sequentially, or invoke individual phases."""

import argparse
import os
import time

import discovery
import analysis
import merge
import generation
import validation


def run_pipeline(source_dir: str, output_path: str, job_dir: str | None = None):
    start = time.time()

    if not job_dir:
        job_dir = os.path.join("jobs", f"run_{int(start)}")
    os.makedirs(job_dir, exist_ok=True)

    discovery_out = os.path.join(job_dir, "discovery.json")
    analysis_out = os.path.join(job_dir, "analysis")
    metadata_out = os.path.join(job_dir, "metadata.json")
    protocol_out = output_path or os.path.join(job_dir, "protocol.json")

    print(f"{'='*60}")
    print(f"Protocol Auto-Generation Pipeline")
    print(f"Source: {source_dir}")
    print(f"Output: {protocol_out}")
    print(f"Job dir: {job_dir}")
    print(f"{'='*60}\n")

    # Phase 1
    discovery.run(source_dir, discovery_out)
    print()

    # Phase 2
    analysis.run(discovery_out, source_dir, analysis_out)
    print()

    # Phase 3
    merge.run(analysis_out, metadata_out)
    print()

    # Phase 4
    generation.run(metadata_out, protocol_out)
    print()

    # Phase 5
    validation.run(protocol_out)

    elapsed = time.time() - start
    print(f"\n{'='*60}")
    print(f"Pipeline complete in {elapsed:.1f}s")
    print(f"Output: {protocol_out}")
    print(f"{'='*60}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Protocol Auto-Generation Pipeline")
    parser.add_argument("--source", required=True, help="Source directory")
    parser.add_argument("--output", default=None, help="Output protocol JSON path")
    parser.add_argument("--job-dir", default=None, help="Job directory for intermediate files")
    args = parser.parse_args()
    run_pipeline(args.source, args.output, args.job_dir)
