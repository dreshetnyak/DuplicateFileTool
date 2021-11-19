using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using DuplicateFileTool.Annotations;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool.Configuration
{
    public enum FileExtensionType { Other, Documents, Images, Audio, Video, SourceCode, Binaries }

    public interface IExtensionsTypeConverter
    {
        string GetExtensionTypeName(FileExtensionType extensionType);
        FileExtensionType GetExtensionType(string extension);
    }

    [DebuggerDisplay("{Extension,nq}; {Type,nq}")]
    public class FileExtension : INotifyPropertyChanged, ICloneable
    {
        private FileExtensionType _type;
        private string _extension;

        public static IExtensionsTypeConverter ExtensionConverter { get; set; }

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
                var trimmedValue = value?.Trim() ?? "";
                if (_extension == trimmedValue)
                    return;
                _extension = trimmedValue;
                OnPropertyChanged();
                Type = ExtensionConverter.GetExtensionType(trimmedValue);
            }
        }

        public FileExtension()
        {
            _extension = "";
            _type = FileExtensionType.Other;
        }

        public FileExtension(string extension)
        {
            _extension = extension;
            _type = ExtensionConverter?.GetExtensionType(extension) ?? FileExtensionType.Other;
        }

        public FileExtension(string extension, FileExtensionType type)
        {
            _extension = extension;
            _type = type;
        }

        #region ICloneable Implementation

        public object Clone()
        {
            return new FileExtension(Extension, Type);
        }

        #endregion

        #region Equality Members

        protected bool Equals(FileExtension other)
        {
            return Extension.Equals(other.Extension, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            
            if (ReferenceEquals(this, obj))
                return true;

            return obj.GetType() == GetType() && Equals((FileExtension)obj);
        }

        public override int GetHashCode()
        {
            return Extension != null
                ? Extension.GetHashCode()
                : 0;
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    internal class ExtensionsConfiguration : NotifyPropertyChanged, IChangeable, IExtensionsTypeConverter
    {
        private const string DEFAULT_EXTENSIONS = "Documents=pdf,epub,djvu,djv,azw,azw3,lit,cbr,cbz,chm,doc,docx,fb2,mobi,txt,rtf,xps;" +
                                                  "Images=jpg,jpeg,png,gif,bmp,tiff,tif,ico,psd,ai;" +
                                                  "Audio=aac,aiff,amr,ape,flac,gsm,m4a,m4b,m4p,mmf,mp3,ogg,oga,mogg,wav,wma;" +
                                                  "Video=webm,mkv,flv,f4v,vob,ogv,avi,mov,wmv,mp4,m4p,m4v,mpg,mp2,mpeg,mpe,mpv,m2v,m4v,3gp,divx,ts,m2ts,rmvb;" +
                                                  "SourceCode=c,h,cpp,hpp,cs,xaml,resx,config;" +
                                                  "Binaries=exe,obj,dll,sys,bin";
        
        public ObservableCollection<FileExtension> Extensions { get; }

        public ConfigurationProperty<string> ExtensionsSettings { get; } = new(
            Resources.Config_Extensions_Name,
            Resources.Config_Extensions_Description,
            DEFAULT_EXTENSIONS);

        public bool HasChanged
        {
            get => ChangeTracker.HasChanged;
            set => ChangeTracker.HasChanged = value;
        }

        private PropertiesChangeTracker<ExtensionsConfiguration> ChangeTracker { get; }

        public ExtensionsConfiguration()
        {
            FileExtension.ExtensionConverter = this;
            Extensions = new ObservableCollection<FileExtension>(GetFileExtensions(ExtensionsSettings.Value));
            SubscribeToExtensionsChanges();

            ChangeTracker = new PropertiesChangeTracker<ExtensionsConfiguration>(this);
            ChangeTracker.PropertyChanged += (_, _) => OnPropertyChanged(nameof(HasChanged));
        }

        private void SubscribeToExtensionsChanges()
        {
            foreach (var extension in Extensions)
                extension.PropertyChanged += (_, _) => ExtensionsSettings.Value = GetExtensionsConfiguration(Extensions);
        }

        private IEnumerable<FileExtension> GetFileExtensions(string extensionsData)
        {
            return extensionsData.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .SelectMany(ParseFileTypeExtensions)
                .OrderBy(fileExtensions => fileExtensions.Type.ToString());
        }

        private IEnumerable<FileExtension> ParseFileTypeExtensions(string fileTypeExtensionsData)
        {
            var typeDataSplit = fileTypeExtensionsData.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
            if (typeDataSplit.Length != 2)
                throw new ApplicationException("Invalid extensions configuration data, type and extensions divider not found");

            var fileExtensionType = ParseFileType(typeDataSplit[0]);
            return ParseExtensions(typeDataSplit[1]).Select(extension => new FileExtension(extension, fileExtensionType));
        }

        private static FileExtensionType ParseFileType(string fileTypeData)
        {
            if (!Enum.TryParse(fileTypeData.Trim(), out FileExtensionType fileType))
                throw new ApplicationException("Unknown extensions type found in the configuration");
            return fileType;
        }

        private static IEnumerable<string> ParseExtensions(string extensions)
        {
            return extensions.Trim()
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(extension => extension.Trim())
                .OrderBy(extension => extension);
        }

        private static string GetExtensionsConfiguration(IEnumerable<FileExtension> fileExtensions)
        {
            var configuration = new StringBuilder(512);
            var extensions = fileExtensions
                .OrderBy(fileExtension => fileExtension.Type)
                .ThenBy(fileExtension => fileExtension.Extension)
                .ToArray();

            var extensionsCount = 0;
            FileExtensionType currentFileExtensionType = default;
            foreach (var extension in extensions)
            {
                var configLength = configuration.Length;
                if (configLength == 0 || extension.Type != currentFileExtensionType)
                {
                    if (configLength != 0)
                        configuration.Append(';');
                    configuration.Append(extension.Type);
                    configuration.Append('=');
                    extensionsCount = 0;
                    currentFileExtensionType = extension.Type;
                }

                if (extensionsCount != 0)
                    configuration.Append(',');
                configuration.Append(extension.Extension);
                extensionsCount++;
            }

            return configuration.ToString();
        }

        public FileExtensionType GetExtensionType(string extension)
        {
            return Extensions.FirstOrDefault(fileExtension => fileExtension.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase))?.Type ?? FileExtensionType.Other;
        }

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
    }
}
