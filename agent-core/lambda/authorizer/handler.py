"""API Key authorizer for HTTP API v2."""

import os

EXPECTED_KEY = os.environ.get("API_KEY", "")


def handler(event, context):
    key = event.get("headers", {}).get("x-api-key", "")
    return {"isAuthorized": key == EXPECTED_KEY and key != ""}
