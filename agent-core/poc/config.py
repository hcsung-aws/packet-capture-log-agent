"""Bedrock LLM configuration."""

import os

REGION = os.environ.get("AWS_REGION", "us-east-1")
# Inference profile required for on-demand Sonnet 4
MODEL_ID = os.environ.get("BEDROCK_MODEL_ID", "us.anthropic.claude-sonnet-4-20250514-v1:0")
MAX_TOKENS = int(os.environ.get("BEDROCK_MAX_TOKENS", "16384"))
ANTHROPIC_VERSION = "bedrock-2023-05-31"
MAX_REVIEW_ROUNDS = 3
