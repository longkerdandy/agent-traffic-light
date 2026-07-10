# Project Background & Integration Feasibility

## 1. Product Overview

This project is **agent-traffic-light**, a multi-agent hardware bridge for the **AgentCore-Light v1** desktop traffic-light device.

- **Hardware**: ESP32-C3 powered mini RGB traffic light, connected to the PC via USB.
- **Official supported agents**: Codex (OpenAI) and Claude (Anthropic).
- **Goal**: Make the same hardware reflect activity states from any supported AI coding agent — initially Kimi Code, Claude Code, and Codex CLI (idle, thinking, busy, waiting, success, error).

The hardware is already capable of displaying the required states. The missing piece is a bridge between **agent lifecycle events** and the HTTP/serial control pipeline.

---

## 2. Hardware Reference

### 2.1 MCU & Connection

| Item | Value |
|------|-------|
| MCU | ESP32-C3 |
| Serial baud rate | 115200 |
| USB role | USB-to-serial device (CDC) |
| Wireless fallback | BLE peripheral (`AgentCore-Light`, service UUID `12345678-1234-5678-1234-56789abcdef0`) |

### 2.2 LED Pinout

> **Note**: Documentation and firmware pin definitions differ. The firmware is the authoritative source.

| Color | Official doc pin | Firmware pin (`esp32_c3_traffic_light.ino`) |
|-------|------------------|-----------------------------------------------|
| Red   | GPIO0            | GPIO21                                        |
| Yellow| GPIO2            | GPIO10                                        |
| Green | GPIO1            | GPIO20                                        |

The LEDs are **active-low** by default (`LED_ACTIVE_HIGH 0`). Change the define if your board is active-high.

### 2.3 Serial Command Protocol

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

Legacy aliases are also accepted: `writing` -> `ai`, `running` -> `busy`, `done` -> `success`.

### 2.4 Light Effects

| Command | Visual effect |
|---------|---------------|
| `idle` | Green breathing |
| `thinking` | Red-yellow-green chase (fast) |
| `ai` | Red-yellow-green chase (soft/slow) |
| `busy` | Yellow slow blink |
| `success` | Green steady for 5 s, then back to idle |
| `wait_confirm` / `confirm` / `waiting` / `wait` | Yellow steady |
| `error` | Red fast blink |
| `off` | All LEDs off |

---

## 3. Official Software Architecture (Codex / Claude)

The official delivery package uses a three-tier architecture:

```text
┌─────────────────────────────────────────────┐
│  Codex / Claude (user-facing AI agent)      │
│  Emits lifecycle hooks                      │
└──────────────┬──────────────────────────────┘
               │ JSON payload to POST /hook
               ▼
┌─────────────────────────────────────────────┐
│  agent-signal-light-web (Node.js, :8787)    │
│  - Aggregates sessions & priority           │
│  - Serves dashboard on http://127.0.0.1:8787│
│  - Exposes /api/status and /stream (SSE)    │
└──────────────┬──────────────────────────────┘
               │ GET /api/status every 0.5 s
               ▼
┌─────────────────────────────────────────────┐
│  codex_status_bridge.py (Python + pyserial) │
│  - Auto-detects ESP32 COM port              │
│  - Maps status → serial command             │
│  - Sends command to ESP32 over USB          │
└─────────────────────────────────────────────┘
```

### 3.1 Web Server (`server.js`)

- `POST /hook` receives events such as `SessionStart`, `UserPromptSubmit`, `PreToolUse`, `PostToolUse`, `PermissionRequest`, `Stop`, `StopFailure`.
- `detectAgent()` identifies `codex` or `claude` from payload fields (`agent`, `client`, `source_agent`, etc.) or event names.
- `deriveDeviceStatus()` maps events/effects to a canonical status set: `idle`, `thinking`, `ai`, `busy`, `wait_confirm`, `success`, `error`, `off`.
- `GET /api/status` returns JSON with `device_status`, `display_state`, `winner_event`, `effect_id`, `agent`, sessions, and config.

### 3.2 Serial Bridge (`codex_status_bridge.py`)

- Polls `http://127.0.0.1:8787/api/status`.
- Maps `device_status` / `display_state` / `winner_event` / `effect_id` to one of the serial commands.
- Uses an instance lock on TCP port `37638` to avoid duplicate bridges.
- Auto-detects the highest-scoring serial port containing `esp32`, `usb`, `serial`, or `jtag` (excluding Bluetooth).

### 3.3 Hook Installation

