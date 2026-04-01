"""Orchestrator Lambda — API Gateway → Step Functions."""

import json
import os
import uuid
import boto3

SFN_ARN = os.environ.get("SFN_PIPELINE_ARN", "")
BUCKET = os.environ.get("S3_BUCKET", "")
sfn = boto3.client("stepfunctions")
s3 = boto3.client("s3")


def handler(event, context):
    method = event.get("httpMethod", "")
    path = event.get("path", "")
    body = json.loads(event.get("body") or "{}")

    if method == "POST" and path == "/jobs":
        return _create_job()
    if method == "POST" and "/pipeline" in path:
        job_id = path.split("/")[2]
        return _start_pipeline(job_id, body)
    if method == "GET" and "/status" in path:
        job_id = path.split("/")[2]
        return _get_status(job_id, body)
    if method == "GET" and "/result" in path:
        job_id = path.split("/")[2]
        return _get_result(job_id)

    return _resp(404, {"error": "Not found"})


def _create_job():
    job_id = str(uuid.uuid4())[:8]
    # Generate presigned URLs for source upload
    upload_url = s3.generate_presigned_url(
        "put_object",
        Params={"Bucket": BUCKET, "Key": f"jobs/{job_id}/source.zip"},
        ExpiresIn=3600,
    )
    return _resp(200, {"job_id": job_id, "upload_url": upload_url})


def _start_pipeline(job_id, body):
    model_id = body.get("model_id", "")
    execution = sfn.start_execution(
        stateMachineArn=SFN_ARN,
        name=f"{job_id}-{str(uuid.uuid4())[:4]}",
        input=json.dumps({"job_id": job_id, **({"model_id": model_id} if model_id else {})}),
    )
    return _resp(200, {
        "job_id": job_id,
        "execution_arn": execution["executionArn"],
    })


def _get_status(job_id, body):
    arn = body.get("execution_arn") or ""
    if not arn:
        # Find latest execution for this job
        execs = sfn.list_executions(stateMachineArn=SFN_ARN, maxResults=20)
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
    try:
        protocol = s3.get_object(Bucket=BUCKET, Key=f"jobs/{job_id}/protocol.json")
        data = json.loads(protocol["Body"].read())
        return _resp(200, data)
    except s3.exceptions.NoSuchKey:
        return _resp(404, {"error": "Result not ready"})


def _resp(code, body):
    return {
        "statusCode": code,
        "headers": {"Content-Type": "application/json", "Access-Control-Allow-Origin": "*"},
        "body": json.dumps(body, default=str),
    }
