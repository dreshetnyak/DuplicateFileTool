using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DuplicateFileTool
{
    internal class CandidatesSearchProgressEventArgs : EventArgs
    {
        public string FilePath { get; }
        public int CurrentFileIndex { get; }
        public int TotalFilesCount { get; }
        public int CandidateGroupsCount { get; }
        public int CandidateFilesCount { get; }
        public long CandidatesTotalSize { get; set; }

        public CandidatesSearchProgressEventArgs(string filePath, int currentFileIndex, int totalFilesCount, int candidateGroupsCount, int candidateFilesCount, long candidatesTotalSize)
        {
            FilePath = filePath; 
            CurrentFileIndex = currentFileIndex;
            TotalFilesCount = totalFilesCount;
            CandidateGroupsCount = candidateGroupsCount;
            CandidateFilesCount = candidateFilesCount;
            CandidatesTotalSize = candidatesTotalSize;
        }
    }

    internal delegate void CandidatesSearchProgressEventHandler(object sender, CandidatesSearchProgressEventArgs eventArgs);

    internal class DuplicateCandidates
    {
        public event CandidatesSearchProgressEventHandler CandidatesSearchProgress;
        public event FileSystemErrorEventHandler FileSystemError;

        public async Task<List<IComparableFile[]>> Find(IReadOnlyCollection<FileData> srcFiles, ICandidatePredicate duplicateCandidatePredicate, IComparableFileFactory comparableFileFactory, CancellationToken cancellationToken)
        {
            return await Task.Run(() => FindSync(srcFiles, duplicateCandidatePredicate, comparableFileFactory, cancellationToken), cancellationToken);
        }

        private List<IComparableFile[]> FindSync(
            IReadOnlyCollection<FileData> srcFiles,
            ICandidatePredicate duplicateCandidatePredicate,
            IComparableFileFactory comparableFileFactory,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            var duplicateCandidates = new List<IComparableFile[]>(256);

            var fileIndex = 0;
            var filesCount = srcFiles.Count;
            var candidatesTotalSize = 0L;
            var candidateFilesCount = 0;
            foreach (var currentFile in srcFiles)
            {
                try
                {
                    bool IsCurrentFileAlreadyAdded(IComparableFile[] candidatesSet)
                    {
                        foreach (var candidateFile in candidatesSet)
                        {
                            if (ReferenceEquals(candidateFile.FileData, currentFile)) 
                                return true;
                        }

                        return false;
                    }

                    if (duplicateCandidates.AsParallel().Any(IsCurrentFileAlreadyAdded))
                        continue;

                    var candidates = srcFiles
                        .AsParallel()
                        .WithCancellation(cancellationToken)
                        .Where(file => duplicateCandidatePredicate.IsCandidate(file, currentFile))
                        .ToArray();

                    var candidatesLength = candidates.Length;
                    if (candidatesLength < 2)
                        continue;

                    var candidatesGroup = new IComparableFile[candidatesLength];
                    for (var index = 0; index < candidatesLength; index++)
                    {
                        var candidateFileData = candidates[index];
                        candidatesGroup[index] = comparableFileFactory.Create(candidateFileData);
                        candidatesTotalSize += candidateFileData.Size;
                    }

                    candidateFilesCount += candidatesLength;
                    duplicateCandidates.Add(candidatesGroup);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    OnFileSystemError(currentFile.FullName, ex);
                }
                finally
                {
                    OnScanningPath(currentFile.FullName, fileIndex++, filesCount, duplicateCandidates.Count, candidateFilesCount, candidatesTotalSize);
                }
            }

            stopwatch.Stop();
            var elapsed = TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds);
            System.Windows.MessageBox.Show($"Candidates search time: {elapsed:c}");

            return duplicateCandidates;
        }

        protected virtual void OnScanningPath(string filePath, int currentFileIndex, int totalFilesCount, int candidateGroupsFound, int candidateFilesFound, long candidatesTotalSize)
        {
            CandidatesSearchProgress?.Invoke(this, new CandidatesSearchProgressEventArgs(filePath, currentFileIndex, totalFilesCount, candidateGroupsFound, candidateFilesFound, candidatesTotalSize));
        }

        protected virtual void OnFileSystemError(string path, Exception ex)
        {
            FileSystemError?.Invoke(this, new FileSystemErrorEventArgs(path, ex.Message, ex));
        }
    }
}
