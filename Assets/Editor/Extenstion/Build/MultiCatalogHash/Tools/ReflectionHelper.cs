using System;
using System.Reflection;

namespace Editor.Extenstion.Build.MultiCatalogHash.Tools
{
    public static class ReflectionHelper
    {
        public static object GetFieldValue(object obj, string fieldName)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            var type = obj.GetType();
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null) throw new InvalidOperationException($"Field {fieldName} not found in {type}.");
            return field.GetValue(obj);
        }

        public static void SetFieldValue(object obj, string fieldName, object value)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            var type = obj.GetType();
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null) throw new InvalidOperationException($"Field {fieldName} not found in {type}.");
            field.SetValue(obj, value);
        }
    }
}