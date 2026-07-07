---
uid: test-ui-in-runtime
---
# Test UI in runtime

Use [`RuntimeUITestFixture`](xref:UnityEngine.UIElements.TestFramework.RuntimeUITestFixture) to create tests that run in Play mode.
This fixture allows you to load and test an existing `Scene` containing your UI.

The following examples show how to set up your test class to use `RuntimeUITestFixture` in a Play mode test assembly.

## Test UI made with UIDocument

After loading the `Scene`, fetch the [`UIDocument`](xref:UnityEngine.UIElements.UIDocument) object and call [`SetUIContent`](xref:UnityEngine.UIElements.TestFramework.RuntimeUITestFixture#UnityEngine_UIElements_TestFramework_RuntimeUITestFixture_SetUIContent_UnityEngine_UIElements_UIDocument_) to hook up your UI for simulation.

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Runtime/RuntimeUITestFixtureExample.cs#BasicUIDocumentRuntimeExampleClass)]

## Test UI made with PanelRenderer

After loading the `Scene`, fetch the [`PanelRenderer`](xref:UnityEngine.UIElements.PanelRenderer) object and call [`SetPanelRenderer`](xref:UnityEngine.UIElements.TestFramework.RuntimeUITestFixture#UnityEngine_UIElements_TestFramework_RuntimeUITestFixture_SetPanelRenderer_UnityEngine_UIElements_PanelRenderer_) to hook up your UI for simulation.

[!code-cs[](../../Samples~/DocCodeSamples.Tests/Runtime/RuntimeUITestFixtureExample.cs#BasicPanelRendererRuntimeExampleClass)]

## Additional resources

- [Test in both Editor and runtime states](xref:test-in-both-editor-and-runtime-states)