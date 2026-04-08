"""Protocol Agent CLI — Generate protocol JSON via API Gateway."""

import argparse
import json
import os
import sys
import tempfile
import time
import zipfile

import requests

CODE_EXT = {".h", ".hpp", ".c", ".cpp", ".cc", ".cxx", ".cs", ".java", ".py", ".go", ".rs", ".proto", ".fbs"}
SKIP_DIRS = {".git", "build", "out", "bin", "obj", "Debug", "Release", "x64", ".vs", "node_modules"}


def _get_config(args):
    """Resolve API URL and key from args or environment."""
    url = args.api_url or os.environ.get("PROTOCOL_AGENT_URL", "")
    key = args.api_key or os.environ.get("PROTOCOL_AGENT_KEY", "")
    if not url or not key:
        print("Error: API URL and API Key required.")
        print("  Set PROTOCOL_AGENT_URL and PROTOCOL_AGENT_KEY environment variables,")
        print("  or use --api-url and --api-key flags.")
        sys.exit(1)
    return url.rstrip("/"), key


def _headers(api_key):
    return {"x-api-key": api_key, "Content-Type": "application/json"}


def _zip_source(source_dir):
    """Zip source directory, filtering by CODE_EXT."""
    tmp = tempfile.NamedTemporaryFile(suffix=".zip", delete=False)
    tmp.close()
    count = 0
    with zipfile.ZipFile(tmp.name, "w", zipfile.ZIP_DEFLATED) as zf:
        for root, dirs, files in os.walk(source_dir):
            dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
            for fname in files:
                ext = os.path.splitext(fname)[1].lower()
                if ext in CODE_EXT:
                    fpath = os.path.join(root, fname)
                    arcname = os.path.relpath(fpath, source_dir)
                    zf.write(fpath, arcname)
                    count += 1
    return tmp.name, count


def cmd_generate(args):
    api_url, api_key = _get_config(args)

    # 1. Zip source
    print(f"Zipping source: {args.source}")
    zip_path, file_count = _zip_source(args.source)
    zip_size = os.path.getsize(zip_path)
    print(f"  {file_count} files, {zip_size / 1024:.0f} KB")

    try:
        # 2. Create job
        resp = requests.post(f"{api_url}/jobs", headers=_headers(api_key))
        resp.raise_for_status()
        data = resp.json()
        job_id = data["job_id"]
        upload_url = data["upload_url"]
        print(f"Job ID: {job_id}")

        # 3. Upload zip via presigned URL
        print("Uploading...")
        with open(zip_path, "rb") as f:
            put_resp = requests.put(upload_url, data=f)
            put_resp.raise_for_status()
        print("Upload complete.")

        # 4. Start pipeline
        body = {}
        if args.model:
            body["model_id"] = args.model
        resp = requests.post(
            f"{api_url}/jobs/{job_id}/pipeline",
            headers=_headers(api_key),
            json=body,
        )
        resp.raise_for_status()
        print("Pipeline started.")

        # 5. Poll status
        print("Waiting for completion...")
        start = time.time()
        while True:
            resp = requests.get(f"{api_url}/jobs/{job_id}/status", headers=_headers(api_key))
            resp.raise_for_status()
            status_data = resp.json()
            status = status_data.get("status", "UNKNOWN")
            elapsed = int(time.time() - start)
            print(f"\r  [{elapsed}s] {status}", end="", flush=True)
            if status in ("SUCCEEDED", "FAILED", "TIMED_OUT", "ABORTED"):
                print()
                break
            time.sleep(30)

        if status != "SUCCEEDED":
            # Show discovery warnings even on failure
            missing = status_data.get("warnings", {}).get("missing_dependencies", [])
            if missing:
                print(f"\n⚠ Warning: Missing dependencies detected — results may be incomplete:")
                for dep in missing:
                    ref_by = dep.get("referenced_by", "?")
                    print(f"  - {dep.get('path', '?')} (referenced by {ref_by})")
                print("  Consider including the project root directory as --source.\n")
            print(f"Pipeline {status}")
            sys.exit(1)

        # 5.5. Check discovery warnings
        missing = status_data.get("warnings", {}).get("missing_dependencies", [])
        if missing:
            print(f"\n⚠ Warning: Missing dependencies detected — results may be incomplete:")
            for dep in missing:
                ref_by = dep.get("referenced_by", "?")
                print(f"  - {dep.get('path', '?')} (referenced by {ref_by})")
            print("  Consider including the project root directory as --source.\n")

        # 6. Download result
        resp = requests.get(f"{api_url}/jobs/{job_id}/result", headers=_headers(api_key))
        resp.raise_for_status()
        result = resp.json()

        output = args.output or f"protocol_{job_id}.json"
        with open(output, "w") as f:
            json.dump(result, f, indent=2)

        packets = result.get("packets", [])
        types = result.get("types", [])
        print(f"\n✓ Generated: {len(packets)} packets, {len(types)} types")
        print(f"Output: {output}")

    finally:
        os.unlink(zip_path)


def cmd_status(args):
    api_url, api_key = _get_config(args)
    resp = requests.get(f"{api_url}/jobs/{args.job_id}/status", headers=_headers(api_key))
    resp.raise_for_status()
    print(json.dumps(resp.json(), indent=2))


def cmd_download(args):
    api_url, api_key = _get_config(args)
    resp = requests.get(f"{api_url}/jobs/{args.job_id}/result", headers=_headers(api_key))
    resp.raise_for_status()
    data = resp.json()
    with open(args.output, "w") as f:
        json.dump(data, f, indent=2)
    print(f"Downloaded: {len(data.get('packets', []))} packets → {args.output}")


def main():
    parser = argparse.ArgumentParser(description="Protocol Agent CLI")
    parser.add_argument("--api-url", help="API Gateway endpoint URL")
    parser.add_argument("--api-key", help="API Key for authentication")

    sub = parser.add_subparsers(dest="command")

    gen = sub.add_parser("generate", help="Generate protocol from source")
    gen.add_argument("--source", required=True, help="Source directory")
    gen.add_argument("--output", help="Output JSON path")
    gen.add_argument("--model", help="Bedrock model ID override")

    st = sub.add_parser("status", help="Check job status")
    st.add_argument("--job-id", required=True)

    dl = sub.add_parser("download", help="Download result")
    dl.add_argument("--job-id", required=True)
    dl.add_argument("--output", default="protocol.json")

    args = parser.parse_args()
    if args.command == "generate":
        cmd_generate(args)
    elif args.command == "status":
        cmd_status(args)
    elif args.command == "download":
        cmd_download(args)
    else:
        parser.print_help()


if __name__ == "__main__":
    main()
