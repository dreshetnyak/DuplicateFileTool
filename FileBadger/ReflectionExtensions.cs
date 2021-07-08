using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DuplicateFileTool
{
    internal static class ReflectionExtensions
    {
        public static bool HasPropertyWhereAttribute(this Type objectType, string propertyName, Type attributeType, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public)
        {
            return objectType
                .GetProperties(bindingFlags)
                .Where(property => property.Name == propertyName)
                .Select(property => property.GetCustomAttributes(true))
                .Any(attributes => attributes.Any(attribute => attribute.GetType() == attributeType));
        }

        public static IEnumerable<string> GetPropertyNamesWhereAttribute(this Type objectType, Type attributeType, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public)
        {
            return objectType
                .GetProperties(bindingFlags)
                .Where(attributes => attributes.GetCustomAttributes(true).Any(attribute => attribute.GetType() == attributeType))
                .Select(property => property.Name);
        }

        public static object GetPropertyAttribute(this Type objectType, string propertyName, Type attributeType, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public)
        {
            return objectType
                .GetProperties(bindingFlags)
                .FirstOrDefault(property => property.Name == propertyName && property.GetCustomAttributes(true).Any(attribute => attribute.GetType() == attributeType));
        }

        public static T GetAttribute<T>(this Type objectType) where T : class
        {
            return objectType.GetCustomAttributes(true).FirstOrDefault(attribute => attribute is T) as T;
        }

        public static bool DerivedFrom(this Type checkType, Type searchType)
        {
            return checkType == searchType || checkType.BaseType != null && checkType.BaseType.DerivedFrom(searchType);
        }
    }
}
