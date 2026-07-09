using System.Collections.Generic;
using UnityEditor;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// A helper class for getting/setting complex properties on serialized objects
    /// </summary>
    class PropertyUtils
    {
        /// <summary>
        /// Get the value of a serialized property as a string hash set
        /// </summary>
        /// <param name="serializedProperty">The serialized property</param>
        /// <returns>A string hash set with the value of the property</returns>
        public static HashSet<string> GetHashSetPropertyValue(SerializedProperty serializedProperty)
        {
            var hashSet = new HashSet<string>();

            var enumerator = serializedProperty.GetEnumerator();
            while (enumerator.MoveNext())
            {
                serializedProperty = enumerator.Current as SerializedProperty;
                hashSet.Add(serializedProperty.stringValue);
            }

            return hashSet;
        }

        /// <summary>
        /// Set the value of a serialized property using a string hash set
        /// </summary>
        /// <param name="serializedProperty">The serialized property to update.</param>
        /// <param name="hashSet">The new value as a string hash set</param>
        public static void SetHashSetPropertyValue(
            SerializedProperty serializedProperty,
            HashSet<string> hashSet
        )
        {
            serializedProperty.ClearArray();

            int i = 0;
            foreach (var value in hashSet)
            {
                serializedProperty.InsertArrayElementAtIndex(i);
                serializedProperty.GetArrayElementAtIndex(i).stringValue = value;
                i++;
            }
        }
    }
}
