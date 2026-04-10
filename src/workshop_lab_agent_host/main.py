"""
Hosted Agent Readiness Coach — FastAPI server exposing /responses on port 8088.

This is the Python equivalent of the .NET ChatClientAgent host.
It uses the OpenAI Python SDK with Azure credentials to provide
AI-powered responses backed by deterministic local tools.
"""

import json
import logging
import os
import sys

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse
import uvicorn
from azure.identity import DefaultAzureCredential
from openai import AzureOpenAI

# Add the src directory to path so we can import workshop_lab_core
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from workshop_lab_core import HostedAgentAdvisor

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("workshop_lab_agent")

# --- Environment ---
project_endpoint = os.environ.get("AZURE_AI_PROJECT_ENDPOINT")
if not project_endpoint:
    raise RuntimeError("AZURE_AI_PROJECT_ENDPOINT is not set.")

deployment_name = os.environ.get("MODEL_DEPLOYMENT_NAME", "gpt-4.1-mini")

logger.info("WorkshopLab Agent Host starting for project: %s", project_endpoint)
logger.info("Using deployment: %s", deployment_name)

# --- Azure OpenAI client ---
credential = DefaultAzureCredential()
token = credential.get_token("https://cognitiveservices.azure.com/.default")

# Extract the OpenAI endpoint from the project endpoint
# Project endpoint format: https://<resource>.services.ai.azure.com/api/projects/<project>
resource_host = project_endpoint.split("/api/projects")[0].replace("https://", "").replace("http://", "")
openai_endpoint = f"https://{resource_host}"

client = AzureOpenAI(
    azure_endpoint=openai_endpoint,
    azure_ad_token_provider=lambda: credential.get_token(
        "https://cognitiveservices.azure.com/.default"
    ).token,
    api_version="2025-04-01-preview",
)

# --- Advisor tools ---
advisor = HostedAgentAdvisor()

TOOLS = [
    {
        "type": "function",
        "function": {
            "name": "recommend_implementation_shape",
            "description": (
                "Recommend whether a team should start with a hosted agent "
                "and explain the implementation shape to use."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "goal": {
                        "type": "string",
                        "description": "The business goal for the agent.",
                    },
                    "needs_custom_code": {
                        "type": "string",
                        "description": (
                            "Whether the team needs custom server-side code such as "
                            "deterministic logic or enterprise integrations. Use yes or no."
                        ),
                    },
                    "needs_external_tools": {
                        "type": "string",
                        "description": (
                            "Whether the team needs external tools, MCP integrations, "
                            "or private APIs. Use yes or no."
                        ),
                    },
                    "needs_workflow": {
                        "type": "string",
                        "description": (
                            "Whether the team expects multi-step orchestration "
                            "or workflow handoffs. Use yes or no."
                        ),
                    },
                },
                "required": [
                    "goal",
                    "needs_custom_code",
                    "needs_external_tools",
                    "needs_workflow",
                ],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "build_launch_checklist",
            "description": (
                "Create a practical launch checklist for a Microsoft Foundry "
                "Hosted Agent pilot."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "agent_name": {
                        "type": "string",
                        "description": "The short agent name.",
                    },
                    "environment": {
                        "type": "string",
                        "description": (
                            "The target environment such as dev, pilot, or production."
                        ),
                    },
                },
                "required": ["agent_name", "environment"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "troubleshoot_hosted_agent",
            "description": (
                "Suggest troubleshooting guidance for a common "
                "hosted-agent symptom."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "symptom": {
                        "type": "string",
                        "description": (
                            "A short symptom or error description from the team."
                        ),
                    },
                },
                "required": ["symptom"],
            },
        },
    },
]

SYSTEM_INSTRUCTIONS = """You are a Microsoft Foundry Hosted Agent readiness coach.

Help teams choose when to use a hosted agent, prepare a safe pilot, and troubleshoot early setup issues.

When a user asks for implementation guidance:
1. Clarify the business goal when it is vague.
2. Use recommend_implementation_shape for architecture decisions.
3. Use build_launch_checklist for concrete onboarding steps.
4. Use troubleshoot_hosted_agent for setup or runtime symptoms.
5. Keep answers practical, concise, and aimed at teams getting started with Microsoft Foundry Hosted Agents.
"""


def execute_tool_call(name: str, arguments: dict) -> str:
    """Dispatch a tool call to the advisor."""
    if name == "recommend_implementation_shape":
        return advisor.recommend_implementation_shape(
            goal=arguments["goal"],
            needs_custom_code=arguments["needs_custom_code"],
            needs_external_tools=arguments["needs_external_tools"],
            needs_workflow=arguments["needs_workflow"],
        )
    elif name == "build_launch_checklist":
        return advisor.build_launch_checklist(
            agent_name=arguments["agent_name"],
            environment=arguments["environment"],
        )
    elif name == "troubleshoot_hosted_agent":
        return advisor.troubleshoot_hosted_agent(
            symptom=arguments["symptom"],
        )
    else:
        return f"Unknown tool: {name}"


# --- FastAPI app ---
app = FastAPI(title="Hosted Agent Readiness Coach")


@app.post("/responses")
async def responses(request: Request):
    """Handle the hosted-agent /responses protocol."""
    body = await request.json()

    # Parse input — supports both string and array formats
    raw_input = body.get("input", "")
    if isinstance(raw_input, str):
        user_content = raw_input
    elif isinstance(raw_input, list):
        # Array of {role, content} messages
        user_parts = [
            m.get("content", "")
            for m in raw_input
            if m.get("role") == "user"
        ]
        user_content = "\n".join(user_parts)
    else:
        user_content = str(raw_input)

    messages = [
        {"role": "system", "content": SYSTEM_INSTRUCTIONS},
        {"role": "user", "content": user_content},
    ]

    # Chat completion with tool calling loop
    max_iterations = 5
    for _ in range(max_iterations):
        response = client.chat.completions.create(
            model=deployment_name,
            messages=messages,
            tools=TOOLS,
            tool_choice="auto",
        )

        choice = response.choices[0]

        if choice.finish_reason == "tool_calls" and choice.message.tool_calls:
            messages.append(choice.message)
            for tool_call in choice.message.tool_calls:
                fn_name = tool_call.function.name
                fn_args = json.loads(tool_call.function.arguments)
                result = execute_tool_call(fn_name, fn_args)
                messages.append(
                    {
                        "role": "tool",
                        "tool_call_id": tool_call.id,
                        "content": result,
                    }
                )
        else:
            # Final assistant response
            assistant_text = choice.message.content or ""
            return JSONResponse(
                content={
                    "id": response.id,
                    "status": "completed",
                    "output": [
                        {
                            "type": "message",
                            "role": "assistant",
                            "content": [
                                {
                                    "type": "output_text",
                                    "text": assistant_text,
                                }
                            ],
                        }
                    ],
                }
            )

    # Fallback if we exceed iterations
    return JSONResponse(
        content={
            "id": "fallback",
            "status": "completed",
            "output": [
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "The agent could not complete the request within the allowed iterations.",
                        }
                    ],
                }
            ],
        }
    )


@app.get("/readiness")
async def readiness():
    """Health check endpoint."""
    return {"status": "ready"}


if __name__ == "__main__":
    logger.info("Hosted Agent Readiness Coach is listening on http://localhost:8088")
    uvicorn.run(app, host="0.0.0.0", port=8088)
