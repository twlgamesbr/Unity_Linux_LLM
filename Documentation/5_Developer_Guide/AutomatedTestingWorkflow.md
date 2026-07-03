# Automated Testing Workflow

This project uses a layered test gate so multiplayer/auth/dialogue regressions are caught before manual Unity testing or server rebuilds.

## Layers

### 1. Static conflict gate

Fast shell checks for architectural conflicts that previously caused debugging loops:

```bash
Tests/Integration/run-network-conflict-scan.sh
```

Catches:

- Direct `StartHost`, `StartClient`, or `StartServer` calls outside `NPCNetworkBootstrap`.
- `AuthNetworkBridge.autoDetectStartupMode` enabled in the main scene.
- `AuthNetworkBridge.startAsHost` enabled in the main scene.
- `NPCSceneInitialization.startNetworkingAfterInitialization` enabled in the main scene.
- Non-idempotent auth SQL role/trigger setup.

### 2. Unity EditMode tests

Run from CLI only when no Unity Editor instance has this project open:

```bash
Tests/Unity/run-editmode-tests.sh
```

If the project is already open, Unity returns:

```text
Multiple Unity instances cannot open the same project.
```

In that case, run the tests from Unity Editor's Test Runner UI instead.

Current high-value Editor tests include:

- `AuthNetworkBridgeTests`
- `NPCMainSceneWiringTests`
- `NPCStartupAuthorityStaticTests`
- `NPCSceneInitializationTests`
- existing networking/dialogue/logger/prefab tests

### 3. Unity PlayMode tests

Run when no Editor instance has this project open:

```bash
Tests/Unity/run-playmode-tests.sh
```

Use this for lifecycle/spawn tests after the EditMode and static gates are stable.

### 4. External service smoke tests

These require Docker and live backend services:

```bash
Tests/Integration/run-auth-db-init-idempotency.sh
Tests/Integration/run-auth-http-smoke.sh
Tests/Integration/run-backend-health-smoke.sh
Tests/Integration/run-dedicated-server-smoke.sh
```

The default local quality gate does not run them unless requested because they depend on current machine services.

### 5. Local quality gate

Safe default gate:

```bash
Tests/run-quality-gate.sh
```

Full external gate:

```bash
RUN_EXTERNAL_SMOKE=1 Tests/run-quality-gate.sh
```

Include Unity batchmode tests too, only when the project is not open in the Editor:

```bash
RUN_UNITY_TESTS=1 Tests/run-quality-gate.sh
```

Full preflight before manual two-client testing:

```bash
RUN_UNITY_TESTS=1 Tests/Integration/run-multiplayer-preflight.sh
```

If Unity is already open, leave `RUN_UNITY_TESTS` unset and run Editor tests manually from the Test Runner UI.

## Before rebuilding the dedicated server

Do not rebuild blindly. First run:

```bash
Tests/run-quality-gate.sh
```

If changing Docker/auth/backend/networking, run:

```bash
RUN_EXTERNAL_SMOKE=1 Tests/run-quality-gate.sh
```

If changing C# behavior and Unity is closed, run:

```bash
RUN_UNITY_TESTS=1 Tests/run-quality-gate.sh
```

## Current dedicated-server invariants

For Docker dedicated-server testing:

- `AuthNetworkBridge.autoDetectStartupMode = false`
- `AuthNetworkBridge.startAsHost = false`
- `AuthNetworkBridge.hostPort = 0`
- `NPCSceneInitialization.configureNetworkTransport = false`
- `NPCSceneInitialization.startNetworkingAfterInitialization = false`
- `NPCNetworkBootstrap` is the only direct owner of `StartHost`, `StartClient`, and `StartServer`.

These are now enforced by static/Editor tests.
