using System;
using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Transforms
{
    /// <summary>
    /// Internal helper methods for managing parent/child relationships during structural changes.
    /// </summary>
    [BurstCompile]
    static unsafe class ParentingHelpers
    {
        internal static void AddEntityToChildListDuringStructuralChange(EntityDataAccess* eda, Entity child, Entity newParent)
        {
            // Add Child buffer to newParent, if it doesn't already exist
            eda->AddComponentDuringStructuralChange(newParent, ComponentType.ReadWrite<Child>());
            // Append child to newParent's Child buffer
            var typeIndex = TypeManager.GetTypeIndex<Child>();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandles = &eda->DependencyManager->Safety;
            var childBuf = eda->GetBuffer<Child>(newParent, safetyHandles->GetSafetyHandle(typeIndex, false), safetyHandles->GetBufferSafetyHandle(typeIndex), false);
#else
            var childBuf = eda->GetBuffer<Child>(newParent, false);
#endif
            childBuf.Add(new Child { Value = child });
        }

        internal static void RemoveEntityFromChildListDuringStructuralChange(EntityDataAccess* eda, Entity child, Entity currentParent)
        {
            var childComponentType = ComponentType.ReadWrite<Child>();
            var typeIndex = TypeManager.GetTypeIndex<Child>();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandles = &eda->DependencyManager->Safety;
            var childBuf = eda->GetBuffer<Child>(currentParent, safetyHandles->GetSafetyHandle(typeIndex, false), safetyHandles->GetBufferSafetyHandle(typeIndex), false);
#else
            var childBuf = eda->GetBuffer<Child>(currentParent, false);
#endif
            // Remove this entity from currentParent's Child buffer
            bool foundChild = false;
            for (int i = 0; i < childBuf.Length; ++i)
            {
                if (Hint.Unlikely(childBuf[i].Value == child))
                {
                    foundChild = true;
                    childBuf.RemoveAtSwapBack(i); // This assumes child buffer order does not need to be preserved
                    break;
                }
            }
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(!foundChild))
                throw new InvalidOperationException(
                    $"entity {child} not found in Child buffer of current parent {currentParent}");
#endif
            // If we removed the last child, remove the now-empty Child buffer.
            if (foundChild && childBuf.Length == 0)
            {
                eda->RemoveComponentDuringStructuralChange(currentParent, childComponentType);
            }
        }

        static bool IsUniformScale(in float3 scale, float tolerance = 0.0001f)
        {
            return math.abs(scale.x - scale.y) <= tolerance &&
                   math.abs(scale.x - scale.z) <= tolerance &&
                   math.abs(scale.y - scale.z) <= tolerance;
        }

        internal static void DetachChildrenDuringStructuralChange(EntityDataAccess* eda, Entity parent, bool preserveWorldTransform)
        {
            eda->EntityComponentStore->AssertEntitiesExist(&parent, 1);
            var childComponentType = ComponentType.ReadWrite<Child>();
            bool hasChildren = eda->HasComponent(parent, childComponentType);
            if (Hint.Unlikely(!hasChildren))
                return; // nothing to do
            // We must create a copy of the child buffer here, as SetParentDuringStructuralChange will modify the
            // original buffer while removing the children.
            var typeIndex = TypeManager.GetTypeIndex<Child>();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandles = &eda->DependencyManager->Safety;
            var children = eda->GetBuffer<Child>(parent, safetyHandles->GetSafetyHandle(typeIndex, true), safetyHandles->GetBufferSafetyHandle(typeIndex), true).ToNativeArray(Allocator.Temp);
#else
            var children = eda->GetBuffer<Child>(parent, true).ToNativeArray(Allocator.Temp);
#endif
            for (int i = 0; i < children.Length; ++i)
            {
                Entity child = children[i].Value;
                // This will do more work than necessary, as we'll individually search for & remove each child from the parent's
                // Child buffer, even though we know we're about to destroy the entire Child buffer anyway. This is O(N^2).
                // If that becomes a bottleneck, we can provide a flag to SetParentDuringStructuralChange to skip the
                // remove-from-parent step, in which case we'll need to manually remove the Child component after this loop.
                SetParentDuringStructuralChange(eda, child, Entity.Null, preserveWorldTransform);
            }
            Assert.IsFalse(eda->HasComponent(parent, childComponentType), "Entity should no longer have Child buffer after removing all children");
        }

#if ENABLE_TRANSFORMREF
        internal static void SetParentTransformRefInternal(EntityDataAccess* eda, Entity childEntity, Entity parentEntity, bool preserveWorldTransform)
        {
            TransformRef childRef = eda->GetTransformRef(childEntity);
            TransformRef parentRef = parentEntity == Entity.Null ? default(TransformRef) : eda->GetTransformRef(parentEntity);
            childRef.SetParent(eda->EntityComponentStore, parentRef, parentEntity, childEntity, preserveWorldTransform);
        }
#endif

        [BurstCompile]
        internal static void SetParentInternal(EntityDataAccess* eda, in Entity child, in Entity newParent, bool preserveWorldTransform)
        {
            eda->EntityComponentStore->AssertEntityExists(child);
            var parentComponentType = ComponentType.ReadWrite<Parent>();
            bool hasCurrentParent = eda->HasComponent(child, parentComponentType);
            Entity currentParent = hasCurrentParent ? eda->GetComponentData<Parent>(child).Value : Entity.Null;
            if (currentParent == newParent)
            {
                // Handle the edge case where a user calls SetParent(child, Entity.Null) on a child with a null Parent component.
                // This is not an error, but we need to take the opportunity to remove the null Parent so it isn't flagged
                // as an error later.
                if (Hint.Unlikely(hasCurrentParent && currentParent == Entity.Null))
                {
                    eda->RemoveComponentDuringStructuralChange(child, parentComponentType);
                }

                return;
            }
            // Transform hierarchies must not contain any cycles. Make sure newParent is not child itself, or one of child's descendants.
            // This is technically a debug-only check, but the consequences of a cycle are almost certainly a cryptic infinite loop / crash,
            // so let's leave it active in all builds.
            if (newParent != Entity.Null)
            {
                Entity e = newParent;
                while (e != Entity.Null)
                {
                    // no error, but operation is no-op
                    if (e == child)
                        return;
                    e = eda->HasComponent(e, parentComponentType) ? eda->GetComponentData<Parent>(e).Value : Entity.Null;
                }
            }
            float4x4 currentLocalToWorld = default;
            ComponentLookup<LocalTransform> localTransformLookup = default;
            ComponentLookup<Parent> parentLookup = default;
            ComponentLookup<PostTransformMatrix> postTransformMatrixLookup = default;
            if (Hint.Likely(preserveWorldTransform))
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                localTransformLookup = new ComponentLookup<LocalTransform>(TypeManager.GetTypeIndex<LocalTransform>(), eda, true);
                parentLookup = new ComponentLookup<Parent>(TypeManager.GetTypeIndex<Parent>(), eda, true);
                postTransformMatrixLookup = new ComponentLookup<PostTransformMatrix>(TypeManager.GetTypeIndex<PostTransformMatrix>(), eda, true);
#else
                localTransformLookup = new ComponentLookup<LocalTransform>(TypeManager.GetTypeIndex<LocalTransform>(), eda);
                parentLookup = new ComponentLookup<Parent>(TypeManager.GetTypeIndex<Parent>(), eda);
                postTransformMatrixLookup = new ComponentLookup<PostTransformMatrix>(TypeManager.GetTypeIndex<PostTransformMatrix>(), eda);
#endif

                if (currentParent == Entity.Null)
                {
                    if (Hint.Likely(localTransformLookup.TryGetComponent(child, out var childLocalTransform)))
                        currentLocalToWorld = childLocalTransform.ToMatrix();
                    else
                        throw new InvalidOperationException(
                            $"Entity {child} does not have the required LocalTransform component");
                }
                else
                {
                    TransformHelpers.ComputeWorldTransformMatrix(child, out currentLocalToWorld,
                        ref localTransformLookup, ref parentLookup, ref postTransformMatrixLookup);
                }
            }

            if (newParent == Entity.Null)
            {
                // Remove child from hierarchy
                RemoveEntityFromChildListDuringStructuralChange(eda, child, currentParent);
                // Remove Parent component from child
                eda->RemoveComponentDuringStructuralChange(child, parentComponentType);
            }
            else if (currentParent == Entity.Null)
            {
                // Add child to hierarchy
                eda->EntityComponentStore->AssertEntityExists(newParent);
                AddEntityToChildListDuringStructuralChange(eda, child, newParent);
                // Add Parent component to child, with value newParent
                eda->AddComponentDuringStructuralChange(child, parentComponentType);
                eda->SetComponentData(child, new Parent { Value = newParent });
            }
            else if (currentParent != Entity.Null && newParent != Entity.Null)
            {
                // Change parent
                eda->EntityComponentStore->AssertEntityExists(currentParent);
                RemoveEntityFromChildListDuringStructuralChange(eda, child, currentParent);
                eda->EntityComponentStore->AssertEntityExists(newParent);
                AddEntityToChildListDuringStructuralChange(eda, child, newParent);
                // Update Parent component value on child
                eda->SetComponentData(child, new Parent { Value = newParent });
            }
            // Compute and set new LocalTransform if preserveWorldTransform is true
            if (Hint.Likely(preserveWorldTransform))
            {
                // Compute new local-to-parent matrix
                float4x4 newLocalToParent = default;
                if (newParent == Entity.Null)
                    newLocalToParent = currentLocalToWorld;
                else
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    localTransformLookup = new ComponentLookup<LocalTransform>(TypeManager.GetTypeIndex<LocalTransform>(), eda, true);
                    parentLookup = new ComponentLookup<Parent>(TypeManager.GetTypeIndex<Parent>(), eda, true);
                    postTransformMatrixLookup = new ComponentLookup<PostTransformMatrix>(TypeManager.GetTypeIndex<PostTransformMatrix>(), eda, true);
#else
                    localTransformLookup = new ComponentLookup<LocalTransform>(TypeManager.GetTypeIndex<LocalTransform>(), eda);
                    parentLookup = new ComponentLookup<Parent>(TypeManager.GetTypeIndex<Parent>(), eda);
                    postTransformMatrixLookup = new ComponentLookup<PostTransformMatrix>(TypeManager.GetTypeIndex<PostTransformMatrix>(), eda);
#endif
                    TransformHelpers.ComputeWorldTransformMatrix(newParent, out float4x4 newParentToWorld,
                        ref localTransformLookup, ref parentLookup, ref postTransformMatrixLookup);
                    newLocalToParent = math.mul(math.inverse(newParentToWorld), currentLocalToWorld);
                }
                // Convert to LocalTransform (and optional PostTransformMatrix)
                float3 newT = newLocalToParent.Translation();
                quaternion newR = newLocalToParent.Rotation();
                float3 newS = newLocalToParent.Scale();
                var postTransformMatrixType = ComponentType.ReadWrite<PostTransformMatrix>();
                if (IsUniformScale(newS))
                {
                    eda->SetComponentData(child, LocalTransform.FromPositionRotationScale(newT, newR, newS.x));
                    if (eda->HasComponent(child, postTransformMatrixType))
                        eda->RemoveComponentDuringStructuralChange(child, postTransformMatrixType);
                }
                else
                {
                    eda->SetComponentData(child, LocalTransform.FromPositionRotation(newT, newR));
                    eda->AddComponentDuringStructuralChange(child, postTransformMatrixType);
                    eda->SetComponentData(child, new PostTransformMatrix { Value = float4x4.Scale(newS) });
                }
            }
        }

        internal static void SetParentDuringStructuralChange(EntityDataAccess* eda, Entity child, Entity newParent, bool preserveWorldTransform)
        {
#if ENABLE_TRANSFORMREF
            var transformRefType = ComponentType.ReadWrite<TransformRef>();
            if (eda->HasComponent(child, transformRefType) &&
                (newParent == Entity.Null ||
                 eda->HasComponent(newParent, transformRefType)))
            {
                SetParentTransformRefInternal(eda, child, newParent, preserveWorldTransform);
                // Allow SetParentInternal to update Parent/Child components, but do not modify localTransform
                preserveWorldTransform = false;
            }
#endif
            SetParentInternal(eda, child, newParent, preserveWorldTransform);
        }
    }
}

