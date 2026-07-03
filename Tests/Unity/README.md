# Unity Test Runner Wrappers

These wrappers standardize local/CI Unity Test Framework execution.

```bash
Tests/Unity/run-editmode-tests.sh
Tests/Unity/run-playmode-tests.sh
```

Override Unity path if needed:

```bash
UNITY_BIN=/path/to/Unity Tests/Unity/run-editmode-tests.sh
```

If batchmode fails with a Unity license/headless entitlement error, run the same suite from the Unity Editor Test Runner UI. The shell wrappers are still useful on CI-capable machines and for stable result/log paths under `TestResults/`.
