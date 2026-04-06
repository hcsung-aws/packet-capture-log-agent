"""Orchestrator Lambda — API Gateway HTTP API v2 → Step Functions."""

import io
import json
import os
import uuid
import zipfile
import boto3

SFN_ARN = os.environ.get("SFN_PIPELINE_ARN", "")
BUCKET = os.environ.get("S3_BUCKET", "")
sfn = boto3.client("stepfunctions")
s3 = boto3.client("s3")

CODE_EXT = {".h", ".hpp", ".c", ".cpp", ".cc", ".cxx", ".cs", ".java", ".py", ".go", ".rs", ".proto", ".fbs"}
SKIP_EXT = {".exe", ".dll", ".obj", ".pdb", ".lib", ".o", ".so", ".png", ".jpg", ".zip", ".gz"}


def handler(event, context):
    # HTTP API v2 payload format
    http = event.get("requestContext", {}).get("http", {})
    method = http.get("method", "")
    path = event.get("rawPath", "")
    params = event.get("pathParameters", {}) or {}
    body = json.loads(event.get("body") or "{}")

    if method == "POST" and path == "/jobs":
        return _create_job()
    if method == "POST" and path.endswith("/pipeline"):
        return _start_pipeline(params.get("id", ""), body)
    if method == "GET" and path.endswith("/status"):
        return _get_status(params.get("id", ""))
    if method == "GET" and path.endswith("/result"):
        return _get_result(params.get("id", ""))

    return _resp(404, {"error": "Not found"})


def _create_job():
    job_id = str(uuid.uuid4())[:8]
    upload_url = s3.generate_presigned_url(
        "put_object",
        Params={"Bucket": BUCKET, "Key": f"jobs/{job_id}/source.zip"},
        ExpiresIn=3600,
    )
    return _resp(200, {"job_id": job_id, "upload_url": upload_url})


def _unzip_source(job_id):
    """Download source.zip from S3, extract code files, upload individually."""
    zip_key = f"jobs/{job_id}/source.zip"
    resp = s3.get_object(Bucket=BUCKET, Key=zip_key)
    buf = io.BytesIO(resp["Body"].read())

    count = 0
    with zipfile.ZipFile(buf) as zf:
        for name in zf.namelist():
            if name.endswith("/"):
                continue
            ext = os.path.splitext(name)[1].lower()
            if ext in SKIP_EXT or ext not in CODE_EXT:
                continue
            data = zf.read(name)
            s3.put_object(Bucket=BUCKET, Key=f"jobs/{job_id}/source/{name}", Body=data)
            count += 1
    return count


def _start_pipeline(job_id, body):
    if not job_id:
        return _resp(400, {"error": "Missing job_id"})

    # Unzip source files
    try:
        file_count = _unzip_source(job_id)
    except Exception as e:
        return _resp(400, {"error": f"Failed to unzip source: {e}"})

    model_id = body.get("model_id", "")
    inp = {"job_id": job_id}
    if model_id:
        inp["model_id"] = model_id

    execution = sfn.start_execution(
        stateMachineArn=SFN_ARN,
        name=f"{job_id}-{str(uuid.uuid4())[:4]}",
        input=json.dumps(inp),
    )
    return _resp(200, {
        "job_id": job_id,
        "execution_arn": execution["executionArn"],
        "file_count": file_count,
    })


def _get_status(job_id):
    if not job_id:
        return _resp(400, {"error": "Missing job_id"})

    # Find latest execution for this job
    execs = sfn.list_executions(stateMachineArn=SFN_ARN, maxResults=20)
    arn = ""
    for e in execs.get("executions", []):
        if e["name"].startswith(job_id):
            arn = e["executionArn"]
            break
    if not arn:
        return _resp(404, {"error": "No execution found"})

    desc = sfn.describe_execution(executionArn=arn)
    result = {"status": desc["status"], "start": str(desc.get("startDate", ""))}
    if desc["status"] in ("SUCCEEDED", "FAILED"):
        result["output"] = json.loads(desc.get("output", "{}"))
        if desc.get("stopDate"):
            result["end"] = str(desc["stopDate"])
    return _resp(200, result)


def _get_result(job_id):
    if not job_id:
        return _resp(400, {"error": "Missing job_id"})
    try:
        obj = s3.get_object(Bucket=BUCKET, Key=f"jobs/{job_id}/protocol.json")
        data = json.loads(obj["Body"].read())
        return _resp(200, data)
    except s3.exceptions.NoSuchKey:
        return _resp(404, {"error": "Result not ready"})


def _resp(code, body):
    return {
        "statusCode": code,
        "headers": {"Content-Type": "application/json", "Access-Control-Allow-Origin": "*"},
        "body": json.dumps(body, default=str),
    }
