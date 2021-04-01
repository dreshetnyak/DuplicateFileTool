using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace FileBadger.Comparers
{
    internal class ComparableFileHashConfig : ComparerConfig
    {
        [DefaultValue(65535)]
        [IntRangeValidationRule(512, int.MaxValue)]
        [ConfigurationProperty("Hash chunk size", 
            "This file comparing algorithm does not compare the files byte to byte, but divides the file to chunks and calculates " +
            "the hash of the chunks and then compares the hash, this parameter specifies the size of the chunk to be used. A smaller " +
            "value will lead to a faster comparison but will consume more memory, a bigger value will lead to slower comparison, " +
            "but will require less memory.")]
        public int HashChunkSize { get; set; }
    }

    [DebuggerDisplay("{FileData.FullName}")]
    [FileComparer("56E94DDC-1021-49D5-8DB1-FF1C92710978", 
        "File Hash Comparer", 
        "Calculates file hash while read files, compares the hash values. The hash is cached to prevent reading files twice.", 
        typeof(ComparableFileHashConfig), 
        typeof(Factory),
        typeof(CandidatePredicate))]
    internal class ComparableFileHash : ComparableFile, IDisposable
    {
        #region Abstract Factory Implementation
        public class Factory : ComparableFileFactory
        {
            public Factory(IComparerConfig comparerConfig) : base(comparerConfig) { }

            public override ComparableFile Create(FileData file) => new ComparableFileHash(file, ComparerConfig);
        }
        
        #endregion

        #region Candidate Predicate Implementation
        public class CandidatePredicate : ICandidatePredicate
        {
            public bool IsCandidate(FileData firstFile, FileData secondFile)
            {
                return firstFile.Size == secondFile.Size;
            }
        }

        #endregion

        private int HashChunkSize { get; }
        private List<byte[]> Cache { get; }
        private FileReader FileReader { get; }
        private int TotalFragments { get; }

        private ComparableFileHash(FileData file, IComparerConfig config)
        {
            if (!(config is ComparableFileHashConfig fileHashComparerConfig))
                throw new ArgumentException("File hash comparer configuration object is of an invalid type", nameof(config));

            var fileFullName = file.FullName;
            if (!Win32.PathFileExists(fileFullName))
                throw new FileNotFoundException($"The file '{fileFullName}' does not exist");

            var hashChunkSize = fileHashComparerConfig.HashChunkSize;
            Debug.Assert(hashChunkSize > 0, "Invalid fragment size");

            FileData = file;
            HashChunkSize = hashChunkSize;
            Cache = new List<byte[]>();
            FileReader = new FileReader(fileFullName);
            TotalFragments = CalculateTotalFragments(file.Size, hashChunkSize);
        }

        public void Dispose()
        {
            FileReader?.Dispose();
        }

        private int CalculateTotalFragments(long fileLength, int fragmentSize)
        {
            var totalFragments = (int)(fileLength / fragmentSize);
            if (FileData.Size % fragmentSize != 0)
                totalFragments++;
            return totalFragments;
        }

        public override int CompareTo(ComparableFile otherFile, CancellationToken cancellationToken)
        {
            if (!(otherFile is ComparableFileHash otherFileHashComparer))
                throw new ArgumentException("File comparer type mismatch", nameof(otherFile));
            if (FileData.Size != otherFileHashComparer.FileData.Size)
                throw new ArgumentException("Comparing with the file hash object that has a different fragment size", nameof(otherFile));

            if (TotalFragments != otherFileHashComparer.TotalFragments)
                return CompleteMismatch;

            for (var fragmentIndex = 0; fragmentIndex < TotalFragments; fragmentIndex++)
            {
                var hash = GetFragmentHash(fragmentIndex);
                var otherHash = otherFileHashComparer.GetFragmentHash(fragmentIndex);
                if (!hash.ByteArrayEquals(otherHash))
                    return CompleteMismatch;
                cancellationToken.ThrowIfCancellationRequested();
            }

            return CompleteMatch;
        }

        private byte[] GetFragmentHash(int fragmentIndex)
        {
            if (fragmentIndex >= TotalFragments || fragmentIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(fragmentIndex), $"Fragment index '{fragmentIndex:N0}' is out of bounds, there is '{TotalFragments:N0}' fragments in the file");

            if (fragmentIndex < Cache.Count)
                return Cache[fragmentIndex];

            if (fragmentIndex != Cache.Count)
                throw new ApplicationException("The random access for hashing is not supported");

            var fragmentForHash = new byte[HashChunkSize];
            var bytesRead = FileReader.ReadNext(fragmentForHash);
            if (bytesRead == -1)
                throw new ApplicationException($"Can't read the file '{FileData.FullName}'. " + new Win32Exception(Marshal.GetLastWin32Error()).Message);

            var hash = Hash.Compute(fragmentForHash.SubArray(0, bytesRead));
            Cache.Add(hash);

            return hash;
        }
    }
}
