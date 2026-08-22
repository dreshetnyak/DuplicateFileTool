using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool.Configuration;

public enum FileExtensionType { Other, Documents, Images, Audio, Video, SourceCode, Binaries }

public interface IExtensionsTypeConverter
{
    string GetExtensionTypeName(FileExtensionType extensionType);
    FileExtensionType GetExtensionType(string extension);
}

[Localizable(true)]
internal sealed class ExtensionsCatalogValidationRule : ValidationRule
{
    public override ValidationResult Validate(object? value, CultureInfo cultureInfo) =>
        value is string catalog && ExtensionsConfiguration.IsCatalogValid(catalog)
            ? new ValidationResult(true, null)
            : new ValidationResult(false, Resources.Error_Invalid_extension_catalog);
}

[DebuggerDisplay("{Extension,nq}; {Type,nq}")]
public sealed class FileExtension(string extension, FileExtensionType type) : INotifyPropertyChanged, ICloneable
{
    private FileExtensionType _type = type;
    private string _extension = extension;

    public static IExtensionsTypeConverter? ExtensionConverter { get; set; }

    public FileExtensionType Type
    {
        get => _type;
        set
        {
            if (_type == value)
                return;
            _type = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TypeName));
        }
    }
    public string TypeName => ExtensionConverter?.GetExtensionTypeName(Type) ?? "";

    public string Extension
    {
        get => _extension;
        set
        {
            var trimmedValue = value.Trim();
            if (_extension == trimmedValue)
                return;
            _extension = trimmedValue;
            OnPropertyChanged();
            if (ExtensionConverter != null)
                Type = ExtensionConverter.GetExtensionType(trimmedValue);
        }
    }

    public FileExtension() : this("", FileExtensionType.Other)
    { }

    public FileExtension(string extension) : this(extension, ExtensionConverter?.GetExtensionType(extension) ?? FileExtensionType.Other)
    { }

    #region ICloneable Implementation

    public object Clone() => 
        new FileExtension(Extension, Type);

    #endregion

    #region Equality Members

    private bool Equals(FileExtension other) => 
        Extension.Equals(other.Extension, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;
            
        if (ReferenceEquals(this, obj))
            return true;

        return obj.GetType() == GetType() && Equals((FileExtension)obj);
    }

    public override int GetHashCode() => Extension != ""
        ? Extension.GetHashCode()
        : 0;

    #endregion

    #region INotifyPropertyChanged Implementation

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = "") => 
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion
}

internal sealed class ExtensionsConfiguration : NotifyPropertyChanged, IChangeable, IExtensionsTypeConverter, IDisposable
{
    private const string DEFAULT_EXTENSIONS =
        "Documents=pdf,epub,djvu,djv,azw,azw3,lit,cbr,cbz,chm,doc,docx,fb2,mobi,txt,rtf,xps;" +
        "Images=jpg,jpeg,png,gif,bmp,tiff,tif,ico,psd,ai;" +
        "Audio=aac,aiff,amr,ape,flac,gsm,m4a,m4b,m4p,mmf,mp3,ogg,oga,mogg,wav,wma;" +
        "Video=webm,mkv,flv,f4v,vob,ogv,avi,mov,wmv,mp4,m4p,m4v,mpg,mp2,mpeg,mpe,mpv,m2v,m4v,3gp,divx,ts,m2ts,rmvb;" +
        "SourceCode=c,h,cpp,hpp,cs,xaml,resx,config;" +
        "Binaries=exe,obj,dll,sys,bin";
        
    // The persisted string is authoritative; consumers receive a stable read-only view rebuilt after valid changes.
    private ObservableCollection<FileExtension> MutableExtensions { get; } = [];
    public ReadOnlyObservableCollection<FileExtension> Extensions { get; }

    public ConfigurationProperty<string> ExtensionsSettings { get; } = new(
        Resources.Config_Extensions_Name,
        Resources.Config_Extensions_Description,
        DEFAULT_EXTENSIONS,
        new ExtensionsCatalogValidationRule());

    public bool HasChanged
    {
        get => ChangeTracker.HasChanged;
        set => ChangeTracker.HasChanged = value;
    }

    private PropertiesChangeTracker<ExtensionsConfiguration> ChangeTracker { get; }

