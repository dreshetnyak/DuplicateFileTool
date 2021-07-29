using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DuplicateFileTool
{
    #region DuplicatesSearchProgress Implementation

    internal class DuplicatesSearchProgressEventArgs : EventArgs
    {
        public string FilePath { get; }
        public int CurrentFileIndex { get; }
        public int TotalFilesCount { get; }
        public int DuplicateFilesCount { get; }
        public long DuplicatedTotalSize { get; set; }

        public DuplicatesSearchProgressEventArgs(string filePath, int currentFileIndex, int totalFilesCount, int duplicateFilesCount, long duplicatedTotalSize)
        {
            FilePath = filePath;
            CurrentFileIndex = currentFileIndex;
            TotalFilesCount = totalFilesCount;
            DuplicateFilesCount = duplicateFilesCount;
            DuplicatedTotalSize = duplicatedTotalSize;
        }
    }

    internal delegate void DuplicatesSearchProgressEventHandler(object sender, DuplicatesSearchProgressEventArgs eventArgs);

    #endregion

    #region DuplicatesGroupFound Implementation

    internal class DuplicatesGroupFoundEventArgs : EventArgs
    {
        public List<MatchResult> DuplicatesGroup { get; }

        public DuplicatesGroupFoundEventArgs(List<MatchResult> duplicatesGroup)
        {
            DuplicatesGroup = duplicatesGroup;
        }
    }

    internal delegate void DuplicatesGroupFoundEventHandler(object sender, DuplicatesGroupFoundEventArgs eventArgs);

    #endregion

    internal class MatchResult
    {
        public IComparableFile ComparableFile { get; set; }
        public int MatchValue { get; set; }
        public int CompleteMatch { get; set; }
        public int CompleteMismatch { get; set; }
    }

    internal class DuplicatesSearch
    {
        private class SearchContext
        {
            public IFileComparerConfig ComparerConfig { get; set; }
            public int CurrentFileIndex { get; set; }
            public int TotalFilesCount { get; set; }
            public int DuplicateGroupsCount { get; set; }
            public int DuplicateFilesCount { get; set; }
            public long DuplicatedTotalSize { get; set; }
        }

        public event DuplicatesGroupFoundEventHandler DuplicatesGroupFound;
        public event DuplicatesSearchProgressEventHandler DuplicatesSearchProgress;
        public event FileSystemErrorEventHandler FileSystemError;

        public async Task Find(IReadOnlyCollection<IComparableFile[]> duplicateCandidates, IFileComparerConfig comparerConfig, CancellationToken cancellationToken)
        {
            await Task.Run(() => GetDuplicatesFromCandidates(duplicateCandidates, comparerConfig, cancellationToken), cancellationToken);
        }

        private void GetDuplicatesFromCandidates(IReadOnlyCollection<IComparableFile[]> duplicateCandidates, IFileComparerConfig comparerConfig, CancellationToken cancellationToken)
        {
            var context = new SearchContext { ComparerConfig = comparerConfig, TotalFilesCount = duplicateCandidates.AsParallel().Sum(group => group.Length) };

            foreach (var duplicateCandidateGroup in duplicateCandidates)
            {
                var groupDuplicates = GetDuplicatesFromGroup(duplicateCandidateGroup, context, cancellationToken);
                if (groupDuplicates.Count == 0)
                    continue;

                context.DuplicateGroupsCount += groupDuplicates.Count;
                context.DuplicateFilesCount += groupDuplicates.Sum(group => group.Count);
                context.DuplicatedTotalSize += GetGroupDuplicatedSize(groupDuplicates);
                OnDuplicatesSearchProgress(groupDuplicates.First().First().ComparableFile.FileData.FullName, context);

                foreach (var group in groupDuplicates)
                    OnDuplicatesGroupFound(group);
            }
        }

        private static long GetGroupDuplicatedSize(IEnumerable<List<MatchResult>> duplicateGroups)
        {
            var size = 0L;
            foreach (var duplicateGroup in duplicateGroups)
            {
                var smallestFileSize = duplicateGroup.Min(item => item.ComparableFile.FileData.Size);
                size += duplicateGroup.Sum(item => item.ComparableFile.FileData.Size) - smallestFileSize;
            }

            return size;
        }

        private List<List<MatchResult>> GetDuplicatesFromGroup(IReadOnlyCollection<IComparableFile> fileGroup, SearchContext context, CancellationToken cancellationToken)
        {
            var duplicates = new List<List<MatchResult>>();
            foreach (var fileFromGroup in fileGroup)
            {
                try
                {
                    if (ContainsFile(duplicates, fileFromGroup))
                        continue;

                    OnDuplicatesSearchProgress(fileFromGroup.FileData.FileName, context);
                
                    var fileFromGroupDuplicates = GetFileDuplicates(fileFromGroup, fileGroup, context, cancellationToken);

                    if (fileFromGroupDuplicates.Count != 0)
                       duplicates.Add(fileFromGroupDuplicates);
                }
                finally
                { context.CurrentFileIndex++; }
            }

            return duplicates;
        }

        private List<MatchResult> GetFileDuplicates(IComparableFile fileToFind, IEnumerable<IComparableFile> fileGroup, SearchContext context, CancellationToken cancellationToken)
        {
            var duplicates = new List<MatchResult>();
            var matchThreshold = context.ComparerConfig.MatchThreshold.Value;
            var completeMatch = context.ComparerConfig.CompleteMatch.Value;
            var completeMismatch = context.ComparerConfig.CompleteMismatch.Value;
            foreach (var fileFromGroup in fileGroup)
            {
                if (ReferenceEquals(fileFromGroup, fileToFind)) 
                    continue; //Skip self

                int matchValue;
                try
                {
                    if ((matchValue = fileToFind.CompareTo(fileFromGroup, cancellationToken)) < matchThreshold)
                        continue;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (FileSystemException ex)
                {
                    OnFileSystemError(ex.FileFullName, ex.Message, ex);
                    continue;
                }
                catch (Exception ex)
                {
                    OnFileSystemError(fileFromGroup.FileData.FullName, ex.Message, ex);
                    continue;
                }

                if (duplicates.Count == 0)
                    duplicates.Add(new MatchResult { ComparableFile = fileToFind, MatchValue = completeMatch, CompleteMatch = completeMatch, CompleteMismatch = completeMismatch });
                duplicates.Add(new MatchResult { ComparableFile = fileFromGroup, MatchValue = matchValue, CompleteMatch = completeMatch, CompleteMismatch = completeMismatch });
            }

            return duplicates;
        }

        private static bool ContainsFile(IEnumerable<List<MatchResult>> filesWhereToLook, IComparableFile fileToFind)
        {
            return filesWhereToLook.Any(fileGroupFromFiles => fileGroupFromFiles.Any(fileFromGroup => ReferenceEquals(fileToFind, fileFromGroup.ComparableFile)));
        }

        private void OnDuplicatesGroupFound(List<MatchResult> duplicatesGroup)
        {
            DuplicatesGroupFound?.Invoke(this, new DuplicatesGroupFoundEventArgs(duplicatesGroup));
        }
        
        private void OnDuplicatesSearchProgress(string filePath, SearchContext context)
        {
            DuplicatesSearchProgress?.Invoke(this, new DuplicatesSearchProgressEventArgs(filePath, context.CurrentFileIndex, context.TotalFilesCount, context.DuplicateFilesCount, context.DuplicatedTotalSize));
        }

        protected virtual void OnFileSystemError(string path, string message, Exception exception = null)
        {
            FileSystemError?.Invoke(this, new FileSystemErrorEventArgs(path, message, exception));
        }
    }
}
