# Material overrides using C#

Entities Graphics supports per-entity overrides of various HDRP and URP material properties as well as overrides for custom Shader Graphs. You can write C#/Burst code to setup and animate material override values at runtime.

## Built-in Material overrides

Entities Graphics contains a built-in library of IComponentData components that you can add to your entities to override their material properties.

To override color properties, make sure the values are in linear color space. Entities Graphics can't automatically convert color values from gamma space to linear space.

### Supported HDRP Material overrides

- AlphaCutoff
- AORemapMax
- AORemapMin
- BaseColor
- DetailAlbedoScale
- DetailNormalScale
- DetailSmoothnessScale
- DiffusionProfileHash
- EmissiveColor
- Metallic
- Smoothness
- SmoothnessRemapMax
- SmoothnessRemapMin
- SpecularColor
- Thickness
- ThicknessRemap
- UnlitColor (HDRP/Unlit)

You can also use the same property names with an `HDRPMaterialProperty` prefix, for example `HDRPMaterialPropertyBaseColor`.

### Supported URP Material overrides

- BaseColor
- BumpScale
- Cutoff
- EmissionColor
- Metallic
- OcclusionStrength
- Smoothness
- SpecColor

You can also use the same property names with a `URPMaterialProperty` prefix, for example `URPMaterialPropertyBaseColor`.

If you want to override a built-in HDRP or URP property not listed here, you can do that with custom Shader Graph Material overrides.

## Custom Shader Graph Material overrides

You can create your own custom Shader Graph properties, and expose them to ECS as IComponentData. This allows you to write C#/Burst code to setup and animate your own shader inputs. To do this, see the following steps:

### Shader Graph Asset

1. Select your Shader Graph custom property and view it in the **Graph Inspector**.
2. Open the **Node Settings** tab.
3. Enable **Override Property Declaration** then set **Shader Declaration** to **Hybrid Per Instance**.

### IComponentData

For the IComponentData struct, use the `MaterialProperty` Attribute, passing in the **Reference** and type for the Shader Graph property. For example, the IComponentData for the color (float4) property in the above step would be:

```
[MaterialProperty("_Color")]
public struct MyOwnColor : IComponentData
{
   public float4 Value;
}
```

Ensure that the **Reference** name in Shader Graph and the string name in MaterialProperty attribute match exactly.

### Burst C# system

Now you can write a Burst C# system to animate your Material property. The following example uses a custom `MyAnimationTime` component to create a different color each frame.

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
partial struct AnimateMyOwnColorSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (color, t) in SystemAPI.Query<RefRW<MyOwnColor>,
                                                   RefRO<MyAnimationTime>>())
        {
            float timeAnim = t.ValueRO.Value;
            color.ValueRW.Value = new float4(
                math.cos(timeAnim + 1.0f),
                math.cos(timeAnim + 2.0f),
                math.cos(timeAnim + 3.0f),
                1.0f);
        }
    }
}
```

**Important:** You need to create a matching IComponentData struct for every custom Shader Graph property that has **Hybrid Per Instance** enabled. For information on how to do this, see [IComponentData](#icomponentdata). If you don't do this for a custom property, Entities Graphics zero fills the property.
