using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace DuplicateFileTool.Configuration;

internal static class SettingsService
{
    public static SettingsStore Current { get; } = new();
}

internal sealed class SettingsStore
{
    private const string SettingsDirectoryName = "DuplicateFileTool";
    private const string SettingsFileName = "settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    private readonly string[] _legacyConfigPaths;
    private bool _isLoaded;
    private bool _saveBlocked;

    public string SettingsPath { get; }
    public UserSettings Settings { get; private set; } = new();
    public bool NeedsInitialSave { get; private set; }
    public bool InvalidValuesReset { get; private set; }
    public Exception? LoadException { get; private set; }
    public Exception? QuarantineException { get; private set; }
    public string? QuarantinedSettingsPath { get; private set; }

    public SettingsStore()
        : this(GetDefaultSettingsPath(), GetDefaultLegacyConfigPaths())
    {
    }

    internal SettingsStore(string settingsPath, IEnumerable<string> legacyConfigPaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        SettingsPath = Path.GetFullPath(settingsPath);
        _legacyConfigPaths = legacyConfigPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void Load()
    {
        if (_isLoaded)
            return;

        _isLoaded = true;
        if (File.Exists(SettingsPath))
        {
            LoadJsonSettings();
            return;
        }

        var legacyConfigPath = _legacyConfigPaths.FirstOrDefault(File.Exists);
        if (legacyConfigPath == null)
            return;

        try
        {
            Settings = LoadLegacySettings(legacyConfigPath);
            InvalidValuesReset = Settings.Normalize();
            NeedsInitialSave = true;
        }
        catch (Exception ex)
        {
            LoadException = ex;
            Settings = new UserSettings();
            _saveBlocked = true;
        }
    }

    public void Save(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!_isLoaded)
            Load();
        if (_saveBlocked)
            throw new SettingsSaveBlockedException(LoadException);

        settings.SchemaVersion = UserSettings.CurrentSchemaVersion;
        settings.Normalize();
        var json = JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions);
        var settingsDirectory = Path.GetDirectoryName(SettingsPath)
            ?? throw new InvalidOperationException("The settings file path does not contain a directory.");
        Directory.CreateDirectory(settingsDirectory);

        var temporaryPath = Path.Combine(settingsDirectory, $".{SettingsFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(json);
                stream.Flush(flushToDisk: true);
            }

