"""S3 helper for Lambda functions."""

import json
import os
import boto3

BUCKET = os.environ.get("S3_BUCKET", "")
_s3 = None


def _get_s3():
    global _s3
    if _s3 is None:
        _s3 = boto3.client("s3")
    return _s3


def read_json(key: str) -> dict:
    resp = _get_s3().get_object(Bucket=BUCKET, Key=key)
    return json.loads(resp["Body"].read())


def write_json(key: str, data: dict):
    _get_s3().put_object(
        Bucket=BUCKET, Key=key,
        Body=json.dumps(data, ensure_ascii=False, indent=2),
        ContentType="application/json",
    )


def read_text(key: str) -> str:
    resp = _get_s3().get_object(Bucket=BUCKET, Key=key)
    return resp["Body"].read().decode("utf-8")


def list_keys(prefix: str) -> list[str]:
    resp = _get_s3().list_objects_v2(Bucket=BUCKET, Prefix=prefix)
    return [obj["Key"] for obj in resp.get("Contents", [])]


def write_text(key: str, text: str):
    _get_s3().put_object(Bucket=BUCKET, Key=key, Body=text.encode("utf-8"))
