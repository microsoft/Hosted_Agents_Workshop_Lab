#!/usr/bin/env python3
"""Smoke-test a deployed Microsoft Foundry hosted agent via the Responses API.

Reads a JSON catalog of prompts + assertions, POSTs each prompt to the agent's
dedicated Responses endpoint, and validates the response text. The process exits
non-zero if any test fails, so it works as a CI gate after deployment.

Auth: uses the FOUNDRY_TOKEN environment variable when set (GitHub Actions),
otherwise falls back to the local Azure CLI session
(`az account get-access-token --resource https://ai.azure.com`).

Endpoint contract (current hosted-agent spec):
  POST {project_endpoint}/agents/{name}/endpoint/protocols/openai/responses
       ?api-version=2025-11-15-preview
  Header: Foundry-Features: HostedAgents=V1Preview
  Body:   {"input": "<prompt>", "stream": false}
"""
from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import urllib.error
import urllib.request

API_VERSION = "2025-11-15-preview"
TOKEN_RESOURCE = "https://ai.azure.com"


def get_token() -> str:
    """Return a bearer token from FOUNDRY_TOKEN or the local Azure CLI session."""
    token = os.environ.get("FOUNDRY_TOKEN", "").strip()
    if token:
        return token
    try:
        result = subprocess.run(
            [
                "az", "account", "get-access-token",
                "--resource", TOKEN_RESOURCE,
                "--query", "accessToken", "-o", "tsv",
            ],
            capture_output=True,
            text=True,
            check=True,
            shell=(os.name == "nt"),
        )
        return result.stdout.strip()
    except Exception as exc:  # noqa: BLE001 - surface any auth failure clearly
        print(
            "ERROR: could not acquire a token. Set FOUNDRY_TOKEN or run 'az login'.\n"
            f"       {exc}",
            file=sys.stderr,
        )
        sys.exit(2)


def post_response(endpoint: str, agent: str, token: str, timeout: float, body: dict) -> dict:
    url = (
        f"{endpoint.rstrip('/')}/agents/{agent}/endpoint/protocols/openai/responses"
        f"?api-version={API_VERSION}"
    )
    request = urllib.request.Request(url, data=json.dumps(body).encode("utf-8"), method="POST")
    request.add_header("Authorization", f"Bearer {token}")
    request.add_header("Content-Type", "application/json")
    request.add_header("Foundry-Features", "HostedAgents=V1Preview")
    with urllib.request.urlopen(request, timeout=timeout) as response:
        return json.loads(response.read().decode("utf-8"))


def extract_text(payload: dict) -> str:
    """Pull the assistant text out of a Responses API payload."""
    convenience = payload.get("output_text")
    if isinstance(convenience, str) and convenience:
        return convenience
    parts: list[str] = []
    for item in payload.get("output", []) or []:
        for content in item.get("content", []) or []:
            text = content.get("text")
            if isinstance(text, str):
                parts.append(text)
    return "\n".join(parts)


def evaluate(text: str, assertions: dict) -> list[str]:
    """Return a list of failure descriptions (empty means the test passed)."""
    lowered = text.lower()
    failures: list[str] = []

    any_terms = assertions.get("contains_any")
    if any_terms and not any(term.lower() in lowered for term in any_terms):
        failures.append(f"contains_any {any_terms}")

    all_terms = assertions.get("contains_all")
    if all_terms:
        missing = [term for term in all_terms if term.lower() not in lowered]
        if missing:
            failures.append(f"contains_all missing {missing}")

    none_terms = assertions.get("contains_none")
    if none_terms:
        present = [term for term in none_terms if term.lower() in lowered]
        if present:
            failures.append(f"contains_none but found {present}")

    return failures


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-endpoint", required=True)
    parser.add_argument("--agent-name", required=True)
    parser.add_argument("--tests-file", default="deployment/smoke-tests.json")
    parser.add_argument("--timeout", type=float, default=120.0)
    args = parser.parse_args()

    with open(args.tests_file, encoding="utf-8") as handle:
        catalog = json.load(handle)
    tests = catalog.get("tests", [])
    token = get_token()

    print(f"Project endpoint : {args.project_endpoint}")
    print(f"Agent            : {args.agent_name}")
    print(f"Tests            : {len(tests)} from {args.tests_file}")
    print(f"Per-req timeout  : {args.timeout}s\n")

    response_ids: dict[str, str] = {}
    passed = 0

    for test in tests:
        test_id = test.get("id", "<unnamed>")
        body: dict = {"input": test["prompt"], "stream": False}

        prev_key = test.get("use_previous_response_id")
        if prev_key:
            if prev_key not in response_ids:
                print(f"  FAIL  {test_id}  (no saved response id '{prev_key}')")
                continue
            body["previous_response_id"] = response_ids[prev_key]

        try:
            payload = post_response(args.project_endpoint, args.agent_name, token, args.timeout, body)
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode("utf-8", "replace")[:200]
            print(f"  FAIL  {test_id}  (HTTP {exc.code}: {detail})")
            continue
        except Exception as exc:  # noqa: BLE001
            print(f"  FAIL  {test_id}  ({exc})")
            continue

        text = extract_text(payload)

        save_key = test.get("save_response_id_as")
        if save_key and payload.get("id"):
            response_ids[save_key] = payload["id"]

        failures = evaluate(text, test.get("assertions", {}))
        if failures:
            print(f"  FAIL  {test_id}  -> {'; '.join(failures)}")
            print(f"        response: {text[:300]}")
        else:
            print(f"  PASS  {test_id}")
            passed += 1

    print(f"\n=== Summary: {passed}/{len(tests)} passed ===")
    sys.exit(0 if passed == len(tests) else 1)


if __name__ == "__main__":
    main()
