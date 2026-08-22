namespace DuplicateFileTool.Configuration;

internal sealed class UserSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public ProgramSettings Program { get; set; } = new();
    public SearchSettings Search { get; set; } = new();
    public ResultsSettings Results { get; set; } = new();
    public ExtensionsSettings Extensions { get; set; } = new();

    public bool Normalize()
    {
        var corrected = false;
        if (Program == null)
        {
            Program = new ProgramSettings();
            corrected = true;
        }
        if (Search == null)
        {
            Search = new SearchSettings();
            corrected = true;
        }
        if (Results == null)
        {
            Results = new ResultsSettings();
            corrected = true;
        }
        if (Extensions == null)
        {
            Extensions = new ExtensionsSettings();
            corrected = true;
        }

        if (Search.MaximumFilesOpenedAtOnce is int maximumFilesOpenedAtOnce
            && maximumFilesOpenedAtOnce is < SearchConfiguration.MinimumOpenFileHandles or > SearchConfiguration.MaximumOpenFileHandles)
        {
            Search.MaximumFilesOpenedAtOnce = null;
            corrected = true;
        }
        if (Search.MinFileSize is < SearchConfiguration.MinimumFileSizeValue)
        {
            Search.MinFileSize = null;
            corrected = true;
        }
        if (Search.MaxFileSize is < SearchConfiguration.MinimumFileSizeValue)
        {
            Search.MaxFileSize = null;
            corrected = true;
        }
        if (Results.ItemsPerPage is int itemsPerPage
            && itemsPerPage is < ResultsConfiguration.MinimumItemsPerPage or > ResultsConfiguration.MaximumItemsPerPage)
        {
            Results.ItemsPerPage = null;
            corrected = true;
        }

        if (IsInvalidWidth(Results.NameColumnWidth))
        {
            Results.NameColumnWidth = null;
            corrected = true;
        }
        if (IsInvalidWidth(Results.SizeColumnWidth))
        {
            Results.SizeColumnWidth = null;
            corrected = true;
        }
        if (IsInvalidWidth(Results.ModifiedColumnWidth))
        {
            Results.ModifiedColumnWidth = null;
            corrected = true;
        }
        if (Extensions.Catalog is string catalog && !ExtensionsConfiguration.IsCatalogValid(catalog))
        {
            Extensions.Catalog = null;
            corrected = true;
        }
        return corrected;
    }

    private static bool IsInvalidWidth(double? width) =>
        width.HasValue && (!double.IsFinite(width.Value) || width.Value <= 0);
}

internal sealed class ProgramSettings
{
    public string? SelectedCulture { get; set; }
}

internal sealed class SearchSettings
{
    public int? MaximumFilesOpenedAtOnce { get; set; }
    public bool? ExcludeSystemFiles { get; set; }
    public bool? ExcludeHiddenFiles { get; set; }
    public bool? ExcludeOsFiles { get; set; }
    public bool? ExcludeZeroSizeFiles { get; set; }
    public Guid? SelectedFileComparerGuid { get; set; }
    public ByteSizeUnits? ByteSizeUnit { get; set; }
    public long? MinFileSize { get; set; }
    public long? MaxFileSize { get; set; }
    public InclusionType? ExtensionInclusionType { get; set; }
}

internal sealed class ResultsSettings
{
    public bool? SortDescending { get; set; }
    public SortOrder? SortOrder { get; set; }
    public int? ItemsPerPage { get; set; }
    public bool? RemoveEmptyDirectories { get; set; }
    public bool? DeleteToRecycleBin { get; set; }
    public double? NameColumnWidth { get; set; }
    public double? SizeColumnWidth { get; set; }
    public double? ModifiedColumnWidth { get; set; }
}

internal sealed class ExtensionsSettings
{
    public string? Catalog { get; set; }
}
