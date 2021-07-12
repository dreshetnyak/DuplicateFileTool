using System;
using System.Reflection;

namespace DuplicateFileTool.Configuration
{
    internal static class ConfigurationManager
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
                if (AppConfig.TryGetString(path, out var stringValue))
                    reflectedObject.TrySetValue(propertyName, stringValue);
            }
        }

        public static void SaveToAppConfig(this object configObject)
        {
            if (configObject is null or not IChangeable or IChangeable {HasChanged: false})
                return;

            var configType = configObject.GetType();
            var reflectedObject = new Reflected(configObject);

            foreach (var property in configType.GetPropertiesThatImplementGeneric(typeof(IConfigurationProperty<>)))
            {
                var path = $"{configType.Name}.{property.Name}";
                if (!reflectedObject.TryGetValue(property.Name, out var valueObject))
                    throw new ApplicationException($"Unable to get '{path}' configuration parameter value");
                AppConfig.Set(path, valueObject);
            }

            AppConfig.Save();
        }

        public static string GetAppName()
        {
            var appName = Assembly.GetExecutingAssembly().GetName();
            var appVersion = appName.Version;
            return $"{appName.Name} {appVersion.Major}.{appVersion.Minor}.{appVersion.Revision}";
        }
    }
}