namespace Unity.Entities
{
    /// <summary>
    /// Extension methods for EntityManager to manage parent/child transform hierarchies.
    /// These are currently extension methods on EntityManager as Unity.Entities cannot have a dependency on Unity.Transforms.
    /// </summary>
    public static unsafe class EntityManagerParentingExtensions
    {
        /// <summary>
        /// Establish a new link between a child and parent entity.
        /// </summary>
        /// <remarks>
        /// This operation completes synchronously, leaving all effected transform hierarchies in a consistent state. It
        /// guarantees the following postconditions:
        /// - If <paramref name="newParent"/> exists, <paramref name="child"/> will have the <see cref="Unity.Transforms.Parent"/> and <see cref="Unity.Transforms.PreviousParent"/>
        ///   components with the value set to <paramref name="newParent"/>, and <paramref name="newParent"/> will have a
        ///   <see cref="Unity.Transforms.Child"/> buffer component containing <paramref name="child"/>.
        /// - If <paramref name="newParent"/> does not exist, <paramref name="child"/> will not have the <see cref="Unity.Transforms.Parent"/>
        ///   or <see cref="Unity.Transforms.PreviousParent"/>component.
        /// - If <paramref name="child"/> already had the <see cref="Unity.Transforms.Parent"/> component with a value other than <paramref name="newParent"/>,
        ///   the previous parent will no longer contain a reference to <paramref name="child"/> in its <see cref="Unity.Transforms.Child"/> buffer.
        ///   If this would have left the previous parent's <see cref="Unity.Transforms.Child"/> buffer empty, its buffer component will be removed.
        ///
        /// This function must not be used from an ExclusiveEntityTransaction context. Manipulating transform hierarchies
        /// from worker threads is not safe.
        /// </remarks>
        /// <param name="em">The EntityManager.</param>
        /// <param name="child">The entity whose parent should be changed.</param>
        /// <param name="newParent">The new parent entity for <paramref name="child"/>. If this value is <see cref="Entity.Null"/>,
        /// <paramref name="child"/> will have no parent and will become the root of a new hierarchy.</param>
        /// <param name="preserveWorldTransform">
        /// If true, the world-space transform of <paramref name="child"/> will be preserved as closely as possible by
        /// setting its <see cref="Unity.Transforms.LocalTransform"/> component to match its current world-space transform. Slight
        /// differences are still possible due to floating-point rounding.
        ///
        /// If false, <paramref name="child"/>'s <see cref="Unity.Transforms.LocalTransform"/> components will not be modified. However, the existing
        /// value will now be relative to world-space instead to its previous parent; this may cause a significant
        /// instantaneous change in its world-space transform.
        /// </param>
        /// <exception cref="System.ArgumentException">Thrown if <paramref name="child"/> does not exist.</exception>
        /// <exception cref="System.ArgumentException">Thrown if <paramref name="newParent"/> is not <see cref="Entity.Null"/> and does not exist.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown if <paramref name="preserveWorldTransform"/> is true,
        /// but <paramref name="child"/> or <paramref name="newParent"/> (or their ancestors) do not have the required
        /// <see cref="Unity.Transforms.LocalTransform"/> component.</exception>
        public static void SetParent(this EntityManager em, Entity child, Entity newParent, bool preserveWorldTransform = true)
        {
            var access = em.GetCheckedEntityDataAccess();
            access->AssertMainThread();
            var changes = access->BeginStructuralChanges();
            Unity.Transforms.ParentingHelpers.SetParentDuringStructuralChange(access, child, newParent, preserveWorldTransform);
            access->EndStructuralChanges(ref changes);
        }

#if ENABLE_TRANSFORMREF
        // TODO DOTS-10284 Keeping this around for speed-of-light testing without all the migration overhead (LocalTransform, Parent, Child updates)
        internal static void SetParentTransformRef(this EntityManager em, Entity childEntity, Entity parentEntity, bool preserveWorldTransform = true)
        {
            var access = em.GetCheckedEntityDataAccess();
            access->AssertMainThread();
            Unity.Transforms.ParentingHelpers.SetParentTransformRefInternal(access, childEntity, parentEntity, preserveWorldTransform);
        }
#endif

