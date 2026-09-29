#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Harbourview.EditorTools
{
    /// <summary>
    /// Small wrapper so scene builders can write serialized fields without touching
    /// internals when those fields are private [SerializeField] members.
    /// </summary>
    public static class SerializedObjectUtility
    {
        public static void SetString(Object target, string field, string value)
        {
            SerializedObject so = new SerializedObject(target);
            Apply(so, so.FindProperty(field), p => p.stringValue = value);
        }

        public static void SetFloat(Object target, string field, float value)
        {
            SerializedObject so = new SerializedObject(target);
            Apply(so, so.FindProperty(field), p => p.floatValue = value);
        }

        public static void SetBool(Object target, string field, bool value)
        {
            SerializedObject so = new SerializedObject(target);
            Apply(so, so.FindProperty(field), p => p.boolValue = value);
        }

        public static void SetInt(Object target, string field, int value)
        {
            SerializedObject so = new SerializedObject(target);
            Apply(so, so.FindProperty(field), p => p.intValue = value);
        }

        public static void SetObject(Object target, Object value, string field)
        {
            SerializedObject so = new SerializedObject(target);
            Apply(so, so.FindProperty(field), p => p.objectReferenceValue = value);
        }

        private static void Apply(SerializedObject so, SerializedProperty property, System.Action<SerializedProperty> setter)
        {
            if (property == null)
            {
                Debug.LogWarning("[SerializedObjectUtility] Field not found: " + so.targetObject.name);
                return;
            }

            setter(property);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
