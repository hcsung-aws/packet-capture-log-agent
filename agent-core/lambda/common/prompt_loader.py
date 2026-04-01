"""Prompt loader for Lambda functions."""

import os

_PROMPT_DIR = os.path.join(os.path.dirname(__file__), "prompts")


def load(name: str) -> str:
    with open(os.path.join(_PROMPT_DIR, name)) as f:
        return f.read()
