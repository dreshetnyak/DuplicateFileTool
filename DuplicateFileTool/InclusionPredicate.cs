﻿using System;
using System.ComponentModel;
using System.Linq;
using DuplicateFileTool.Annotations;
using DuplicateFileTool.Configuration;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool
{
    internal interface IInclusionPredicate
    {
        bool IsFileIncluded(FileData fileData);
    }

    [Localizable(true)]
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
            if (SearchConfig.ExcludeSystemFiles.Value && fileAttributes.IsSystem || 
                SearchConfig.ExcludeHiddenFiles.Value && fileAttributes.IsHidden ||
                SearchConfig.ExcludeOsFiles.Value && fileData.FullName.StartsWith(WindowsOsPath, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!IsFileExtensionIncluded(fileData.Extension))
                return false;

            var maxFileSize = SearchConfig.MaxFileSize.Value;
            if (maxFileSize == 0)
                return true;

            var fileSize = fileData.Size;
            var sizeUnit = SearchConfig.ByteSizeUnit.Value;
            var maxSize = GetSizeInBytes(SearchConfig.MaxFileSize.Value, sizeUnit);
            if (fileSize > maxSize)
                return false;

            var minSize = GetSizeInBytes(SearchConfig.MinFileSize.Value, sizeUnit);
            if (fileSize > minSize)
                return false;

            return true;
        }

        private bool IsFileExtensionIncluded([NotNull] string fileExtension)
        {
            var extensions = SearchConfig.Extensions;
            if (extensions.Count == 0)
                return true;

            switch (SearchConfig.ExtensionInclusionType.Value)
            {
                case InclusionType.Include: return extensions.Any(ext => ext.Value.Equals(fileExtension, StringComparison.OrdinalIgnoreCase));
                case InclusionType.Exclude: return extensions.All(ext => !ext.Value.Equals(fileExtension, StringComparison.OrdinalIgnoreCase));
                default: throw new ApplicationException(Resources.Error_Unknown_file_extension_inclusion_type);
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
                        _ => throw new ArgumentOutOfRangeException(nameof(byteSizeUnits), byteSizeUnits, Resources.Error_Unable_to_convert_the_size_to_bytes_unknown_SizeUnits)
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