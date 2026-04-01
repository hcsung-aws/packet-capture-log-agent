"""Bedrock LLM client with Generator + Reviewer 2-agent pattern."""

import json
import boto3
import config


def _create_client():
    from botocore.config import Config
    return boto3.client(
        "bedrock-runtime",
        region_name=config.REGION,
        config=Config(read_timeout=300, retries={"max_attempts": 2}),
    )


_client = None


def _get_client():
    global _client
    if _client is None:
        _client = _create_client()
    return _client


def invoke(system_prompt: str, user_message: str, model_id: str | None = None) -> str:
    """Single LLM invocation."""
    resp = _get_client().invoke_model(
        modelId=model_id or config.MODEL_ID,
        contentType="application/json",
        body=json.dumps({
            "anthropic_version": config.ANTHROPIC_VERSION,
            "max_tokens": config.MAX_TOKENS,
            "system": system_prompt,
            "messages": [{"role": "user", "content": user_message}],
        }),
    )
    return json.loads(resp["body"].read())["content"][0]["text"]


def invoke_json(system_prompt: str, user_message: str, model_id: str | None = None) -> dict:
    """Invoke and parse JSON from response. Extracts first JSON object/array."""
    raw = invoke(system_prompt, user_message, model_id)
    text = raw.strip()
    # Strip markdown fences
    if text.startswith("```"):
        first_nl = text.index("\n")
        text = text[first_nl + 1:]
        if text.endswith("```"):
            text = text[:-3]
        text = text.strip()
    # Find first { or [ and extract JSON
    for i, ch in enumerate(text):
        if ch in "{[":
            decoder = json.JSONDecoder()
            obj, _ = decoder.raw_decode(text, i)
            return obj
    raise ValueError(f"No JSON found in response: {text[:200]}")


def generate_and_review(
    generator_system: str,
    generator_user: str,
    reviewer_system: str,
    context_for_reviewer: str = "",
    max_rounds: int | None = None,
) -> dict:
    """2-agent pattern: Generator produces JSON, Reviewer validates.

    Returns the final accepted JSON result.
    """
    rounds = max_rounds or config.MAX_REVIEW_ROUNDS
    result = None

    for i in range(rounds):
        # Generate
        if i == 0:
            result = invoke_json(generator_system, generator_user)
        else:
            # Re-generate with reviewer feedback
            feedback_msg = (
                f"{generator_user}\n\n"
                f"--- REVIEWER FEEDBACK (round {i}) ---\n{review['feedback']}\n"
                f"--- PREVIOUS OUTPUT ---\n{json.dumps(result, ensure_ascii=False, indent=2)}"
            )
            result = invoke_json(generator_system, feedback_msg)

        # Review
        review_user = (
            f"Generated output:\n```json\n{json.dumps(result, ensure_ascii=False, indent=2)}\n```"
        )
        if context_for_reviewer:
            review_user = f"{context_for_reviewer}\n\n{review_user}"

        review_raw = invoke_json(reviewer_system, review_user)
        # Normalize: reviewer might return a list or non-dict
        if isinstance(review_raw, dict):
            review = review_raw
        else:
            review = {"approved": False, "feedback": f"Unexpected reviewer format: {str(review_raw)[:200]}"}

        if review.get("approved", False):
            print(f"  ✓ Approved after {i + 1} round(s)")
            return result

        print(f"  ✗ Round {i + 1} rejected: {review.get('feedback', '')[:100]}")

    print(f"  ⚠ Max rounds ({rounds}) reached, using last result")
    return result
