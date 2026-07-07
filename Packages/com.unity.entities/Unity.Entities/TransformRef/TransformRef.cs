#if ENABLE_TRANSFORMREF
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Unity.Entities
{
    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer] // Needed for safety handles to be patched if TransformRef is used in a job struct
    public unsafe readonly struct TransformRef : IQueryTypeParameter
    {
        readonly internal TransformUnion* m_TransformUnion;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        readonly internal AtomicSafetyHandle m_Safety0;
        readonly internal AtomicSafetyHandle m_HierarchySafety;
        readonly internal int m_SafetyReadOnlyCount;
        readonly internal int m_SafetyReadWriteCount;
#endif
        readonly internal byte m_IsReadOnly;

        public bool IsValid => m_TransformUnion != null;


#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal TransformRef(TransformUnion* transformUnion, bool isReadOnly,
            AtomicSafetyHandle safety, AtomicSafetyHandle hierarchySafety)
#else
        internal TransformRef(TransformUnion* transformUnion, bool isReadOnly)
#endif
        {
            m_TransformUnion = transformUnion;
            m_IsReadOnly = (byte)(isReadOnly ? 1 : 0);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety0 = safety;
            m_HierarchySafety = hierarchySafety;
            m_SafetyReadOnlyCount = isReadOnly ? 2 : 0;
            m_SafetyReadWriteCount = isReadOnly ? 0 : 2;
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckReadAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety0);
            AtomicSafetyHandle.CheckReadAndThrow(m_HierarchySafety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckWriteAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety0);
            AtomicSafetyHandle.CheckWriteAndThrow(m_HierarchySafety);
