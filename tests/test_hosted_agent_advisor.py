import os
import sys

import pytest

# Add src to path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

from workshop_lab_core import HostedAgentAdvisor


@pytest.fixture
def advisor():
    return HostedAgentAdvisor()


class TestRecommendImplementationShape:
    def test_returns_hosted_agent_when_custom_logic_is_needed(self, advisor):
        result = advisor.recommend_implementation_shape(
            goal="Onboard teams to Hosted Agents",
            needs_custom_code="yes",
            needs_external_tools="no",
            needs_workflow="no",
        )
        assert "Recommended implementation: Hosted agent" in result
        assert "custom server-side logic" in result

    def test_returns_prompt_agent_when_scenario_is_lightweight(self, advisor):
        result = advisor.recommend_implementation_shape(
            goal="Prototype a simple FAQ assistant",
            needs_custom_code="no",
            needs_external_tools="no",
            needs_workflow="no",
        )
        assert "Recommended implementation: Prompt agent" in result

    def test_returns_full_platform_when_all_three_required(self, advisor):
        result = advisor.recommend_implementation_shape(
            goal="Enterprise agent with APIs, tools, and orchestration",
            needs_custom_code="yes",
            needs_external_tools="yes",
            needs_workflow="yes",
        )
        assert "Recommended implementation: Hosted agent (full platform)" in result
        assert "full-platform hosted agent use case" in result
        assert "modular tool layer" in result


class TestBuildLaunchChecklist:
    def test_includes_core_hosted_agent_steps(self, advisor):
        result = advisor.build_launch_checklist("triage-coach", "pilot")
        assert "triage-coach" in result
        assert "pilot" in result
        assert "agent.yaml" in result
        assert "linux/amd64" in result


class TestTroubleshootHostedAgent:
    @pytest.mark.parametrize(
        "symptom, expected",
        [
            ("requests to /responses fail after startup", "/responses"),
            ("docker image fails on amd64", "linux/amd64"),
            ("credential login problem", "AZURE_AI_PROJECT_ENDPOINT"),
        ],
    )
    def test_returns_targeted_guidance(self, advisor, symptom, expected):
        result = advisor.troubleshoot_hosted_agent(symptom)
        assert expected in result
