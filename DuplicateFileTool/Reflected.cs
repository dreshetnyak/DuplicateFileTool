using System.Reflection;

namespace DuplicateFileTool;

internal sealed class Reflected(object obj, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public)
{
    private object ReflectedObject { get; } = obj;
    private Type ReflectedObjectType { get; } = obj.GetType();
    private BindingFlags BindingFlags { get; } = bindingFlags;

    public bool TryGet(string name, out object? value)
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

    public bool TryGetValue(string name, out object? value) => 
        TryGet(name, out value) && 
        value is not null && 
        new Reflected(value).TryGet("Value", out value);

    public bool TrySet(string name, string value)
    {
        var property = GetPropertyInfo(name);
        if (property == default)
            return false;

        object convertedValue;
        try { convertedValue = FromString(value, property.PropertyType); }
        catch { return false; }

        try { property.SetValue(ReflectedObject, convertedValue); }
        catch { return false; }

        return true;
    }

    public bool TrySetValue(string name, string stringValue) =>
        TryGet(name, out var value) &&
        value is not null && 
        new Reflected(value).TrySet("Value", stringValue);

    private PropertyInfo? GetPropertyInfo(string name)
    {
        foreach (var property in ReflectedObjectType.GetProperties(BindingFlags))
        {
            if (property.Name == name) 
                return property;
        }

        return null;
    }

    private static object FromString(string str, Type type)
    {
        if (type.IsEnum)
            return Enum.Parse(type, str);
        return type != typeof(Guid)
            ? Convert.ChangeType(str, type)
            : Guid.Parse(str);
    }
}