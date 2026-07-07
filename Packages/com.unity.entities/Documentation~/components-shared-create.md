# Create a shared component

You can create both [managed](components-managed.md) and [unmanaged](components-unmanaged.md) shared components.

Before you create a shared component, make sure you understand how they work and their performance implications. Refer to [Introducing shared components](components-shared-introducing.md) for an overview and when to use them, and [Optimize shared components](components-shared-optimize.md) for performance considerations.

## Create an unmanaged shared component

To create an unmanaged shared component, create a struct that implements the marker interface `ISharedComponentData`.

The following code sample shows an unmanaged shared component:

[!code-cs[Create an unmanaged shared component](../DocCodeSamples.Tests/CreateComponentExamples.cs#shared-unmanaged)]

To override the way that a shared component is checked for equality, you can implement the `IEquatable<>` interface, and ensure `public override int GetHashCode()` is implemented. Entities then internally uses these methods to compare shared components for equality, and therefore partitions entities differently that way. You can also put `[BurstCompile]` on these methods, and they will be compiled with Burst if they comply with Burst's restrictions.

## Create a managed shared component

If you create a shared component struct that has any managed fields (such as class types like strings), that component will be treated as a managed shared component. In that case, you also must implement `IEquatable<>`, and ensure `public override int GetHashCode()` is implemented. The equality methods are necessary to ensure comparisons don't generate unnecessary managed allocations due to implicit boxing when using the default `Equals` and `GetHashCode` implementations.

In contrast to IComponentData components, all shared components must be `struct`s, irrespective of whether they are managed or unmanaged. 

The following code sample shows a managed shared component:

[!code-cs[Create a managed shared component](../DocCodeSamples.Tests/CreateComponentExamples.cs#shared-managed)]
