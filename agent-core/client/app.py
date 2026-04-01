"""Protocol Agent Web UI — Flask wrapper around CLI functionality."""

import json
import os
import sys
import tempfile
import threading
import time

from flask import Flask, request, jsonify, send_from_directory

# Add parent to path for cli module
sys.path.insert(0, os.path.dirname(__file__))
import cli

app = Flask(__name__, static_folder="web", static_url_path="")

# In-memory job tracking
_jobs = {}


@app.route("/")
def index():
    return send_from_directory("web", "index.html")


@app.route("/api/generate", methods=["POST"])
def api_generate():
    data = request.json or {}
    source_dir = data.get("source_dir", "")
    model_id = data.get("model_id", "")

    if not source_dir or not os.path.isdir(source_dir):
        return jsonify({"error": "Invalid source_dir"}), 400

    job_id = f"web-{int(time.time())}"
    _jobs[job_id] = {"status": "UPLOADING", "source_dir": source_dir}

    def _run():
        try:
            _jobs[job_id]["status"] = "UPLOADING"
            cli.upload(source_dir, job_id, cli.DEFAULT_BUCKET, cli.DEFAULT_REGION)

            _jobs[job_id]["status"] = "RUNNING"
            exec_arn = cli.start_pipeline(job_id, cli.DEFAULT_SFN_ARN, cli.DEFAULT_REGION, model_id or None)
            _jobs[job_id]["execution_arn"] = exec_arn

            status, _ = cli.wait_for_completion(exec_arn, cli.DEFAULT_REGION, poll_interval=10)
            _jobs[job_id]["status"] = status

            if status == "SUCCEEDED":
                tmp = tempfile.mktemp(suffix=".json")
                data = cli.download_result(job_id, tmp, cli.DEFAULT_BUCKET, cli.DEFAULT_REGION)
                _jobs[job_id]["result"] = {
                    "packet_count": len(data.get("packets", [])),
                    "type_count": len(data.get("types", [])),
                }
                os.unlink(tmp)
        except Exception as e:
            _jobs[job_id]["status"] = "ERROR"
            _jobs[job_id]["error"] = str(e)

    threading.Thread(target=_run, daemon=True).start()
    return jsonify({"job_id": job_id})


@app.route("/api/jobs")
def api_jobs():
    return jsonify(_jobs)


@app.route("/api/jobs/<job_id>")
def api_job_status(job_id):
    if job_id in _jobs:
        return jsonify(_jobs[job_id])
    return jsonify({"error": "Not found"}), 404


@app.route("/api/jobs/<job_id>/result")
def api_job_result(job_id):
    try:
        import boto3
        s3 = boto3.client("s3", region_name=cli.DEFAULT_REGION)
        resp = s3.get_object(Bucket=cli.DEFAULT_BUCKET, Key=f"jobs/{job_id}/protocol.json")
        data = json.loads(resp["Body"].read())
        return jsonify(data)
    except Exception as e:
        return jsonify({"error": str(e)}), 404


if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8090
    print(f"Protocol Agent Web UI: http://localhost:{port}")
    app.run(host="0.0.0.0", port=port, debug=False)
