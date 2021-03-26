using System;
using System.Configuration;

namespace FileBadger.Configuration
{
    internal static class AppConfig
    {
        private static System.Configuration.Configuration Config { get; } = System.Configuration.ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

        public static T Get<T>(string parameterName, T defaultValue)
        {
            foreach (KeyValueConfigurationElement setting in Config.AppSettings.Settings)
            {
                if (setting.Key == parameterName)
                    return (T)Convert.ChangeType(setting.Value, typeof(T));
            }

            return defaultValue;
        }

        public static bool TryGet<T>(string parameterName, out T value)
        {
            foreach (KeyValueConfigurationElement setting in Config.AppSettings.Settings)
            {
                if (setting.Key != parameterName)
                    continue;

                value = (T) Convert.ChangeType(setting.Value, typeof(T));
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryGetString(string parameterName, out string value)
        {
            foreach (KeyValueConfigurationElement setting in Config.AppSettings.Settings)
            {
                if (setting.Key != parameterName)
                    continue;

                value = setting.Value; 
                return true;
            }

            value = default;
            return false;
        }

        public static void Set<T>(string parameterName, T value)
        {
            if (ReferenceEquals(value, null))
                Config.AppSettings.Settings.Remove(parameterName);
            else
                Config.AppSettings.Settings[parameterName].Value = value.ToString();
        }

        public static void Save()
        {
            Config.Save();
        }
    }
}
