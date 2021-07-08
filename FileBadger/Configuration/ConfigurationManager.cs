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
            var propertyNames = typeof(SearchConfiguration).GetPropertyNamesWhereAttribute(typeof(ConfigurationPropertyAttribute));
            var reflectedObject = new Reflected(configObject);
            foreach (var propertyName in propertyNames)
            {
                var path = $"{configType.Name}.{propertyName}";
                if (AppConfig.TryGetString(path, out var stringValue) && reflectedObject.TrySet(propertyName, stringValue))
                    continue;

                var defaultValueAttribute = configType.GetPropertyAttribute(propertyName, typeof(DefaultValueAttribute)) as DefaultValueAttribute;
                if (defaultValueAttribute == null)
                    continue;
                
                if (!reflectedObject.TrySet(propertyName, defaultValueAttribute.DefaultValue))
                    throw new ApplicationException($"Unable to set the default value to '{path}' configuration parameter");
            }
        }

        public static void SaveToAppConfig(this object configObject)
        {
            if (ReferenceEquals(configObject, null) || configObject is TrackedChangeNotifier<ConfigurationPropertyAttribute> trackedObject && trackedObject.HasChanged)
                return;

            var configType = configObject.GetType();
            var propertyNames = typeof(SearchConfiguration).GetPropertyNamesWhereAttribute(typeof(ConfigurationPropertyAttribute));
            var reflectedObject = new Reflected(configObject);

            foreach (var propertyName in propertyNames)
            {
                var path = $"{configType.Name}.{propertyName}";
                if (!reflectedObject.TryGet(propertyName, out var valueObject))
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