        /// <summary>
        /// Break the parent/child links between the target entity and all of its children. Each child entity becomes
        /// the root of a new hierarchy.
        /// </summary>
        /// <remarks>
        /// This is effectively the same as calling SetParent(child, Entity.Null, preserveWorldTransform) on all
        /// of the target entity's children, but is potentially more efficient.
        ///
        /// This function must not be used from an ExclusiveEntityTransaction context. Manipulating transform hierarchies
        /// from worker threads is not safe.
        /// </remarks>
        /// <param name="em">The EntityManager.</param>
        /// <param name="parent">The entity whose children should be detached.</param>
        /// <param name="preserveWorldTransform">
        /// If true, the world-space transform of all child entities will be
        /// preserved as closely as possible by setting each entity's <see cref="Unity.Transforms.LocalTransform"/> component to match its
        /// current world-space transform. Slight differences are still possible due to floating-point rounding.
        ///
        /// If false, the childrens' <see cref="Unity.Transforms.LocalTransform"/> components will not be modified. However, the existing
        /// values will now be relative to world-space instead of <paramref name="parent"/>; this may cause a significant instantaneous
        /// change in their world-space transforms.
        /// </param>
        /// <exception cref="ArgumentException">Thrown if the target <paramref name="parent"/> entity does not exist.</exception>
        /// <exception cref="InvalidOperationException">Thrown if <paramref name="preserveWorldTransform"/> is true,
        /// but <paramref name="parent"/>, its children, or its ancestors do not have the required
        /// <see cref="Unity.Transforms.LocalTransform"/> component.</exception>
        public static void DetachChildren(this EntityManager em, Entity parent, bool preserveWorldTransform = true)
        {
            var access = em.GetCheckedEntityDataAccess();
            access->AssertMainThread();
            var changes = access->BeginStructuralChanges();
            Unity.Transforms.ParentingHelpers.DetachChildrenDuringStructuralChange(access, parent, preserveWorldTransform);
            access->EndStructuralChanges(ref changes);
        }
    }
}
