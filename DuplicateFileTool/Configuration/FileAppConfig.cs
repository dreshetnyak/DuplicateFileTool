using System.Configuration;

namespace DuplicateFileTool.Configuration;

internal static class FileAppConfig
{
    private static System.Configuration.Configuration Config { get; } = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

    public static T Get<T>(string parameterName, T defaultValue)
    {
        foreach (KeyValueConfigurationElement setting in Config.AppSettings.Settings)
        {
            if (setting.Key == parameterName)
                return (T)Convert.ChangeType(setting.Value, typeof(T));
        }

        return defaultValue;
    }

    public static bool TryGet<T>(string parameterName, out T? value)
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

        value = "";
        return false;
    }

    public static void Set<T>(string parameterName, T? value) where T : class
    {
        var appSettings = Config.AppSettings.Settings;
        if (value is null)
        {
            appSettings.Remove(parameterName);
            return;
        }

        if (IsKeyExist(appSettings, parameterName))
            Config.AppSettings.Settings[parameterName].Value = value.ToString();
        else
            appSettings.Add(parameterName, value.ToString());
    }

    private static bool IsKeyExist(KeyValueConfigurationCollection appSettings, string parameterName)
    {
        foreach (var key in appSettings.AllKeys)
        {
            if (key == parameterName) 
                return true;
        }

        return false;
    }

    public static void Save() => 
        Config.Save();
}