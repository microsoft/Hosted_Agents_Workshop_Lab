import os


class HostedAgentAdvisor:
    """Deterministic business-logic tools for the Hosted Agent Readiness Coach."""

    def recommend_implementation_shape(
        self,
        goal: str,
        needs_custom_code: str,
        needs_external_tools: str,
        needs_workflow: str,
    ) -> str:
        requires_code = _is_affirmative(needs_custom_code)
        requires_tools = _is_affirmative(needs_external_tools)
        requires_workflow = _is_affirmative(needs_workflow)

        all_three = requires_code and requires_tools and requires_workflow

        recommendation = (
            "Hosted agent (full platform)"
            if all_three
            else "Hosted agent"
            if requires_code or requires_tools or requires_workflow
            else "Prompt agent"
        )

        reasons: list[str] = []

        if all_three:
            reasons.append(
                "the scenario requires custom code, external tool access, and "
                "multi-step orchestration — this is a full-platform hosted agent use case"
            )
        else:
            if requires_code:
                reasons.append(
                    "custom server-side logic or enterprise integrations are required"
                )

            if requires_tools:
                reasons.append("tool access or MCP connectivity is required")

            if requires_workflow:
                reasons.append(
                    "the scenario benefits from multi-step orchestration"
                )

        if not reasons:
            reasons.append(
                "the scenario can start with lightweight prompting and does not "
                "need custom runtime logic yet"
            )

        next_step = (
            "Suggested next step: design the agent with a modular tool layer, "
            "separate orchestration logic, and an MCP integration plan. Validate "
            "the /responses contract locally before adding external connections."
            if all_three
            else "Suggested next step: create a code-based hosted agent with local tools "
            "first, then add project-specific connections once the /responses contract "
            "works locally."
            if recommendation == "Hosted agent"
            else "Suggested next step: validate the assistant prompt first, then upgrade "
            "to a hosted agent when you need tools, stateful logic, or controlled "
            "integrations."
        )

        return os.linesep.join(
            [
                f"Recommended implementation: {recommendation}",
                f"Scenario goal: {goal}",
                f"Why: {'; '.join(reasons)}.",
                next_step,
            ]
        )

    def build_launch_checklist(self, agent_name: str, environment: str) -> str:
        normalized_name = (
            agent_name.strip() if agent_name and agent_name.strip() else "sample-hosted-agent"
        )
        normalized_env = (
            environment.strip() if environment and environment.strip() else "dev"
        )

        checklist = [
            f"1. Confirm the agent name '{normalized_name}' follows hosted-agent naming rules.",
            f"2. Create or verify the '{normalized_env}' environment variables: AZURE_AI_PROJECT_ENDPOINT and MODEL_DEPLOYMENT_NAME.",
            "3. Validate that agent.yaml declares kind 'hosted' and protocol 'responses' v1.",
            "4. Run the agent locally and send a POST request to /responses before attempting any deployment.",
            "5. Make sure the Dockerfile exposes port 8088 and can build for linux/amd64.",
            "6. Add a CI check that restores, builds, and tests the solution on every pull request.",
            "7. Document one example request, one expected response pattern, and one troubleshooting path for the pilot team.",
        ]
        return os.linesep.join(checklist)

    def troubleshoot_hosted_agent(self, symptom: str) -> str:
        normalized = (symptom or "").strip()

        if "8088" in normalized.lower() or "responses" in normalized.lower():
            return (
                "Check that the agent host is running, port 8088 is exposed, and you "
                "are sending a POST request to /responses with JSON content. Hosted "
                "agents should be validated locally before deployment."
            )

        if any(
            kw in normalized.lower()
            for kw in ("endpoint", "credential", "login")
        ):
            return (
                "Verify AZURE_AI_PROJECT_ENDPOINT, confirm az login has access to "
                "the Foundry project, and ensure the project contains a usable "
                "OpenAI connection and deployment name."
            )

        if any(
            kw in normalized.lower()
            for kw in ("docker", "amd64", "container")
        ):
            return (
                "Build the container for linux/amd64, confirm port 8088 is exposed, "
                "and keep the hosted-agent HTTP server as the default entrypoint."
            )

        return (
            "Start with the local validation path: confirm environment variables, "
            "run the host locally, test /responses, then move outward to Docker and "
            "Foundry deployment only after the local contract succeeds."
        )


def _is_affirmative(value: str | None) -> bool:
    if not value or not value.strip():
        return False
    return value.strip().lower() in ("yes", "true", "y")
