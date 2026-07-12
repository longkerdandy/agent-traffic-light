# Project Directory Structure

This document defines the repository layout for `agent-traffic-light`. It keeps the **control server** and **agent integrations** as two clearly separated top-level concerns while keeping future agents easy to add.

## Design Principles

1. **Server and agents are first-class, separate directories** at the repository root. No hidden `src/` layer.
2. **One directory per agent** under `agents/`; each agent owns its own language/runtime choices, scripts, and tests.
3. **Everything server-related lives under `server/`**: the ASP.NET Core host, shared contracts, server tests, and service-install scripts.
4. **Scripts are layered**: per-component scripts live next to their code; top-level `scripts/` only holds cross-component orchestration.
5. **Docs and reference material stay top-level** so installers and architecture notes are easy to find.
6. **Reference material is read-only**: files under `reference/` come from the official delivery package and must not be edited.

## Directory Tree

```text
agent-traffic-light/
├── .gitignore
├── AGENTS.md
├── README.md
├── agent-traffic-light.sln          # .NET solution (server projects)
├── Directory.Build.props            # Common MSBuild properties
├── global.json                      # .NET SDK version pin
├── .editorconfig
├── docs/
│   ├── plan/
│   │   ├── v0.1-server-scaffold-plan.md
│   │   └── v1.0-development-plan.md
│   └── architecture/
│       ├── directory-structure.md   # this file
│       ├── api-contract.md          # HTTP API + SSE schema (to be added)
│       └── state-machine.md         # event-to-command mapping reference (to be added)
├── reference/                       # official delivery package; DO NOT MODIFY
│   └── agentcore-light-v1-delivery-20260611/
├── scripts/                         # cross-component orchestration scripts
│   └── install-all.sh
├── server/                          # everything server-side
│   ├── AgentTrafficLightServer/           # ASP.NET Core control service
│   │   ├── AgentTrafficLightServer.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── Properties/
│   │   │   └── launchSettings.json
│   │   ├── Configuration/
│   │   │   └── ServerOptions.cs
│   │   ├── Endpoints/
│   │   │   ├── HookEndpoints.cs
│   │   │   ├── HeartbeatEndpoints.cs
│   │   │   ├── LightEndpoints.cs
│   │   │   └── StatusEndpoints.cs
│   │   ├── Models/
│   │   │   ├── Session.cs
│   │   │   ├── HookPayload.cs
│   │   │   └── StatusResponse.cs
│   │   ├── Services/
│   │   │   ├── SessionStore.cs
│   │   │   ├── StateArbiter.cs
│   │   │   ├── SerialController.cs
│   │   │   └── TtlSweepService.cs
│   │   └── wwwroot/                       # dashboard static files
│   │       ├── index.html
│   │       ├── app.js
│   │       └── styles.css
│   ├── AgentTrafficLightServer.Tests/     # xUnit / NUnit tests for the server
│   │   └── AgentTrafficLightServer.Tests.csproj
│   ├── AgentTrafficLight.Contracts/       # shared .NET DTOs / constants
│   │   └── AgentTrafficLight.Contracts.csproj
│   └── scripts/                           # service install / uninstall scripts
│       ├── install-windows-service.ps1
│       ├── uninstall-windows-service.ps1
│       └── install-systemd.sh
├── agents/                          # everything agent-side
│   ├── kimi/
│   │   ├── package.json                   # Node.js project for hooks + skill
│   │   ├── tsconfig.json
│   │   ├── src/
│   │   │   ├── hook-forwarder.mjs         # reads stdin JSON, POSTs to /hook
│   │   │   ├── heartbeat.mjs              # resident heartbeat sender
│   │   │   └── skill/
│   │   │       └── agent-light/
│   │   │           ├── SKILL.md
│   │   │           └── index.mjs
│   │   ├── scripts/
│   │   │   └── install-kimi-hooks.sh      # idempotent hook installer
│   │   └── tests/
│   │       └── hook-forwarder.test.mjs
│   ├── claude/                            # future Claude Code integration
│   └── codex/                             # future Codex CLI integration
└── tests/
    └── integration/                       # end-to-end tests (future)
```

## Directory Responsibilities

| Path | Responsibility |
|------|----------------|
| `server/AgentTrafficLightServer` | The single .NET process that hosts the HTTP API, SSE stream, session store, state arbitration, serial output, and dashboard. |
| `server/AgentTrafficLightServer/Endpoints/` | Minimal-API route handlers grouped by feature (`/hook`, `/heartbeat`, `/api/light`, `/api/status`, `/stream`). |
| `server/AgentTrafficLightServer/Services/` | Core business logic: session tracking, winner selection, serial port I/O, TTL sweeps. |
| `server/AgentTrafficLightServer/Models/` | C# records/classes for sessions, hook payloads, and API responses. |
| `server/AgentTrafficLightServer/wwwroot/` | Static dashboard files served at `/`. |
| `server/AgentTrafficLight.Contracts/` | Shared .NET library for DTOs and constants. Kept inside `server/` because v1.0 only the server consumes it. |
| `server/AgentTrafficLightServer.Tests/` | Server-side unit/integration tests. |
| `server/scripts/` | Server-specific install / uninstall scripts (Windows service, systemd). |
| `agents/kimi/` | Kimi Code integration: hook forwarder, resident heartbeat, skill definition, and installer. |
| `agents/claude/` | Reserved for future Claude Code hooks / installer. |
| `agents/codex/` | Reserved for future Codex CLI hooks / installer. |
| `agents/<agent>/scripts/` | Agent-specific hook / skill installers. |
| `scripts/` | Cross-component orchestration, e.g. a single command that installs both the server and the Kimi hooks. |
| `tests/integration/` | End-to-end tests that exercise the server and a mocked serial port together. |
| `reference/` | Official delivery package and firmware; treated as read-only reference material. |

## Naming Conventions

- **.NET projects**: `PascalCase` matching the namespace, e.g. `AgentTrafficLightServer.csproj`.
- **Agent client projects**: under `agents/<agent>/`, use the agent name in project metadata, e.g. `@agent-traffic-light/kimi`.
- **Tests project names**: mirror the project under test with a `.Tests` suffix.
- **Scripts**: `verb-noun.ext`, e.g. `install-windows-service.ps1`, `install-kimi-hooks.sh`.
- **Docs**: `kebab-case.md`.

## Future Expansion

- Adding a new agent (e.g. `windsurf`) means creating `agents/windsurf/` with its own runtime, hooks, installer, and tests. No server code changes are required unless the new agent introduces a new event type.
- If an agent client is written in .NET and needs shared DTOs, move `server/AgentTrafficLight.Contracts/` to a top-level `shared/` or `contracts/` directory.
- macOS support, BLE transport, or persistent history would each get their own folder under `server/` or a new top-level `platforms/` directory if they outgrow the current layout.
