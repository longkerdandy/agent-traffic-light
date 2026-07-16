# Agent Traffic Light — AI Coding Guide

This project is a multi-agent hardware bridge for the **AgentCore-Light v1** desktop RGB traffic-light device. The current implementation is a .NET control server under `server/`.

## 1. Project Conventions

### 1.1 Directory Layout

- `server/` — Everything server-side: ASP.NET Core host, services, tests, and install scripts.
- `agents/` — Per-agent integrations (currently only `kimi/` is planned; `claude/` and `codex/` are reserved).
- `reference/` — Official delivery package and firmware. **Read-only.** Do not modify.
- `docs/` — Architecture notes and plans.

When adding code, keep server and agent code in their respective top-level directories. Do not introduce a hidden `src/` layer.

### 1.2 .NET Project Structure

The solution lives at `server/agent-traffic-light.sln` and contains:

| Project | Purpose |
|---------|---------|
| `AgentTrafficLightServer` | Main executable. Hosts the HTTP API, session arbitration, serial I/O, and dashboard. |
| `AgentTrafficLight.Contracts` | Shared DTOs and constants. |
| `AgentTrafficLightServer.Tests` | Unit and integration tests. |

Shared MSBuild settings are in `server/Directory.Build.props`. The SDK version is pinned in `server/global.json`.

### 1.3 Code Style

The repository uses `.editorconfig` for formatting and style rules. After generating or modifying C# code, run both commands before committing:

```bash
cd server
dotnet format        # whitespace, indentation, brace placement
dotnet format style  # var usage, using order, expression bodies, etc.
```

Both are required because they operate on different rule sets. The CI pipeline runs both in `--verify-no-changes` mode.

### 1.4 Build and Test

```bash
cd server
dotnet restore
dotnet build --no-restore --configuration Release
dotnet test --no-build --configuration Release
```

Release builds treat warnings as errors (`TreatWarningsAsErrors` is enabled in Release).

## 1.5 Context7 MCP

This project includes a [Context7](https://context7.com) MCP server configuration for fetching up-to-date library documentation.

A local `.kimi-code/mcp.json` is present (the entire `.kimi-code/` directory is gitignored) with the Context7 API key. If you need to recreate it, copy the tracked template:

```bash
cp .kimi-code/mcp.json.example .kimi-code/mcp.json
# Replace YOUR_CONTEXT7_API_KEY_HERE with your Context7 API key
```

Do not commit `.kimi-code/mcp.json` — it contains a secret.

## 2. Hardware Reference

### 2.1 Connection

| Item | Value |
|------|-------|
| MCU | ESP32-C3 |
| Serial baud rate | 115200 |
| USB role | USB-to-serial (CDC) |
| BLE fallback | `AgentCore-Light`, service UUID `12345678-1234-5678-1234-56789abcdef0` |

### 2.2 LED Pins (Firmware Authority)

| Color | Firmware pin |
|-------|--------------|
| Red | GPIO21 |
| Yellow | GPIO10 |
| Green | GPIO20 |

LEDs are **active-low** by default (`LED_ACTIVE_HIGH 0`).

### 2.3 Serial Commands

Send one newline-terminated string per command:

```text
idle
thinking
ai
busy
success
wait_confirm
confirm
waiting
wait
error
off
```

Legacy aliases: `writing` → `ai`, `running` → `busy`, `done` → `success`.

### 2.4 Canonical Server States

The server model uses these canonical states (`TrafficLightState`):

| State | Serial command | Visual effect |
|-------|----------------|---------------|
| `Idle` | `idle` | Green breathing |
| `Thinking` | `thinking` | Fast red-yellow-green chase |
| `Ai` | `ai` | Soft red-yellow-green chase |
| `Busy` | `busy` | Yellow slow blink |
| `WaitConfirm` | `wait_confirm` | Yellow steady |
| `Success` | `success` | Green steady for 5 s, then idle |
| `Error` | `error` | Red fast blink |
| `Off` | `off` | All LEDs off |

The firmware treats `wait_confirm`, `confirm`, `waiting`, and `wait` identically (yellow steady). The server collapses them into `WaitConfirm`.

### 2.5 Firmware Version Limitation

The stock firmware (`esp32_c3_traffic_light.ino`) does not expose a version number or a version-query command. Although the official dashboard hard-codes a display value of `v1.0.3`, there is no runtime mechanism to read the firmware version from the device.

Implications for this project:

- The server cannot detect or validate the firmware version at runtime.
- Any protocol extensions (e.g., a `version` command) require modifying and re-flashing the firmware.
- Until the firmware is extended, treat the hardware as implementing the command set documented in section 2.3.

## 3. Current Implementation Status

The server implements the v0.3 instance API and heartbeat model:

- ASP.NET Core Minimal API hosted on `127.0.0.1:8787`.
- In-memory instance store with TTL-based expiration (`Services/InMemoryInstanceStore.cs`).
- Exclusive light-control arbitration: first claim wins, control released on disconnect or expiry (`Api/InstanceApi.cs`).
- `POST /api/instances/{id}/connect`, `/disconnect`, `/events`, `/heartbeat`, `/control`.
- `GET /api/instances`, `/api/instances/{id}`, `/api/light`.
- TTL sweep background service (`Services/InstanceCleanupHostedService.cs`).
- BLE hardware output via `ITrafficLightController` / `BleTrafficLightController` (Windows-only).

Not yet implemented:

- Client-side agent integrations (Kimi Code shim, heartbeat subprocess).
- Dashboard UI and SSE stream.
- Windows service / systemd packaging.
- Persistent storage.

Planned endpoints for v1.0 (see `docs/plan/v1.0-development-plan.md`):

| Endpoint | Purpose |
|----------|---------|
| `POST /hook` | Receive agent lifecycle events |
| `POST /heartbeat` | Keep a session alive |
| `POST /api/light` | Enable/disable light for a session |
| `GET /api/status` | Current state, winner, sessions |
| `GET /api/sessions` | All active sessions |
| `GET /stream` | SSE stream of state changes |
| `GET /` | Dashboard |

## 4. Development Do's and Don'ts

- **Do** match the existing code style and naming conventions.
- **Do** run `dotnet format` and `dotnet format style` after code generation.
- **Do** add or update tests for new server behavior.
- **Do** keep `reference/` read-only.
- **Do** write all comments and documentation in code and configuration files in English.
- **Don't** introduce Node.js in the server unless explicitly required; the server is .NET.
- **Don't** expose the server beyond `127.0.0.1` in v1.0.

## 5. Reference Files

Authoritative reference material is under `reference/agentcore-light-v1-delivery-20260611/`:

- `esp32_c3_traffic_light/esp32_c3_traffic_light.ino` — Firmware source (pinout and serial protocol).
- `agent-signal-light-web/server.js` — Official Node.js status server (reference only).
- `codex_status_bridge.py` — Official Python serial bridge (reference only).

Full background and agent-integration options are in `docs/plan/v1.0-development-plan.md`.
