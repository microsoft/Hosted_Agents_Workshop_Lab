"""
Chat UI — Flask web application that forwards prompts
to the deployed Foundry hosted agent.
"""

import json
import logging
import os

from flask import Flask, render_template, request, jsonify
from azure.identity import DefaultAzureCredential
import requests as http_requests

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("workshop_lab_chat_ui")

app = Flask(__name__)

# --- Configuration ---
FOUNDRY_PROJECT_ENDPOINT = os.environ.get(
    "FOUNDRY_PROJECT_ENDPOINT",
    os.environ.get(
        "AZURE_AI_PROJECT_ENDPOINT",
        "https://<account>.services.ai.azure.com/api/projects/<project>",
    ),
)
FOUNDRY_AGENT_NAME = os.environ.get("FOUNDRY_AGENT_NAME", "hosted-agent-readiness-coach")
FOUNDRY_API_VERSION = os.environ.get("FOUNDRY_API_VERSION", "2025-01-01-preview")

credential = DefaultAzureCredential()


def _get_token() -> str:
    """Acquire a bearer token for the Foundry API."""
    token = credential.get_token("https://ai.azure.com/.default")
    return token.token


def send_to_agent(user_prompt: str) -> str:
    """Send a prompt to the deployed Foundry hosted agent and return the assistant text."""
    endpoint = FOUNDRY_PROJECT_ENDPOINT.rstrip("/")
    url = f"{endpoint}/openai/v1/responses"

    token = _get_token()

    payload = {
        "input": [{"role": "user", "content": user_prompt}],
        "agent_reference": {
            "name": FOUNDRY_AGENT_NAME,
            "type": "agent_reference",
        },
    }

    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {token}",
        "api-version": FOUNDRY_API_VERSION,
    }

    last_error = None
    for attempt in range(2):
        try:
            resp = http_requests.post(url, json=payload, headers=headers, timeout=45)
            if not resp.ok:
                raise RuntimeError(
                    f"Foundry request failed with {resp.status_code}: {resp.text}"
                )
            return _extract_assistant_text(resp.text)
        except (http_requests.RequestException, RuntimeError) as exc:
            last_error = exc
            if attempt == 0:
                import time

                time.sleep(0.5)

    raise RuntimeError(f"Foundry request failed after retry: {last_error}")


def _extract_assistant_text(response_json: str) -> str:
    """Parse the Foundry response JSON and extract the assistant's text."""
    try:
        data = json.loads(response_json)
    except json.JSONDecodeError:
        return response_json

    output = data.get("output")
    if not isinstance(output, list):
        return response_json

    for item in output:
        if item.get("type") != "message":
            continue
        content = item.get("content")
        if not isinstance(content, list):
            continue
        for part in content:
            if part.get("type") != "output_text":
                continue
            text = part.get("text")
            if text is not None:
                return text

    return response_json


@app.route("/")
def index():
    """Render the chat UI page."""
    return render_template("index.html")


@app.route("/api/chat", methods=["POST"])
def chat():
    """API endpoint for sending prompts to the Foundry agent."""
    body = request.get_json()
    prompt = body.get("prompt", "").strip()
    if not prompt:
        return jsonify({"error": "Prompt is required."}), 400

    try:
        reply = send_to_agent(prompt)
        return jsonify({"reply": reply})
    except Exception as exc:
        logger.exception("Error calling Foundry agent")
        return jsonify({"error": str(exc)}), 500


if __name__ == "__main__":
    logger.info("Chat UI starting on http://localhost:5075")
    app.run(host="0.0.0.0", port=5075, debug=True)