#endif
        }

        public float3 LocalPosition
        {
            get
            {
                CheckReadAccess();
                return m_TransformUnion->Position;
            }
            set
            {
                CheckWriteAccess();
                m_TransformUnion->Position = value;
            }
        }

        public quaternion LocalRotation
        {
            get
            {
                CheckReadAccess();
                return m_TransformUnion->Rotation;
            }
            set
            {
                CheckWriteAccess();
                m_TransformUnion->Rotation = value;
            }
        }

        public float3 LocalScale
        {
            get
            {
                CheckReadAccess();
                return m_TransformUnion->Scale;
            }
            set
            {
                CheckWriteAccess();
                m_TransformUnion->Scale = value;
            }
        }

        public float4x4 ComputeLocalToWorld()
        {
            CheckReadAccess();
            // TODO DOTS-10269: CheckHierarchyReadAccess();
            return m_TransformUnion->ComputeLocalToWorld();
        }

        /// <summary>
        /// Sets the local (parent-relative) position, rotation, and scale to the values encoded in a 4x4 local-to-parent matrix.
        /// </summary>
        /// <remarks>
        /// This function gives identical results to the following operations, but is more efficient:
        /// <code>
        /// AffineTransform.decompose(localToParentMatrix, out float3 position, out quaternion rotation, out float3 scale);
        /// transformRef.LocalPosition = position;
        /// transformRef.LocalRotation = rotation;
        /// transformRef.LocalScale = scale;
        /// </code>
        /// </remarks>
        /// <param name="localToParentMatrix">A matrix representing the parent-relative affine transformation.</param>
        public void SetLocalTransform(float4x4 localToParentMatrix)
        {
            CheckWriteAccess();
            m_TransformUnion->SetLocalTransform(localToParentMatrix);
        }

        /// <summary>
        /// Sets the local (parent-relative) position, rotation, and scale to the provided values.
        /// </summary>
        /// <remarks>
        /// This function gives identical results to the following operations, but is more efficient:
        /// <code>
        /// transformRef.LocalPosition = position;
        /// transformRef.LocalRotation = rotation;
        /// transformRef.LocalScale = scale;
        /// </code>
        /// </remarks>
        /// <param name="position">The new parent-relative position</param>
        /// <param name="rotation">The new parent-relative rotation</param>
        /// <param name="scale">The new parent-relative scale</param>
        public void SetLocalTransform(float3 position, quaternion rotation, float3 scale)
        {
            CheckWriteAccess();
            m_TransformUnion->SetLocalTransform(position, rotation, scale);
        }

        /// <summary>
        /// Gets the local (parent-relative) position and rotation.
        /// </summary>
        /// <remarks>
        /// This function gives identical results to the following operations, but is more efficient:
        /// <code>
        /// position = transformRef.LocalPosition;
        /// rotation = transformRef.LocalRotation;
        /// </code>
        /// </remarks>
        /// <param name="position">The parent-relative position will be stored here</param>
        /// <param name="rotation">The parent-relative rotation will be stored here</param>
        public void GetLocalPositionAndRotation(out float3 position, out quaternion rotation)
        {
            CheckReadAccess();
            m_TransformUnion->GetLocalPositionAndRotation(out position, out rotation);
        }

        /// <summary>
        /// Sets the local (parent-relative) position and rotation to the provided values.
        /// </summary>
        /// <remarks>
        /// This function gives identical results to the following operations, but is more efficient:
        /// <code>
        /// transformRef.LocalPosition = position;
        /// transformRef.LocalRotation = rotation;
        /// </code>
        /// </remarks>
        /// <seealso cref="SetLocalTransform(float3, quaternion, float3)"/>
        /// <seealso cref="SetLocalTransform(float4x4)"/>
        /// <param name="position">The new parent-relative position</param>
        /// <param name="rotation">The new parent-relative rotation</param>
        public void SetLocalPositionAndRotation(float3 position, quaternion rotation)
        {
            CheckWriteAccess();
            m_TransformUnion->SetLocalPositionAndRotation(position, rotation);
        }

        /// <summary>
        /// Gets the world-space position and rotation.
        /// </summary>
        /// <remarks>
        /// This operation is significantly more expensive than the local-space equivalent. Its execution
        /// time is proporitional to the depth of this object in its transform hierarchy. Only use it
        /// when the call site specifically needs world-space transform data for objects that may be in
        /// a hierarchy.
        ///
        /// The world-space scale can only be computed approximately in the general case; see <see cref="GetWorldLossyScale"/>.
        /// </remarks>
        /// <seealso cref="GetLocalPositionAndRotation(float3, quaternion)"/>
        /// <param name="position">The world-space position will be stored here</param>
        /// <param name="rotation">The world-space rotation will be stored here</param>
        public void GetWorldPositionAndRotation(out float3 position, out quaternion rotation)
        {
            CheckReadAccess();
            m_TransformUnion->GetWorldPositionAndRotation(out position, out rotation);
        }

        /// <summary>
        /// Sets the world-space position and rotation to the provided values.
        /// </summary>
        /// <remarks>
        /// This operation is significantly more expensive than the local-space equivalent. Its execution
        /// time is proporitional to the depth of this object in its transform hierarchy. Only use it
        /// when the call site specifically needs world-space transform data for objects that may be in
        /// a hierarchy.
        /// </remarks>
        /// <seealso cref="SetLocalPositionAndRotation(float3, quaternion)"/>
        /// <param name="position">The new world-space position</param>
        /// <param name="rotation">The new world-space rotation</param>
        public void SetWorldPositionAndRotation(float3 position, quaternion rotation)
        {
            CheckWriteAccess();
            m_TransformUnion->SetWorldPositionAndRotation(position, rotation);
        }

        internal void SetParent(EntityComponentStore* componentStore, TransformRef newParent, Entity parentEntity, Entity childEntity, bool preserveWorldTransform = true)
        {
            CheckWriteAccess();
            if (parentEntity != Entity.Null)
                newParent.CheckWriteAccess();
            m_TransformUnion->SetParent(componentStore, newParent.m_TransformUnion, parentEntity, childEntity, preserveWorldTransform);
        }

        // Do not use. This is only for testing. Implementation will be replaced with access through TransformUnion.
        // The signature will change, your code will break, and you will be sad.
        /*
        internal Entity GetParent(ref EntityManager em, Entity entity)
        {
            if (!em.HasComponent<Parent>(entity))
                return Entity.Null;
            return em.GetComponentData<Parent>(entity).Value;
        }
        */

        internal void QueueTransformDispatch()
        {
            m_TransformUnion->QueueTransformDispatch();
        }
    }
}
#endif
