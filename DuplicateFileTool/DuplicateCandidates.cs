namespace DuplicateFileTool;

internal sealed class CandidatesSearchProgressEventArgs(string filePath, int currentFileIndex, int totalFilesCount, int candidateGroupsCount, int candidateFilesCount, long candidatesTotalSize) : EventArgs
{
    public string FilePath { get; } = filePath;
    public int CurrentFileIndex { get; } = currentFileIndex;
    public int TotalFilesCount { get; } = totalFilesCount;
    public int CandidateGroupsCount { get; } = candidateGroupsCount;
    public int CandidateFilesCount { get; } = candidateFilesCount;
    public long CandidatesTotalSize { get; set; } = candidatesTotalSize;
}

internal delegate void CandidatesSearchProgressEventHandler(object? sender, CandidatesSearchProgressEventArgs eventArgs);

internal sealed class DuplicateCandidates
{
    public event CandidatesSearchProgressEventHandler? CandidatesSearchProgress;
    public event FileSystemErrorEventHandler? FileSystemError;

    public async Task<List<IComparableFile[]>> Find(
        IReadOnlyCollection<FileData> srcFiles,
        ICandidatePredicate duplicateCandidatePredicate,
        IComparableFileFactory comparableFileFactory,
        CancellationToken ctx)
    {
        var duplicateCandidates = new List<IComparableFile[]>(256);
        

        var fileIndex = 0;
        var filesCount = srcFiles.Count;
        var candidatesTotalSize = 0L;
        var candidateFilesCount = 0;

        foreach (var currentFile in srcFiles)
        {
            try
            {
                #region Check if the current file is already added
                var currentFileFoundFlag = 0;
                await Parallel.ForEachAsync(duplicateCandidates, ctx, (candidatesSet, token) =>
                {
                    foreach (var candidateFile in candidatesSet)
                    {
                        if (token.IsCancellationRequested || Volatile.Read(ref currentFileFoundFlag) == 1)
                            return ValueTask.CompletedTask;

                        if (!ReferenceEquals(candidateFile.FileData, currentFile))
                            continue;

                        Interlocked.Exchange(ref currentFileFoundFlag, 1);
                        break;
                    }

                    return ValueTask.CompletedTask;
                });

                if (currentFileFoundFlag == 1)
                    continue;
                #endregion

                var candidates = srcFiles
                    .AsParallel()
                    .WithCancellation(ctx)
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

        return duplicateCandidates;
    }

    private void OnScanningPath(string filePath, int currentFileIndex, int totalFilesCount, int candidateGroupsFound, int candidateFilesFound, long candidatesTotalSize) => 
        CandidatesSearchProgress?.Invoke(this, new CandidatesSearchProgressEventArgs(filePath, currentFileIndex, totalFilesCount, candidateGroupsFound, candidateFilesFound, candidatesTotalSize));

    private void OnFileSystemError(string path, Exception ex) => 
        FileSystemError?.Invoke(this, new FileSystemErrorEventArgs(path, ex.Message, ex));
}