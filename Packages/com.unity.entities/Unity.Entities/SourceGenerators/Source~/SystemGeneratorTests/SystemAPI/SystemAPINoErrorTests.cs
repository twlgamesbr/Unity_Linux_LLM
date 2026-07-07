using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS =
    Unity.Entities.SourceGenerators.Test.CSharpSourceGeneratorVerifier<
        Unity.Entities.SourceGen.SystemGenerator.SystemGenerator>;

namespace Unity.Entities.SourceGenerators;

[TestClass]
public class SystemAPINoErrorTests
{
    [TestMethod]
    public async Task TryGetComponent()
    {
        const string testSource = @"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;

public unsafe partial struct NestedGetSingletonEntity : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var entityQuery = new EntityQuery();
        var foo = SystemAPI.TryGetComponent<EcsTestData>(entityQuery.GetSingletonEntity(), out var result);
    }
}";
        await VerifyCS.VerifySourceGeneratorAsync(testSource);
    }    
    
    [TestMethod]
    public async Task TryGetComponentManaged()
    {
        const string testSource = @"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;

public unsafe partial struct NestedGetSingletonEntity : ISystem
{
    class MyTestClass {}

    public void OnUpdate(ref SystemState state)
    {
        var entityQuery = new EntityQuery();
        var foo = SystemAPI.ManagedAPI.TryGetComponent<MyTestClass>(entityQuery.GetSingletonEntity(), out var result);
    }
}";

        await VerifyCS.VerifySourceGeneratorAsync(testSource);
    }  
    
    [TestMethod]
    public async Task TryGetComponentGenericTypeInferred()
    {
        const string testSource = @"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;

public unsafe partial struct NestedGetSingletonEntity : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var entityQuery = new EntityQuery();
        var foo = SystemAPI.TryGetComponent(entityQuery.GetSingletonEntity(), out EcsTestData result);
    }
}";
        await VerifyCS.VerifySourceGeneratorAsync(testSource);
    }    
    
    [TestMethod]
    public async Task TryGetComponentManagedGenericTypeInferred()
    {
        const string testSource = @"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;

public unsafe partial struct NestedGetSingletonEntity : ISystem
{
    class MyTestClass {}

    public void OnUpdate(ref SystemState state)
    {
        var entityQuery = new EntityQuery();
        var foo = SystemAPI.ManagedAPI.TryGetComponent(entityQuery.GetSingletonEntity(), out MyTestClass result);
    }
}";

        await VerifyCS.VerifySourceGeneratorAsync(testSource);
    }     
}