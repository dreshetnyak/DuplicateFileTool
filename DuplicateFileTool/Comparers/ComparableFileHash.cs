using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool.Comparers
{
    internal class ComparableFileHashConfig : FileComparerConfig
    {
        public IConfigurationProperty<int> HashChunkSize { get; } = new ConfigurationProperty<int>(
            Resources.Config_ComparableFileHash_HashChunkSize_Name,
            Resources.Config_ComparableFileHash_HashChunkSize_Description,
            65535, new LongValidationRule(512, int.MaxValue));
    }

    internal class FileHashComparer : FileComparer, IDisposable
    {
        public static HashAlgorithm Hash { get; private set; } = MD5.Create();

        // ReSharper disable once LocalizableElement
        public FileHashComparer() : base(Guid.Parse("56E94DDC-1021-49D5-8DB1-FF1C92710978"), Resources.FileHashComparer_Name, Resources.FileHashComparer_Description)
        {
            Config = new ComparableFileHashConfig();
            ComparableFileFactory = new ComparableFileHash.Factory(Config);
            CandidatePredicate = new ComparableFileHash.CandidatePredicate();
        }

        public void Dispose()
        {
            Hash?.Dispose();
            Hash = null;
        }
    }

    [Localizable(true)]
    [DebuggerDisplay("{FileData.FullName,nq}")]
    internal class ComparableFileHash : IComparableFile, IDisposable
    {
        #region Candidate Predicate Implementation
        public class CandidatePredicate : ICandidatePredicate
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsCandidate(FileData firstFile, FileData secondFile)
            {
                return firstFile.Size == secondFile.Size;
            }
        }

        #endregion

        #region ComparableFileHash Factory

        internal class Factory : IComparableFileFactory
        {
            public IFileComparerConfig Config { get; }

            public Factory(IFileComparerConfig config) { Config = config; }

            public IComparableFile Create(FileData file) => new ComparableFileHash(file, (ComparableFileHashConfig)Config);
        }

        #endregion
        
        public FileData FileData { get; private set; }

        private static int HashChunkSize { get; set; }
        private static int CompleteMatch { get; set; }
        private static int CompleteMismatch { get; set; }

        private List<byte[]> Cache { get; set; }
        private FileReader FileReader { get; set; }
        private int TotalFragments { get; }

        private ComparableFileHash(FileData file, ComparableFileHashConfig config)
        {
            var fileFullName = file.FullName;
            if (!FileSystem.PathExists(fileFullName))
                throw new FileNotFoundException(string.Format(Resources.Error_File_x_not_found, fileFullName));
            
            HashChunkSize = config.HashChunkSize.Value;
            Debug.Assert(HashChunkSize > 0, Resources.Error_ComparableFileHash_Invalid_fragment_size);
            CompleteMatch = config.CompleteMatch.Value;
            CompleteMismatch = config.CompleteMismatch.Value;
            Debug.Assert(CompleteMatch > CompleteMismatch, Resources.Error_ComparableFileHash_CompleteMatch_and_CompleteMismatch_is_defined_incorrectly);

            FileData = file;
            Cache = new List<byte[]>();
            FileReader = new FileReader(fileFullName);
            TotalFragments = CalculateTotalFragments(file.Size, HashChunkSize);
        }

        public void Dispose()
        {
            FileData = null;
            Cache?.Clear();
            Cache = null;
            FileReader?.Dispose();
            FileReader = null;
        }

        private int CalculateTotalFragments(long fileLength, int fragmentSize)
        {
            var totalFragments = (int)(fileLength / fragmentSize);
            if (FileData.Size % fragmentSize != 0)
                totalFragments++;
            return totalFragments;
        }

        public int CompareTo(IComparableFile otherFile, CancellationToken cancellationToken)
        {
            Debug.Assert(otherFile is ComparableFileHash, Resources.Error_ComparableFileHash_CompareTo_File_Comparer_type_mismatch);
            Debug.Assert(FileData.Size == ((ComparableFileHash)otherFile).FileData.Size, Resources.Error_ComparableFileHash_CompareTo_Fragment_Size_Mismatch);
            var otherFileHashComparer = (ComparableFileHash)otherFile;
            if (TotalFragments != otherFileHashComparer.TotalFragments)
                return CompleteMismatch;

            for (var fragmentIndex = 0; fragmentIndex < TotalFragments; ++fragmentIndex)
            {
                var thisHash = GetFragmentHash(fragmentIndex);
                var otherHash = otherFileHashComparer.GetFragmentHash(fragmentIndex);

                // ReSharper disable once LoopCanBeConvertedToQuery
                for (var index = 0; index < thisHash.Length; ++index)
                {
                    if (thisHash[index] != otherHash[index])
                        return CompleteMismatch;
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            return CompleteMatch;
        }

        private byte[] GetFragmentHash(int fragmentIndex)
        {
            if (fragmentIndex >= TotalFragments || fragmentIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(fragmentIndex), string.Format(Resources.Error_ComparableFileHash_Fragment_index_is_out_of_bounds, fragmentIndex, TotalFragments));

            if (fragmentIndex < Cache.Count)
                return Cache[fragmentIndex];

            if (fragmentIndex != Cache.Count)
                throw new ApplicationException(Resources.Error_ComparableFileHash_The_random_access_for_hashing_is_not_supported);

            var fragmentForHash = new byte[HashChunkSize];
            var bytesRead = FileReader.ReadNext(fragmentForHash);
            if (bytesRead == -1)
                throw new ApplicationException(string.Format(Resources.Error_ComparableFileHash_Cant_read_the_file, FileData.FullName) + new Win32Exception(Marshal.GetLastWin32Error()).Message);

            var hashBytes = FileHashComparer.Hash.ComputeHash(fragmentForHash, 0, bytesRead);
            Cache.Add(hashBytes);
            return hashBytes;
        }
    }
}