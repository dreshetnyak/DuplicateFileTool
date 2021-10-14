using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool.Configuration
{
    [Localizable(true)]
    internal class FileExtensions : NotifyPropertyChanged
    {
        public enum FileType { Documents, Images, Audio, Video, SourceCode, Binaries }

        private FileType _type;
        public FileType Type
        {
            get => _type;
            set
            {
                _type = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> ExtensionNames { get; }

        public FileExtensions(FileType type, IEnumerable<string> extensions)
        {
            _type = type;
            ExtensionNames = new ObservableCollection<string>(extensions);
        }
    }

    internal class ExtensionsConfiguration : NotifyPropertyChanged, IChangeable
    {
        private static string DefaultExtensions => "Documents=pdf,epub,djvu,djv,azw,azw3,lit,cbr,cbz,chm,doc,docx,fb2,mobi,txt,rtf,xps;" +
                                                   "Images=jpg,jpeg,png,gif,bmp,tiff,tif,ico,psd,ai;" +
                                                   "Audio=aac,aiff,amr,ape,flac,gsm,m4a,m4b,m4p,mmf,mp3,ogg,oga,mogg,wav,wma;" +
                                                   "Video=webm,mkv,flv,f4v,vob,ogv,avi,mov,wmv,mp4,m4p,m4v,mpg,mp2,mpeg,mpe,mpv,m2v,m4v,3gp,divx,ts,m2ts,rmvb;" +
                                                   "SourceCode=c,h,cpp,hpp,cs,xaml,resx,config;" +
                                                   "Binaries=exe,obj,dll,sys,bin";

        private ConfigurationProperty<string> ExtensionsSettings { get; } = new(
            Resources.Config_Results_Sort_Descending_Name,
            Resources.Config_Results_Sort_Descending_Description,
            DefaultExtensions);

        public ObservableCollection<FileExtensions> Extensions { get; }

        public bool HasChanged => ChangeTracker.HasChanged;

        private PropertiesChangeTracker<ExtensionsConfiguration> ChangeTracker { get; }

        public ExtensionsConfiguration()
        {
            Extensions = new ObservableCollection<FileExtensions>(GetFileExtensions(ExtensionsSettings.Value));
            SubscribeToExtensionsChanges();

            ChangeTracker = new PropertiesChangeTracker<ExtensionsConfiguration>(this);
            ChangeTracker.PropertyChanged += (_, _) => OnPropertyChanged(nameof(HasChanged));
        }

        private void SubscribeToExtensionsChanges()
        {
            foreach (var extension in Extensions)
                extension.ExtensionNames.CollectionChanged += (_, _) => ExtensionsSettings.Value = GetExtensionsConfiguration(Extensions);
        }

        private static IEnumerable<FileExtensions> GetFileExtensions(string extensionsData)
        {
            return extensionsData.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseFileTypeExtensions)
                .OrderBy(fileExtensions => fileExtensions.Type.ToString());
        }

        private static FileExtensions ParseFileTypeExtensions(string fileTypeExtensionsData)
        {
            var typeDataSplit = fileTypeExtensionsData.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
            if (typeDataSplit.Length != 2)
                throw new ApplicationException("Invalid extensions configuration data, type and extensions divider not found");
            return new FileExtensions(ParseFileType(typeDataSplit[0]), ParseExtensions(typeDataSplit[1]));
        }

        private static FileExtensions.FileType ParseFileType(string fileTypeData)
        {
            if (!Enum.TryParse(fileTypeData.Trim(), out FileExtensions.FileType fileType))
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

        private static string GetExtensionsConfiguration(IEnumerable<FileExtensions> fileExtensions)
        {
            var configuration = new StringBuilder(512);
            foreach (var fileExtension in fileExtensions)
            {
                if (configuration.Length != 0)
                    configuration.Append(';');
                configuration.Append(fileExtension.Type);
                configuration.Append('=');
                configuration.Append(string.Join(",", fileExtension.ExtensionNames));
            }

            return configuration.ToString();
        }
    }
}
