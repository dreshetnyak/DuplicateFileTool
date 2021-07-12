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

        public static bool ImplementsInterface(this Type type, Type implementsInterfaceType)
        {
            return type.GetTypeInfo().ImplementedInterfaces.Any(implementedInterface => implementedInterface == implementsInterfaceType);
        }

        public static bool ImplementsInterfaceGeneric(this Type type, Type implementsInterfaceType)
        {
            return type.GetTypeInfo().ImplementedInterfaces.Any(implementedInterface =>
                implementedInterface.IsGenericType 
                    ? implementedInterface.GetGenericTypeDefinition() == implementsInterfaceType.GetGenericTypeDefinition()
                    : implementedInterface == implementsInterfaceType);
        }
        
        public static IEnumerable<PropertyInfo> GetPropertiesThatImplementGeneric(this Type type, Type implementsInterfaceType)
        {
            return type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(property =>
                property.PropertyType.ImplementsInterfaceGeneric(implementsInterfaceType));
        }

        //public static T GetAttribute<T>(this Type objectType) where T : class
        //{
        //    return objectType.GetCustomAttributes(true).FirstOrDefault(attribute => attribute is T) as T;
        //}

        //public static bool DerivedFrom(this Type checkType, Type searchType)
        //{
        //    return checkType == searchType || checkType.BaseType != null && checkType.BaseType.DerivedFrom(searchType);
        //}
    }
}