    public ExtensionsConfiguration()
    {
        FileExtension.ExtensionConverter = this;
        Extensions = new ReadOnlyObservableCollection<FileExtension>(MutableExtensions);
        ReplaceExtensions(GetFileExtensions(ExtensionsSettings.Value!));
        ExtensionsSettings.PropertyChanged += OnExtensionsSettingsChanged;

        ChangeTracker = new PropertiesChangeTracker<ExtensionsConfiguration>(this);
        ChangeTracker.PropertyChanged += (_, _) => OnPropertyChanged(nameof(HasChanged));
    }

    private void OnExtensionsSettingsChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName != nameof(ConfigurationProperty<string>.Value)
            || ExtensionsSettings.Value is not string catalog)
            return;

        ReplaceExtensions(GetFileExtensions(catalog));
    }

    private void ReplaceExtensions(IEnumerable<FileExtension> extensions)
    {
        var parsedExtensions = extensions.ToArray();
        MutableExtensions.Clear();
        foreach (var extension in parsedExtensions)
            MutableExtensions.Add(extension);
    }

    internal static bool IsCatalogValid(string extensionsData)
    {
        try
        {
            _ = GetFileExtensions(extensionsData).ToArray();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static IEnumerable<FileExtension> GetFileExtensions(string extensionsData)
    {
        if (string.IsNullOrEmpty(extensionsData))
            return [];
        return extensionsData.Split([';'], StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(ParseFileTypeExtensions)
            .OrderBy(fileExtensions => fileExtensions.Type.ToString());
    }

    private static IEnumerable<FileExtension> ParseFileTypeExtensions(string fileTypeExtensionsData)
    {
        var typeDataSplit = fileTypeExtensionsData.Split(['='], StringSplitOptions.RemoveEmptyEntries);
        if (typeDataSplit.Length != 2)
            throw new InvalidOperationException("Invalid extensions configuration data, type and extensions divider not found");

        var fileExtensionType = ParseFileType(typeDataSplit[0]);
        return ParseExtensions(typeDataSplit[1]).Select(extension => new FileExtension(extension, fileExtensionType));
    }

    private static FileExtensionType ParseFileType(string fileTypeData)
    {
        var typeName = fileTypeData.Trim();
        if (!Enum.TryParse(typeName, out FileExtensionType fileType)
            || !string.Equals(Enum.GetName(fileType), typeName, StringComparison.Ordinal))
            throw new InvalidOperationException("Unknown extensions type found in the configuration");
        return fileType;
    }

    private static IEnumerable<string> ParseExtensions(string extensions)
    {
        return extensions.Trim()
            .Split([','], StringSplitOptions.RemoveEmptyEntries)
            .Select(extension => extension.Trim())
            .OrderBy(extension => extension);
    }

    public FileExtensionType GetExtensionType(string extension) => 
        Extensions.FirstOrDefault(fileExtension => fileExtension.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase))?.Type ?? FileExtensionType.Other;

    string IExtensionsTypeConverter.GetExtensionTypeName(FileExtensionType extensionType)
    {
        switch (extensionType)
        {
            case FileExtensionType.Documents: return Resources.Ui_Extension_Type_Name_Documents;
            case FileExtensionType.Images: return Resources.Ui_Extension_Type_Name_Images;
            case FileExtensionType.Audio: return Resources.Ui_Extension_Type_Name_Audio;
            case FileExtensionType.Video: return Resources.Ui_Extension_Type_Name_Video;
            case FileExtensionType.SourceCode: return Resources.Ui_Extension_Type_Name_SourceCode;
            case FileExtensionType.Binaries: return Resources.Ui_Extension_Type_Name_Binaries;
            case FileExtensionType.Other: return Resources.Ui_Extension_Type_Name_Unknown;
            default:
                Debug.Fail($"The support of the extension type '{extensionType}' has not been implemented.");
                return extensionType.ToString();
        }
    }

    public void Dispose()
    {
        ExtensionsSettings.PropertyChanged -= OnExtensionsSettingsChanged;
        ChangeTracker.Dispose();
        if (ReferenceEquals(FileExtension.ExtensionConverter, this))
            FileExtension.ExtensionConverter = null;
    }
}
