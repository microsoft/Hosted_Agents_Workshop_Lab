"""
Foundry agent deployment helper — creates or updates a hosted agent definition
in Microsoft Foundry using the REST API.

Usage:
    python scripts/deploy_foundry_agent.py \
        --project-endpoint <endpoint> \
        --manifest src/workshop_lab_agent_host/agent.yaml \
        --set AZURE_AI_PROJECT_ENDPOINT=<endpoint> \
        --set chat=gpt-4.1-mini
"""

import argparse
import json
import os
import sys

import yaml
from azure.identity import DefaultAzureCredential
import requests


def parse_args():
    parser = argparse.ArgumentParser(description="Deploy a Foundry agent from manifest or definition.")
    parser.add_argument("--project-endpoint", default=os.environ.get("AZURE_AI_PROJECT_ENDPOINT"))
    parser.add_argument("--agent-name", default=None)
    parser.add_argument("--manifest", default=None)
    parser.add_argument("--agent-definition", default=None)
    parser.add_argument("--agent-id", default=os.environ.get("FOUNDRY_AGENT_ID"))
    parser.add_argument("--set", dest="replacements", action="append", default=[])
    return parser.parse_args()


def main():
    args = parse_args()

    project_endpoint = args.project_endpoint
    if not project_endpoint:
        print("ERROR: --project-endpoint or AZURE_AI_PROJECT_ENDPOINT must be set.", file=sys.stderr)
        sys.exit(1)

    # Resolve agent definition
    if args.agent_definition:
        definition = json.loads(args.agent_definition)
        agent_name = args.agent_name or "sample-hosted-agent"
        print(f"Using provided agent definition for agent '{agent_name}'")
    elif args.manifest:
        with open(args.manifest, "r", encoding="utf-8") as f:
            content = f.read()

        # Apply --set replacements
        for replacement in args.replacements:
            key, _, value = replacement.partition("=")
            if not key or not value:
                print(f"ERROR: Invalid --set value '{replacement}'. Use NAME=VALUE.", file=sys.stderr)
                sys.exit(1)
            content = content.replace(f"${{{key}}}", value)
            content = content.replace(f"{{{{{key}}}}}", value)

        # Parse YAML
        definition = yaml.safe_load(content)
        agent_name = definition.get("name", args.agent_name or "sample-hosted-agent")
        print(f"Using agent definition from manifest '{args.manifest}'")
    else:
        print("ERROR: Either --agent-definition or --manifest must be provided.", file=sys.stderr)
        sys.exit(1)

    agent_kind = definition.get("kind", "unknown")
    print(f"Agent Kind: {agent_kind}")

    # Get token
    credential = DefaultAzureCredential()
    token = credential.get_token("https://ai.azure.com/.default").token

    endpoint = project_endpoint.rstrip("/")
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {token}",
        "api-version": "2025-01-01-preview",
    }

    if args.agent_id:
        # Update existing agent
        url = f"{endpoint}/agents/{args.agent_id}"
        payload = {"definition": definition}
        resp = requests.put(url, json=payload, headers=headers, timeout=60)
        resp.raise_for_status()
        print(f"Updated Foundry agent '{args.agent_id}' from definition.")
    else:
        # Create new agent
        url = f"{endpoint}/agents/{agent_name}/versions"
        payload = {"definition": definition}
        resp = requests.post(url, json=payload, headers=headers, timeout=60)
        resp.raise_for_status()
        print(f"Created hosted agent '{agent_name}'.")

    print("Next step: start the hosted agent container and verify status in the Foundry portal or through Foundry MCP tools.")


if __name__ == "__main__":
    main()
