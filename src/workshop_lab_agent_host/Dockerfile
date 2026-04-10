FROM python:3.12-slim AS base

WORKDIR /app

# Install uv
COPY --from=ghcr.io/astral-sh/uv:latest /uv /usr/local/bin/uv

# Copy project metadata and lock file first for layer caching
COPY pyproject.toml uv.lock ./

# Copy source packages
COPY src/workshop_lab_core/ ./src/workshop_lab_core/
COPY src/workshop_lab_agent_host/ ./src/workshop_lab_agent_host/

# Install dependencies (frozen = use lock file exactly)
RUN uv sync --frozen --no-dev

# Create non-root user
RUN groupadd -r appgroup && useradd -r -g appgroup appuser
RUN chown -R appuser:appgroup /app
USER appuser

EXPOSE 8088

ENTRYPOINT ["uv", "run", "python", "src/workshop_lab_agent_host/main.py"]
