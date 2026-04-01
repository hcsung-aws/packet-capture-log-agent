"""Protocol Agent CLI — Upload source, run pipeline, download result."""

import argparse
import json
import os
import sys
import time
import boto3

DEFAULT_REGION = "us-east-1"
DEFAULT_BUCKET = "protocol-agent-jobs-965037532757"
DEFAULT_SFN_ARN = "arn:aws:states:us-east-1:965037532757:stateMachine:protocol-agent-pipeline"

CODE_EXT = {".h", ".hpp", ".c", ".cpp", ".cc", ".cxx", ".cs", ".java", ".py", ".go", ".rs", ".proto", ".fbs"}
SKIP_DIRS = {".git", "build", "out", "bin", "obj", "Debug", "Release", "x64", ".vs", "node_modules"}


def _collect_source_files(source_dir):
    files = []
    for root, dirs, filenames in os.walk(source_dir):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for fname in filenames:
            ext = os.path.splitext(fname)[1].lower()
            if ext in CODE_EXT:
                fpath = os.path.join(root, fname)
                rel = os.path.relpath(fpath, source_dir)
                files.append((fpath, rel))
    return files


def upload(source_dir, job_id, bucket, region):
    s3 = boto3.client("s3", region_name=region)
    files = _collect_source_files(source_dir)
    print(f"Uploading {len(files)} source files to s3://{bucket}/jobs/{job_id}/source/")
    for fpath, rel in files:
        key = f"jobs/{job_id}/source/{rel}"
        s3.upload_file(fpath, bucket, key)
        print(f"  {rel}")
    print("Upload complete.")
    return len(files)


def start_pipeline(job_id, sfn_arn, region, model_id=None):
    sfn = boto3.client("stepfunctions", region_name=region)
    inp = {"job_id": job_id}
    if model_id:
        inp["model_id"] = model_id
    resp = sfn.start_execution(
        stateMachineArn=sfn_arn,
        name=f"{job_id}-{int(time.time())}",
        input=json.dumps(inp),
    )
    return resp["executionArn"]


def wait_for_completion(exec_arn, region, poll_interval=30):
    sfn = boto3.client("stepfunctions", region_name=region)
    start = time.time()
    while True:
        desc = sfn.describe_execution(executionArn=exec_arn)
        status = desc["status"]
        elapsed = int(time.time() - start)
        print(f"\r  [{elapsed}s] {status}", end="", flush=True)
        if status in ("SUCCEEDED", "FAILED", "TIMED_OUT", "ABORTED"):
            print()
            return status, desc
        time.sleep(poll_interval)


def download_result(job_id, output_path, bucket, region):
    s3 = boto3.client("s3", region_name=region)
    key = f"jobs/{job_id}/protocol.json"
    s3.download_file(bucket, key, output_path)
    with open(output_path) as f:
        data = json.load(f)
    return data


def cmd_generate(args):
    job_id = args.job_id or f"job-{int(time.time())}"
    print(f"Job ID: {job_id}")

    # Upload
    upload(args.source, job_id, args.bucket, args.region)

    # Start pipeline
    print("Starting pipeline...")
    exec_arn = start_pipeline(job_id, args.sfn_arn, args.region, args.model)
    print(f"Execution: {exec_arn}")

    # Wait
    print("Waiting for completion...")
    status, desc = wait_for_completion(exec_arn, args.region)

    if status != "SUCCEEDED":
        print(f"Pipeline {status}")
        if desc.get("error"):
            print(f"Error: {desc['error']}")
        sys.exit(1)

    # Download
    output = args.output or f"protocol_{job_id}.json"
    data = download_result(job_id, output, args.bucket, args.region)
    packets = data.get("packets", [])
    types = data.get("types", [])
    print(f"\n✓ Generated: {len(packets)} packets, {len(types)} types")
    print(f"Output: {output}")


def cmd_status(args):
    sfn = boto3.client("stepfunctions", region_name=args.region)
    execs = sfn.list_executions(stateMachineArn=args.sfn_arn, maxResults=10)
    for e in execs.get("executions", []):
        name = e["name"]
        status = e["status"]
        start = e["startDate"].strftime("%Y-%m-%d %H:%M")
        print(f"  {name}: {status} ({start})")


def main():
    parser = argparse.ArgumentParser(description="Protocol Agent CLI")
    parser.add_argument("--region", default=DEFAULT_REGION)
    parser.add_argument("--bucket", default=DEFAULT_BUCKET)
    parser.add_argument("--sfn-arn", default=DEFAULT_SFN_ARN)

    sub = parser.add_subparsers(dest="command")

    gen = sub.add_parser("generate", help="Generate protocol from source")
    gen.add_argument("--source", required=True, help="Source directory")
    gen.add_argument("--output", help="Output JSON path")
    gen.add_argument("--job-id", help="Custom job ID")
    gen.add_argument("--model", help="Bedrock model ID override")

    sub.add_parser("status", help="List recent executions")

    dl = sub.add_parser("download", help="Download result")
    dl.add_argument("--job-id", required=True)
    dl.add_argument("--output", default="protocol.json")

    args = parser.parse_args()
    if args.command == "generate":
        cmd_generate(args)
    elif args.command == "status":
        cmd_status(args)
    elif args.command == "download":
        data = download_result(args.job_id, args.output, args.bucket, args.region)
        print(f"Downloaded: {len(data.get('packets', []))} packets → {args.output}")
    else:
        parser.print_help()


if __name__ == "__main__":
    main()
