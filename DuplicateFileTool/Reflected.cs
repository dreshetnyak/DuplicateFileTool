using System;
using System.Linq;
using System.Reflection;
using DuplicateFileTool.Annotations;

namespace DuplicateFileTool
{
    internal class Reflected
    {
        private object ReflectedObject { get; }
        private Type ReflectedObjectType { get; }
        private BindingFlags BindingFlags { get; }

        public Reflected([NotNull] object obj, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public)
        {
            ReflectedObject = obj;
            ReflectedObjectType = obj.GetType();
            BindingFlags = bindingFlags;
        }

        public bool TryGet(string name, out object value)
        {
            var property = GetPropertyInfo(name);
            if (property == default)
            {
                value = default;
                return false;
            }

            value = property.GetValue(ReflectedObject);
            return true;
        }

        public bool TryGetValue(string name, out object value)
        {
            if (TryGet(name, out var obj) && new Reflected(obj).TryGet("Value", out value))
                return true;
            value = null;
            return false;
        }

        public bool TrySet(string name, object value)
        {
            var property = GetPropertyInfo(name);
            if (property == default)
                return false;

            object convertedValue;
            try { convertedValue = Convert.ChangeType(value, property.PropertyType); }
            catch { return false; }

            try { property.SetValue(ReflectedObject, convertedValue); }
            catch { return false; }

            return true;
        }

        public bool TrySetValue(string name, string stringValue)
        {
            return TryGet(name, out var obj) && 
                   !ReferenceEquals(obj, null) && 
                   new Reflected(obj).TrySet("Value", stringValue);
        }

        private PropertyInfo GetPropertyInfo(string name)
        {
            return ReflectedObjectType
                .GetProperties(BindingFlags)
                .FirstOrDefault(property => property.Name == name);
        }
    }
}
