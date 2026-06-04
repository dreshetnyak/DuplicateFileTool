using System.Reflection;

namespace DuplicateFileTool.Configuration;

internal interface IChangeable
{
    bool HasChanged { get; }
}

internal static class ConfigManager
{
    public static void LoadFromAppConfig(this object configObject)
    {
        if (ReferenceEquals(configObject, null))
            return;
            
        var configType = configObject.GetType();
        var reflectedObject = new Reflected(configObject);
        foreach (var property in configType.GetPropertiesThatImplementGeneric(typeof(IConfigurationProperty<>)))
        {
            var propertyName = property.Name;
            var path = $"{configType.Name}.{propertyName}";
            if (FileAppConfig.TryGetString(path, out var stringValue))
                reflectedObject.TrySetValue(propertyName, stringValue);
        }
    }

    public static void SaveToAppConfig(this object? configObject)
    {
        if (configObject is not IChangeable { HasChanged: true })
            return;

        var configType = configObject.GetType();
        var reflectedObject = new Reflected(configObject);

        foreach (var property in configType.GetPropertiesThatImplementGeneric(typeof(IConfigurationProperty<>)))
        {
            var path = $"{configType.Name}.{property.Name}";
            if (!reflectedObject.TryGetValue(property.Name, out var valueObject) || valueObject == null)
                throw new InvalidOperationException($"Unable to get '{path}' configuration parameter value");
            FileAppConfig.Set(path, valueObject);
        }

        FileAppConfig.Save();
    }

    public static string GetAppName()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var appName = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "";
        var appVersion = assembly.GetName().Version;

        return appVersion != null 
            ? $"{appName} {appVersion.Major}.{appVersion.Minor}.{appVersion.Build}"
            : appName;
    }
}