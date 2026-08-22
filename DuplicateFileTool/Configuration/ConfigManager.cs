using System.Reflection;

namespace DuplicateFileTool.Configuration;

internal interface IChangeable
{
    bool HasChanged { get; }
}

internal static class ConfigManager
{
    public static void LoadFromSettings(this ProgramConfiguration config, ProgramSettings settings)
    {
        if (settings.SelectedCulture != null)
            config.SelectedCulture.Value = settings.SelectedCulture;
    }

    public static void LoadFromSettings(this SearchConfiguration config, SearchSettings settings)
    {
        if (settings.MaximumFilesOpenedAtOnce.HasValue)
            config.MaximumFilesOpenedAtOnce.Value = settings.MaximumFilesOpenedAtOnce.Value;
        if (settings.ExcludeSystemFiles.HasValue)
            config.ExcludeSystemFiles.Value = settings.ExcludeSystemFiles.Value;
        if (settings.ExcludeHiddenFiles.HasValue)
            config.ExcludeHiddenFiles.Value = settings.ExcludeHiddenFiles.Value;
        if (settings.ExcludeOsFiles.HasValue)
            config.ExcludeOsFiles.Value = settings.ExcludeOsFiles.Value;
        if (settings.ExcludeZeroSizeFiles.HasValue)
            config.ExcludeZeroSizeFiles.Value = settings.ExcludeZeroSizeFiles.Value;
        if (settings.SelectedFileComparerGuid.HasValue)
            config.SelectedFileComparerGuid.Value = settings.SelectedFileComparerGuid.Value;
        if (settings.ByteSizeUnit.HasValue)
            config.ByteSizeUnit.Value = settings.ByteSizeUnit.Value;
        if (settings.MinFileSize.HasValue)
            config.MinFileSize.Value = settings.MinFileSize.Value;
        if (settings.MaxFileSize.HasValue)
            config.MaxFileSize.Value = settings.MaxFileSize.Value;
        if (settings.ExtensionInclusionType.HasValue)
            config.ExtensionInclusionType.Value = settings.ExtensionInclusionType.Value;
    }

    public static void LoadFromSettings(this ResultsConfiguration config, ResultsSettings settings)
    {
        if (settings.SortDescending.HasValue)
            config.SortDescending.Value = settings.SortDescending.Value;
        if (settings.SortOrder.HasValue)
            config.SortOrder.Value = settings.SortOrder.Value;
        if (settings.ItemsPerPage.HasValue)
            config.ItemsPerPage.Value = settings.ItemsPerPage.Value;
        if (settings.RemoveEmptyDirectories.HasValue)
            config.RemoveEmptyDirectories.Value = settings.RemoveEmptyDirectories.Value;
        if (settings.DeleteToRecycleBin.HasValue)
            config.DeleteToRecycleBin.Value = settings.DeleteToRecycleBin.Value;
        if (settings.NameColumnWidth.HasValue)
            config.NameColumnWidth.Value = settings.NameColumnWidth.Value;
        if (settings.SizeColumnWidth.HasValue)
            config.SizeColumnWidth.Value = settings.SizeColumnWidth.Value;
        if (settings.ModifiedColumnWidth.HasValue)
            config.ModifiedColumnWidth.Value = settings.ModifiedColumnWidth.Value;
    }

    public static void LoadFromSettings(this ExtensionsConfiguration config, ExtensionsSettings settings)
    {
        if (settings.Catalog != null)
            config.ExtensionsSettings.Value = settings.Catalog;
    }

    public static UserSettings CreateSettings(
        ProgramConfiguration program,
        SearchConfiguration search,
        ResultsConfiguration results,
        ExtensionsConfiguration extensions) =>
        new()
        {
            Program = new ProgramSettings
            {
                SelectedCulture = program.SelectedCulture.Value
            },
            Search = new SearchSettings
            {
                MaximumFilesOpenedAtOnce = search.MaximumFilesOpenedAtOnce.Value,
                ExcludeSystemFiles = search.ExcludeSystemFiles.Value,
                ExcludeHiddenFiles = search.ExcludeHiddenFiles.Value,
                ExcludeOsFiles = search.ExcludeOsFiles.Value,
                ExcludeZeroSizeFiles = search.ExcludeZeroSizeFiles.Value,
                SelectedFileComparerGuid = search.SelectedFileComparerGuid.Value,
                ByteSizeUnit = search.ByteSizeUnit.Value,
                MinFileSize = search.MinFileSize.Value,
                MaxFileSize = search.MaxFileSize.Value,
                ExtensionInclusionType = search.ExtensionInclusionType.Value
            },
            Results = new ResultsSettings
            {
                SortDescending = results.SortDescending.Value,
                SortOrder = results.SortOrder.Value,
                ItemsPerPage = results.ItemsPerPage.Value,
                RemoveEmptyDirectories = results.RemoveEmptyDirectories.Value,
                DeleteToRecycleBin = results.DeleteToRecycleBin.Value,
                NameColumnWidth = results.NameColumnWidth.Value,
                SizeColumnWidth = results.SizeColumnWidth.Value,
                ModifiedColumnWidth = results.ModifiedColumnWidth.Value
            },
            Extensions = new ExtensionsSettings
            {
                Catalog = extensions.ExtensionsSettings.Value
            }
        };

    public static void ResetToDefaults(this object? configObject)
    {
        if (ReferenceEquals(configObject, null))
            return;

        foreach (var property in configObject.GetGenericPropertiesObjects(typeof(IConfigurationProperty<>)))
        {
            var propertyType = property.GetType();
            var defaultValue = propertyType.GetProperty(nameof(IConfigurationProperty<int>.DefaultValue))?.GetValue(property);
            propertyType.GetProperty(nameof(IConfigurationProperty<int>.Value))?.SetValue(property, defaultValue);
        }
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
