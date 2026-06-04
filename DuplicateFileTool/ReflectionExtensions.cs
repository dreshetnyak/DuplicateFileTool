using System.Reflection;


namespace DuplicateFileTool;

internal static class ReflectionExtensions
{
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
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.PropertyType.ImplementsInterfaceGeneric(implementsInterfaceType));
    }

    public static IEnumerable<object> GetGenericPropertiesObjects(this object obj, Type implementsInterfaceType)
    {
        var objType = obj.GetType();
        var properties = objType.GetPropertiesThatImplementGeneric(implementsInterfaceType);
        foreach (var property in properties)
        {
            var propertyValue = property.GetValue(obj);
            if (propertyValue != null)
                yield return propertyValue;
        }
    }
}