            ReplaceSettingsFile(temporaryPath);
            Settings = settings;
            NeedsInitialSave = false;
            InvalidValuesReset = false;
            _saveBlocked = false;
            LoadException = null;
            QuarantineException = null;
            QuarantinedSettingsPath = null;
        }
        finally
        {
            try { File.Delete(temporaryPath); }
            catch { /* A failed save must retain its original exception. */ }
        }
    }

    private void LoadJsonSettings()
    {
        try
        {
            using var stream = new FileStream(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using (var jsonDocument = JsonDocument.Parse(stream))
            {
                var schemaVersion = ReadSchemaVersion(jsonDocument.RootElement);
                if (schemaVersion != UserSettings.CurrentSchemaVersion)
                    throw new UnsupportedSettingsVersionException(schemaVersion);
            }

            stream.Position = 0;
            Settings = JsonSerializer.Deserialize<UserSettings>(stream, JsonOptions)
                ?? throw new InvalidDataException("The settings file contains no settings document.");
            InvalidValuesReset = Settings.Normalize();
            NeedsInitialSave = InvalidValuesReset;
        }
        catch (UnsupportedSettingsVersionException ex)
        {
            LoadException = ex;
            Settings = new UserSettings();
            _saveBlocked = true;
        }
        catch (JsonException ex)
        {
            RecoverFromInvalidSettings(ex);
        }
        catch (InvalidDataException ex)
        {
            RecoverFromInvalidSettings(ex);
        }
        catch (Exception ex)
        {
            LoadException = ex;
            Settings = new UserSettings();
            _saveBlocked = true;
        }
    }

    private void RecoverFromInvalidSettings(Exception exception)
    {
        LoadException = exception;
        Settings = new UserSettings();
        TryQuarantineInvalidSettings();
        NeedsInitialSave = QuarantinedSettingsPath != null;
        _saveBlocked = QuarantinedSettingsPath == null;
    }

    private static int ReadSchemaVersion(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("The settings document must be a JSON object.");
        if (!root.TryGetProperty("schemaVersion", out var schemaVersion))
            return UserSettings.CurrentSchemaVersion;
        if (schemaVersion.ValueKind != JsonValueKind.Number || !schemaVersion.TryGetInt32(out var version))
            throw new JsonException("The settings schema version must be an integer.");
        return version;
    }

    private void TryQuarantineInvalidSettings()
    {
        var settingsDirectory = Path.GetDirectoryName(SettingsPath);
        if (settingsDirectory == null)
            return;

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ", CultureInfo.InvariantCulture);
        var backupPath = Path.Combine(settingsDirectory, $"settings.corrupt.{timestamp}.json");
        try
        {
            File.Move(SettingsPath, backupPath);
            QuarantinedSettingsPath = backupPath;
        }
        catch (Exception ex)
        {
            QuarantineException = ex;
        }
    }

    private void ReplaceSettingsFile(string temporaryPath)
    {
        if (File.Exists(SettingsPath))
        {
            File.Replace(temporaryPath, SettingsPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            return;
        }

        try
        {
            File.Move(temporaryPath, SettingsPath);
        }
        catch (IOException) when (File.Exists(SettingsPath))
        {
            File.Replace(temporaryPath, SettingsPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
    }

    private static string GetDefaultSettingsPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, SettingsDirectoryName, SettingsFileName);
    }

    private static IEnumerable<string> GetDefaultLegacyConfigPaths()
    {
        var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? SettingsDirectoryName;
        yield return Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.dll.config");
        yield return Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.exe.config");

        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
            yield return processPath + ".config";
    }

    private static UserSettings LoadLegacySettings(string configPath)
    {
        var readerSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        using var reader = XmlReader.Create(configPath, readerSettings);
        var document = XDocument.Load(reader);
        var values = document.Root?
            .Element("appSettings")?
            .Elements("add")
            .Where(element => element.Attribute("key") != null)
            .GroupBy(element => element.Attribute("key")!.Value, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Last().Attribute("value")?.Value ?? "",
                StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);

        var settings = new UserSettings();
        LoadLegacyProgramSettings(values, settings.Program);
        LoadLegacySearchSettings(values, settings.Search);
        LoadLegacyResultsSettings(values, settings.Results);
        LoadLegacyExtensionsSettings(values, settings.Extensions);
        return settings;
    }

    private static void LoadLegacyProgramSettings(IReadOnlyDictionary<string, string> values, ProgramSettings settings)
    {
        if (TryGetLegacyValue(values, nameof(ProgramConfiguration), nameof(ProgramConfiguration.SelectedCulture), out var value))
            settings.SelectedCulture = value;
    }

    private static void LoadLegacySearchSettings(IReadOnlyDictionary<string, string> values, SearchSettings settings)
    {
        if (TryParseLegacy(values, nameof(SearchConfiguration), nameof(SearchConfiguration.MaximumFilesOpenedAtOnce), int.TryParse, out int maximumFilesOpenedAtOnce))
            settings.MaximumFilesOpenedAtOnce = maximumFilesOpenedAtOnce;
        if (TryParseLegacy(values, nameof(SearchConfiguration), nameof(SearchConfiguration.ExcludeSystemFiles), bool.TryParse, out bool excludeSystemFiles))
            settings.ExcludeSystemFiles = excludeSystemFiles;
        if (TryParseLegacy(values, nameof(SearchConfiguration), nameof(SearchConfiguration.ExcludeHiddenFiles), bool.TryParse, out bool excludeHiddenFiles))
            settings.ExcludeHiddenFiles = excludeHiddenFiles;
        if (TryParseLegacy(values, nameof(SearchConfiguration), nameof(SearchConfiguration.ExcludeOsFiles), bool.TryParse, out bool excludeOsFiles))
            settings.ExcludeOsFiles = excludeOsFiles;
        if (TryParseLegacy(values, nameof(SearchConfiguration), nameof(SearchConfiguration.ExcludeZeroSizeFiles), bool.TryParse, out bool excludeZeroSizeFiles))
            settings.ExcludeZeroSizeFiles = excludeZeroSizeFiles;
        if (TryParseLegacy(values, nameof(SearchConfiguration), nameof(SearchConfiguration.SelectedFileComparerGuid), Guid.TryParse, out Guid selectedFileComparerGuid))
            settings.SelectedFileComparerGuid = selectedFileComparerGuid;
        if (TryParseLegacyEnum(values, nameof(SearchConfiguration), nameof(SearchConfiguration.ByteSizeUnit), out ByteSizeUnits byteSizeUnit))
            settings.ByteSizeUnit = byteSizeUnit;
        if (TryParseLegacy(values, nameof(SearchConfiguration), nameof(SearchConfiguration.MinFileSize), TryParseInt64, out long minFileSize))
            settings.MinFileSize = minFileSize;
        if (TryParseLegacy(values, nameof(SearchConfiguration), nameof(SearchConfiguration.MaxFileSize), TryParseInt64, out long maxFileSize))
            settings.MaxFileSize = maxFileSize;
        if (TryParseLegacyEnum(values, nameof(SearchConfiguration), nameof(SearchConfiguration.ExtensionInclusionType), out InclusionType extensionInclusionType))
            settings.ExtensionInclusionType = extensionInclusionType;
    }

    private static void LoadLegacyResultsSettings(IReadOnlyDictionary<string, string> values, ResultsSettings settings)
    {
        if (TryParseLegacy(values, nameof(ResultsConfiguration), nameof(ResultsConfiguration.SortDescending), bool.TryParse, out bool sortDescending))
            settings.SortDescending = sortDescending;
        if (TryParseLegacyEnum(values, nameof(ResultsConfiguration), nameof(ResultsConfiguration.SortOrder), out SortOrder sortOrder))
            settings.SortOrder = sortOrder;
        if (TryParseLegacy(values, nameof(ResultsConfiguration), nameof(ResultsConfiguration.ItemsPerPage), int.TryParse, out int itemsPerPage))
            settings.ItemsPerPage = itemsPerPage;
        if (TryParseLegacy(values, nameof(ResultsConfiguration), nameof(ResultsConfiguration.RemoveEmptyDirectories), bool.TryParse, out bool removeEmptyDirectories))
            settings.RemoveEmptyDirectories = removeEmptyDirectories;
        if (TryParseLegacy(values, nameof(ResultsConfiguration), nameof(ResultsConfiguration.DeleteToRecycleBin), bool.TryParse, out bool deleteToRecycleBin))
            settings.DeleteToRecycleBin = deleteToRecycleBin;
        if (TryParseLegacy(values, nameof(ResultsConfiguration), nameof(ResultsConfiguration.NameColumnWidth), TryParseDouble, out double nameColumnWidth))
            settings.NameColumnWidth = nameColumnWidth;
        if (TryParseLegacy(values, nameof(ResultsConfiguration), nameof(ResultsConfiguration.SizeColumnWidth), TryParseDouble, out double sizeColumnWidth))
            settings.SizeColumnWidth = sizeColumnWidth;
        if (TryParseLegacy(values, nameof(ResultsConfiguration), nameof(ResultsConfiguration.ModifiedColumnWidth), TryParseDouble, out double modifiedColumnWidth))
            settings.ModifiedColumnWidth = modifiedColumnWidth;
    }

    private static void LoadLegacyExtensionsSettings(IReadOnlyDictionary<string, string> values, ExtensionsSettings settings)
    {
        if (TryGetLegacyValue(values, nameof(ExtensionsConfiguration), nameof(ExtensionsConfiguration.ExtensionsSettings), out var value))
            settings.Catalog = value;
    }

    private static bool TryGetLegacyValue(
        IReadOnlyDictionary<string, string> values,
        string sectionName,
        string propertyName,
        out string value) =>
        values.TryGetValue($"{sectionName}.{propertyName}", out value!);

    private static bool TryParseLegacy<T>(
        IReadOnlyDictionary<string, string> values,
        string sectionName,
        string propertyName,
        TryParse<T> parser,
        out T result)
    {
        result = default!;
        return TryGetLegacyValue(values, sectionName, propertyName, out var value) && parser(value, out result);
    }

    private static bool TryParseLegacyEnum<T>(
        IReadOnlyDictionary<string, string> values,
        string sectionName,
        string propertyName,
        out T result) where T : struct, Enum
    {
        result = default;
        return TryGetLegacyValue(values, sectionName, propertyName, out var value) && Enum.TryParse(value, ignoreCase: true, out result);
    }

    private static bool TryParseInt64(string value, out long result) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static bool TryParseDouble(string value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private delegate bool TryParse<T>(string value, out T result);
}

internal sealed class UnsupportedSettingsVersionException(int version)
    : Exception($"Settings schema version {version} is not supported by this application version.")
{
    public int Version { get; } = version;
}

internal sealed class SettingsSaveBlockedException(Exception? loadException)
    : Exception("The existing settings file could not be read safely and was left unchanged.", loadException);
