using System;
using System.Collections.Generic;
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
            var duplicateCandidates = new List<IComparableFile[]>();

            var fileIndex = 0;
            var filesCount = srcFiles.Count;
            var candidatesTotalSize = 0L;
            var candidateFilesCount = 0;
            foreach (var currentFile in srcFiles)
            {
                try
                {
                    if (duplicateCandidates.AsParallel().Any(candidatesSet => candidatesSet.Any(candidateFile => ReferenceEquals(candidateFile.FileData, currentFile))))
                        continue; //File already in the list

                    var candidates = srcFiles.AsParallel().WithCancellation(cancellationToken).Where(file => duplicateCandidatePredicate.IsCandidate(file, currentFile)).ToArray();
                    if (candidates.Length < 2)
                        continue;
                    
                    var candidatesGroup = candidates.Select(comparableFileFactory.Create).ToArray();
                    candidateFilesCount += candidatesGroup.Length;
                    candidatesTotalSize += candidatesGroup.Sum(candidate => candidate.FileData.Size);
                    duplicateCandidates.Add(candidatesGroup);
                }
                finally
                {
                    OnScanningPath(currentFile.FullName, fileIndex++, filesCount, duplicateCandidates.Count, candidateFilesCount, candidatesTotalSize);
                }
            }

            return duplicateCandidates;
        }

        protected virtual void OnScanningPath(string filePath, int currentFileIndex, int totalFilesCount, int candidateGroupsFound, int candidateFilesFound, long candidatesTotalSize)
        {
            CandidatesSearchProgress?.Invoke(this, new CandidatesSearchProgressEventArgs(filePath, currentFileIndex, totalFilesCount, candidateGroupsFound, candidateFilesFound, candidatesTotalSize));
        }
    }
}
