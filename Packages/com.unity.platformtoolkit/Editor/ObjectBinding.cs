using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.PlatformToolkit.Editor
{
    [UxmlObject]
    internal partial class ObjectBinding : DataBinding
    {
        private readonly PathVisitor m_DefaultValueVisitor = new DefaultValuePathVisitor();

        protected override BindingResult UpdateSource<TValue>(in BindingContext context, ref TValue value)
        {
            if (TypeTraits<TValue>.IsUnityObject && (value as Object) == null)
            {
                return UpdateSourceFromNull(context.dataSource, context.dataSourcePath);
            }
            else
            {
                return base.UpdateSource(in context, ref value);
            }
        }

        private BindingResult UpdateSourceFromNull(object source, in PropertyPath sourcePath)
        {
            if (ProcessNullValue(ref source, in sourcePath, out VisitReturnCode returnCode))
            {
                return default;
            }

            return new BindingResult(
                BindingStatus.Failure,
                $"Failed to set null value for path '{sourcePath.ToString()}' {returnCode}"
            );
        }

        private bool ProcessNullValue<TContainer>(
            ref TContainer container,
            in PropertyPath path,
            out VisitReturnCode returnCode
        )
        {
            if (path.IsEmpty)
            {
                returnCode = VisitReturnCode.InvalidPath;
                return false;
            }

            m_DefaultValueVisitor.Path = path;
            try
            {
                if (!PropertyContainer.TryAccept(m_DefaultValueVisitor, ref container, out returnCode))
                {
                    return false;
                }

                returnCode = m_DefaultValueVisitor.ReturnCode;
            }
            finally
            {
                m_DefaultValueVisitor.Reset();
            }

            return returnCode == VisitReturnCode.Ok;
        }

        private class DefaultValuePathVisitor : PathVisitor
        {
            protected override void VisitPath<TContainer, TValue>(
                Property<TContainer, TValue> property,
                ref TContainer container,
                ref TValue value
            )
            {
                property.SetValue(ref container, default);
            }
        }
    }
}
