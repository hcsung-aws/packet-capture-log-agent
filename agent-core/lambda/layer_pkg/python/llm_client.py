"""Bedrock LLM client — shared Lambda Layer module."""

import json
import os
import boto3
from botocore.config import Config

REGION = os.environ.get("AWS_REGION", "us-east-1")
MODEL_ID = os.environ.get("BEDROCK_MODEL_ID", "us.anthropic.claude-sonnet-4-20250514-v1:0")
MAX_TOKENS = int(os.environ.get("BEDROCK_MAX_TOKENS", "16384"))
ANTHROPIC_VERSION = "bedrock-2023-05-31"
MAX_REVIEW_ROUNDS = int(os.environ.get("MAX_REVIEW_ROUNDS", "3"))

_client = None


def _get_client():
    global _client
    if _client is None:
        _client = boto3.client(
            "bedrock-runtime",
            region_name=REGION,
            config=Config(read_timeout=300, retries={"max_attempts": 2}),
        )
    return _client


def invoke(system_prompt: str, user_message: str) -> str:
    resp = _get_client().invoke_model(
        modelId=MODEL_ID,
        contentType="application/json",
        body=json.dumps({
            "anthropic_version": ANTHROPIC_VERSION,
            "max_tokens": MAX_TOKENS,
            "system": system_prompt,
            "messages": [{"role": "user", "content": user_message}],
        }),
    )
    return json.loads(resp["body"].read())["content"][0]["text"]


def invoke_json(system_prompt: str, user_message: str) -> dict:
    raw = invoke(system_prompt, user_message)
    text = raw.strip()
    if text.startswith("```"):
        text = text[text.index("\n") + 1:]
        if text.endswith("```"):
            text = text[:-3]
        text = text.strip()
    for i, ch in enumerate(text):
        if ch in "{[":
            decoder = json.JSONDecoder()
            obj, _ = decoder.raw_decode(text, i)
            return obj
    raise ValueError(f"No JSON found in response: {text[:200]}")


def generate_and_review(gen_system, gen_user, rev_system, rev_context="", max_rounds=None):
    rounds = max_rounds or MAX_REVIEW_ROUNDS
    result = None
    for i in range(rounds):
        if i == 0:
            result = invoke_json(gen_system, gen_user)
        else:
            feedback_msg = (
                f"{gen_user}\n\n--- REVIEWER FEEDBACK (round {i}) ---\n"
                f"{review['feedback']}\n--- PREVIOUS OUTPUT ---\n"
                f"{json.dumps(result, ensure_ascii=False, indent=2)}"
            )
            result = invoke_json(gen_system, feedback_msg)

        review_user = f"Generated output:\n```json\n{json.dumps(result, ensure_ascii=False, indent=2)}\n```"
        if rev_context:
            review_user = f"{rev_context}\n\n{review_user}"

        review_raw = invoke_json(rev_system, review_user)
        review = review_raw if isinstance(review_raw, dict) else {
            "approved": False, "feedback": str(review_raw)[:200]
        }

        if review.get("approved", False):
            return {"result": result, "rounds": i + 1, "approved": True}

    return {"result": result, "rounds": rounds, "approved": False}
