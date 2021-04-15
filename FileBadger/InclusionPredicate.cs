using System;
using System.Linq;
using FileBadger.Annotations;
using FileBadger.Configuration;

namespace FileBadger
{
    internal interface IInclusionPredicate
    {
        bool IsFileIncluded(FileData fileData);
    }

    internal class InclusionPredicate : IInclusionPredicate
    {
        public SearchConfiguration SearchConfig { get; }
        public static string WindowsOsPath { get; } = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        public InclusionPredicate(SearchConfiguration searchConfig)
        {
            SearchConfig = searchConfig;
        }

        public bool IsFileIncluded(FileData fileData)
        {
            var fileAttributes = fileData.Attributes;
            if (SearchConfig.ExcludeSystemFiles && fileAttributes.IsSystem || 
                SearchConfig.ExcludeHiddenFiles && fileAttributes.IsHidden ||
                SearchConfig.ExcludeOsFiles && fileData.FullName.StartsWith(WindowsOsPath, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!IsFileExtensionIncluded(fileData.Extension))
                return false;

            var maxFileSize = SearchConfig.MaxFileSize;
            if (maxFileSize == 0)
                return true;

            var fileSize = fileData.Size;
            var sizeUnit = SearchConfig.ByteSizeUnit;
            var maxSize = GetSizeInBytes(SearchConfig.MaxFileSize, sizeUnit);
            if (fileSize > maxSize)
                return false;

            var minSize = GetSizeInBytes(SearchConfig.MinFileSize, sizeUnit);
            if (fileSize > minSize)
                return false;

            return true;
        }

        private bool IsFileExtensionIncluded([NotNull] string fileExtension)
        {
            var extensions = SearchConfig.Extensions;
            if (extensions.Count == 0)
                return true;

            switch (SearchConfig.ExtensionInclusionType)
            {
                case InclusionType.Include: return extensions.Any(ext => ext.Equals(fileExtension, StringComparison.OrdinalIgnoreCase));
                case InclusionType.Exclude: return extensions.All(ext => !ext.Equals(fileExtension, StringComparison.OrdinalIgnoreCase));
                default: throw new ApplicationException("Unknown file extension inclusion type");
            }
        }

        private static long GetSizeInBytes(long size, ByteSizeUnits byteSizeUnits)
        {
            try
            {
                checked 
                {
                    return byteSizeUnits switch
                    {
                        ByteSizeUnits.Bytes => size,
                        ByteSizeUnits.Kilobytes => size * 1_024L,
                        ByteSizeUnits.Megabytes => size * 1_048_576L,
                        ByteSizeUnits.Gigabytes => size * 1_073_741_824L,
                        _ => throw new ArgumentOutOfRangeException(nameof(byteSizeUnits), byteSizeUnits, "Unable to convert the size to bytes, unknown SizeUnits parameter")
                    };
                }
            }
            catch (OverflowException)
            {
                return long.MaxValue;
            }
        }
    }
}