- **Codex**: writes `.codex/hooks.json` in the workspace (and merges into `~/.codex/hooks.json`).
- **Claude**: merges hooks into `~/.claude/settings.json`.
- Hook entry points: `hook.cmd` (Windows) and `hook.sh` (macOS/Linux) call `hook-forwarder.js`, which forwards JSON from stdin to `POST /hook` and starts the web/bridge daemons if they are not running.

### 3.4 Configuration Model (`config.default.json`)

- `effects`: list of frame-based LED patterns.
- `event_bindings`: maps event names to effect IDs.
- `event_priority`: determines which active session wins when multiple agents are running.
- `agent_priority`: per-agent override of the priority list.

Only `codex` and `claude` are recognized agent scopes today (`AGENT_SCOPES = {"all", "claude", "codex"}`). Additional agents such as `kimi` must be added.

---

## 4. Agent Integration: Feasible Approaches

Different agent CLIs expose lifecycle events in different ways. The following sections describe the integration options for each supported agent. Kimi Code CLI is covered first because it ships a native lifecycle-hooks system (Beta), making it the cleanest reference implementation.

Documentation references:

- [Kimi Code CLI — Hooks](https://www.kimi.com/code/docs/en/kimi-code-cli/customization/hooks.html)
- [Kimi Code CLI — Configuration files](https://www.kimi.com/code/docs/en/kimi-code-cli/configuration/config-files.html)

### 4.1 Native Kimi Code Lifecycle Hooks (Recommended)

Hooks are configured in `~/.kimi-code/config.toml` (or `$KIMI_CODE_HOME/config.toml`) under the `[[hooks]]` array. When an event fires, Kimi spawns the configured command and writes a JSON payload to its stdin.

**Supported events useful for the traffic light**

| Event | Purpose |
|-------|---------|
| `SessionStart` | Session created or resumed → `idle` |
| `UserPromptSubmit` | User just sent a message → `thinking` |
| `PreToolUse` | About to call a tool → `busy` |
| `PostToolUse` | Tool succeeded → continue `busy`/`thinking` |
| `PostToolUseFailure` | Tool failed → `error` |
| `PermissionRequest` | Waiting for human approval → `wait_confirm` |
| `Stop` | Turn ended cleanly → `success` |
| `StopFailure` | Turn ended with error → `error` |
| `SubagentStart` / `SubagentStop` | Sub-agent activity → `ai` / `thinking` |
| `PreCompact` / `PostCompact` | Context compaction → `ai` |
| `Interrupt` | User interrupted → `off` or `idle` |
| `SessionEnd` | Session closed → `off` |
| `Notification` | Background notification → `wait_confirm` |

**Hook payload format**

Base fields (all events):

```json
{
  "hook_event_name": "PreToolUse",
  "session_id": "session_abc",
  "cwd": "/path/to/project"
}
```

Event-specific fields are also included (e.g., `tool_name`, `tool_input`, `agent_name`). This is the same JSON schema the existing web server already consumes, so the mapping layer can be reused.

**Example `~/.kimi-code/config.toml` snippet**

```toml
[[hooks]]
event = "SessionStart"
command = "node /path/to/agent-traffic-light/agents/kimi/kimi-hook.mjs"
timeout = 5

[[hooks]]
event = "UserPromptSubmit"
command = "node /path/to/agent-traffic-light/agents/kimi/kimi-hook.mjs"
timeout = 5

[[hooks]]
event = "PreToolUse"
command = "node /path/to/agent-traffic-light/agents/kimi/kimi-hook.mjs"
timeout = 5

[[hooks]]
event = "PostToolUse"
command = "node /path/to/agent-traffic-light/agents/kimi/kimi-hook.mjs"
timeout = 5

[[hooks]]
event = "PermissionRequest"
command = "node /path/to/agent-traffic-light/agents/kimi/kimi-hook.mjs"
timeout = 5

[[hooks]]
event = "Stop"
command = "node /path/to/agent-traffic-light/agents/kimi/kimi-hook.mjs"
timeout = 5

[[hooks]]
event = "StopFailure"
command = "node /path/to/agent-traffic-light/agents/kimi/kimi-hook.mjs"
timeout = 5

[[hooks]]
event = "SessionEnd"
command = "node /path/to/agent-traffic-light/agents/kimi/kimi-hook.mjs"
timeout = 5
```

**Hook script responsibility**

The script should:

1. Read JSON from stdin.
2. Optionally append `agent = "kimi"` (or the web server can detect it from the query string).
3. POST to `http://127.0.0.1:8787/hook?agent=kimi`.
4. Exit with code `0` (do **not** block Kimi; the traffic light is observation-only).

**Pros**: Native, fine-grained, no wrapper binary, works with the Kimi TUI and any editor driving the CLI via ACP.  
**Cons**: Hooks are Beta and config location may evolve; hooks are global (`~/.kimi-code/config.toml`) rather than per-project. Some older docs reference `~/.kimi/config.toml`, but current official docs use `~/.kimi-code/config.toml`.

### 4.2 Wrapper CLI (Fallback)

If native hooks are unavailable or disabled, create a `kimi` wrapper that proxies to the real binary and emits coarse start/end events.

**Pros**: Works without any Kimi config change.  
**Cons**: Only captures explicit invocations, misses internal states (tool use, waiting for approval), and does not integrate with the TUI or ACP-driven sessions.

### 4.3 Shell Lifecycle Hooks (Fallback)

Wrap the `kimi` command in a shell function and emit `SessionStart`/`Stop` events around invocations.

**Pros**: No separate binary.  
**Cons**: Shell-specific, coarse granularity, conflicts with aliases.

### 4.4 Manual / Dashboard Trigger (Demo / Debug)

The existing web dashboard already supports manual events via `POST /event` (G/Y/W/R/O). This remains useful for testing and demos.

---

## 5. Recommended Implementation Path

1. **Rewrite the control server** as a single Node.js process (`agent-traffic-light-server`) that combines the web API, session arbitration, and direct serial control. The official `server.js` and `codex_status_bridge.py` are used only as reference.
2. **Normalize all agent events** to a common schema at `/hook`, identifying the source agent from query parameters or payload fields.
3. **Create per-agent hook forwarders** under `agents/<agent>/`, starting with `agents/kimi/kimi-hook.mjs` for Kimi Code. Each forwarder reads stdin and POSTs to `http://127.0.0.1:8787/hook?agent=<agent>`.
4. **Install Kimi hooks** by appending `[[hooks]]` entries to `~/.kimi-code/config.toml` (and/or `~/.kimi/config.toml` for legacy installs), or by packaging them as a Kimi plugin. Provide an installer script that backs up the existing config and idempotently adds the traffic-light hooks.
5. **Add light ownership controls** so a session only drives the hardware after the user explicitly enables it (e.g., `/agent-light:on`). Use heartbeats to keep sessions alive while their Kimi/Claude/Codex instances are open.
6. **Document event-to-light mappings** and ship cross-platform setup scripts.

This approach keeps the hardware logic agent-agnostic while giving each agent CLI a thin, native integration.

---

## 6. Reference Files

| File | Description |
|------|-------------|
| `reference/agentcore-light-v1-delivery-20260611/AI状态灯v1客户交付包/客户使用说明-一键安装版.md` | Official user manual (Chinese) |
| `reference/agentcore-light-v1-delivery-20260611/AI状态灯v1客户交付包/codex_status_bridge.py` | Python serial bridge |
| `reference/agentcore-light-v1-delivery-20260611/AI状态灯v1客户交付包/agent_light_control.py` | Manual serial test tool |
| `reference/agentcore-light-v1-delivery-20260611/AI状态灯v1客户交付包/agent-signal-light-web/server.js` | Node.js status server |
| `reference/agentcore-light-v1-delivery-20260611/AI状态灯v1客户交付包/agent-signal-light-web/hook-forwarder.js` | Hook daemon launcher |
| `reference/agentcore-light-v1-delivery-20260611/AI状态灯v1客户交付包/agent-signal-light-web/install-hooks.js` | Codex/Claude hook installer |
| `reference/agentcore-light-v1-delivery-20260611/AI状态灯v1客户交付包/agent-signal-light-web/config.default.json` | Effects & event bindings |
| `reference/agentcore-light-v1-delivery-20260611/AI状态灯v1客户交付包/esp32_c3_traffic_light/esp32_c3_traffic_light.ino` | Arduino/ESP32 firmware source |
| `reference/agentcore-light-v1-firmware-20260611/windows/firmware.bin` | Prebuilt Windows firmware |
| `reference/agentcore-light-v1-firmware-20260611/mac/firmware.bin` | Prebuilt macOS firmware |
| [Kimi Code CLI — Hooks](https://www.kimi.com/code/docs/en/kimi-code-cli/customization/hooks.html) | Official Kimi hook documentation |
| [Kimi Code CLI — Configuration files](https://www.kimi.com/code/docs/en/kimi-code-cli/configuration/config-files.html) | Official Kimi config documentation |
