using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;

namespace DuplicateFileTool;

#region DuplicatesSearchProgress Implementation

internal sealed class DuplicatesSearchProgressEventArgs : EventArgs
{
    public string FilePath { get; }
    public bool HasStats { get; }
    public int CurrentFileIndex { get; }
    public int TotalFilesCount { get; }
    public int DuplicateFilesCount { get; }
    public long DuplicatedTotalSize { get; set; }

    public DuplicatesSearchProgressEventArgs(string filePath, SearchContext? context)
    {
        FilePath = filePath;
        if (context != null)
            HasStats = true;
        else
            return;
        CurrentFileIndex = Volatile.Read(ref context.CurrentFileIndex);
        TotalFilesCount = context.TotalFilesCount;
        DuplicateFilesCount = Volatile.Read(ref context.DuplicateFilesCount);
        DuplicatedTotalSize = Interlocked.Read(ref context.DuplicatedTotalSize);
    }
}

internal delegate void DuplicatesSearchProgressEventHandler(object? sender, DuplicatesSearchProgressEventArgs eventArgs);

#endregion

#region DuplicatesGroupFound Implementation

internal sealed class DuplicatesGroupFoundEventArgs(List<MatchResult> duplicatesGroup) : EventArgs
{
    public List<MatchResult> DuplicatesGroup { get; } = duplicatesGroup;
}

internal delegate void DuplicatesGroupFoundEventHandler(object? sender, DuplicatesGroupFoundEventArgs eventArgs);

#endregion

internal sealed class MatchResult(IComparableFile comparableFile)
{
    public IComparableFile ComparableFile { get; set; } = comparableFile;
    public int MatchValue { get; set; }
    public int CompleteMatch { get; set; }
    public int CompleteMismatch { get; set; }
}

internal sealed class SearchContext(IFileComparerConfig comparerConfig)
{
    public IFileComparerConfig ComparerConfig { get; set; } = comparerConfig;
    public int TotalFilesCount { get; set; }

    //The counters are fields because they are updated with the Interlocked operations from the drive lanes running in parallel
    public int CurrentFileIndex;
    public int DuplicateGroupsCount;
    public int DuplicateFilesCount;
    public long DuplicatedTotalSize;
}

internal sealed class DuplicatesSearch
{
    private const int SSD_LANE_WIDTH = 4;

    public event DuplicatesGroupFoundEventHandler? DuplicatesGroupFound;
    public event DuplicatesSearchProgressEventHandler? DuplicatesSearchProgress;
    public event FileSystemErrorEventHandler? FileSystemError;

    public Task Find(IReadOnlyCollection<IComparableFile[]> duplicateCandidates, IFileComparerConfig comparerConfig, CancellationToken cancellationToken) =>
        GetDuplicatesFromCandidates(duplicateCandidates, comparerConfig, cancellationToken);

    // Reading several streams at a time from one spinning drive makes its heads thrash, while an SSD
    // serves concurrent reads without penalty. Every physical drive therefore gets a lane: the drives
    // with a seek penalty (and the drives where it cannot be determined) admit one candidate group at
    // a time, SSDs admit several. A group spanning multiple drives holds a slot in each of its lanes
    // while it is being compared. The candidate groups are partitioned by the set of lanes they touch,
    // every partition gets as many workers as its narrowest lane admits, and since each partition
    // drains its own queue the groups of one drive can never starve the groups of another.
    private async Task GetDuplicatesFromCandidates(IReadOnlyCollection<IComparableFile[]> duplicateCandidates, IFileComparerConfig comparerConfig, CancellationToken cancellationToken)
    {
        var context = new SearchContext(comparerConfig) { TotalFilesCount = duplicateCandidates.AsParallel().Sum(group => group.Length) };

        var driveLanes = new DriveLanes();
        var lanePartitions = duplicateCandidates
            .Select(candidateGroup => (CandidateGroup: candidateGroup, Lanes: driveLanes.GetOrderedLanes(candidateGroup)))
            .GroupBy(scheduledGroup => string.Join(",", scheduledGroup.Lanes.Select(lane => lane.Order)));

        var workerTasks = new List<Task>();
        foreach (var lanePartition in lanePartitions)
        {
            var scheduledGroups = lanePartition.ToList();
            var partitionQueue = new ConcurrentQueue<IComparableFile[]>(scheduledGroups.Select(scheduledGroup => scheduledGroup.CandidateGroup));
            var partitionLanes = scheduledGroups[0].Lanes;
            var workersCount = Math.Min(partitionLanes.Min(lane => lane.Width), scheduledGroups.Count);

            for (var workerIndex = 0; workerIndex < workersCount; workerIndex++)
            {
                workerTasks.Add(Task.Run(async () =>
                {
                    while (partitionQueue.TryDequeue(out var candidateGroup))
                    {
                        //The lanes are ordered by creation, taking the slots in that fixed order is what makes the groups spanning the same drives unable to deadlock
                        var acquiredCount = 0;
                        try
                        {
                            foreach (var lane in partitionLanes)
                            {
                                await lane.Slots.WaitAsync(cancellationToken);
                                acquiredCount++;
                            }

                            FindGroupDuplicates(candidateGroup, context, cancellationToken);
                        }
                        finally
                        {
                            for (var laneIndex = 0; laneIndex < acquiredCount; laneIndex++)
                                partitionLanes[laneIndex].Slots.Release();
                        }
                    }
                }, cancellationToken));
            }
        }

        await Task.WhenAll(workerTasks);
    }

    private void FindGroupDuplicates(IComparableFile[] duplicateCandidateGroup, SearchContext context, CancellationToken cancellationToken)
    {
        var groupDuplicates = GetDuplicatesFromGroup(duplicateCandidateGroup, context, cancellationToken);
        if (groupDuplicates.Count == 0)
            return;

        var (duplicatesCount, duplicatesSize) = GetGroupDuplicatedCountAndSize(groupDuplicates);
        Interlocked.Add(ref context.DuplicateGroupsCount, groupDuplicates.Count);
        Interlocked.Add(ref context.DuplicateFilesCount, duplicatesCount);
        Interlocked.Add(ref context.DuplicatedTotalSize, duplicatesSize);
        DuplicatesSearchProgress?.Invoke(this, new DuplicatesSearchProgressEventArgs("", context));

        foreach (var group in groupDuplicates)
            DuplicatesGroupFound?.Invoke(this, new DuplicatesGroupFoundEventArgs(group));
    }

    private static (int duplicatesCount, long duplicatesSize) GetGroupDuplicatedCountAndSize(IEnumerable<List<MatchResult>> groupDuplicates)
    {
        var count = 0;
        var size = 0L;
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var duplicateGroup in groupDuplicates)
        {
            var sizeSum = 0L;
            var smallestFileSize = long.MaxValue;
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (var item in duplicateGroup)
            {
                var itemFileSize = item.ComparableFile.FileData.Size;
                sizeSum += itemFileSize;
                if (itemFileSize < smallestFileSize)
                    smallestFileSize = itemFileSize;
            }

            Debug.Assert(smallestFileSize != long.MaxValue, "Invalid data, the duplicate group contains no items");

            count += duplicateGroup.Count;
            size += sizeSum - smallestFileSize;
        }

        return (count, size);
    }

    private List<List<MatchResult>> GetDuplicatesFromGroup(IReadOnlyCollection<IComparableFile> fileGroup, SearchContext context, CancellationToken cancellationToken)
    {
        var duplicates = new List<List<MatchResult>>();
        var alreadyMatched = new HashSet<IComparableFile>(ReferenceEqualityComparer.Instance);
        foreach (var fileFromGroup in fileGroup)
        {
            try
            {
                if (alreadyMatched.Contains(fileFromGroup))
                    continue;

                DuplicatesSearchProgress?.Invoke(this, new DuplicatesSearchProgressEventArgs(fileFromGroup.FileData.FullName, null));

                var fileFromGroupDuplicates = GetFileDuplicates(fileFromGroup, fileGroup, context, cancellationToken);
                if (fileFromGroupDuplicates.Count == 0)
                    continue;

                duplicates.Add(fileFromGroupDuplicates);
                foreach (var matchResult in fileFromGroupDuplicates)
                    alreadyMatched.Add(matchResult.ComparableFile);
            }
            finally
            { Interlocked.Increment(ref context.CurrentFileIndex); }
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
                duplicates.Add(new MatchResult(fileToFind) { MatchValue = completeMatch, CompleteMatch = completeMatch, CompleteMismatch = completeMismatch });
            duplicates.Add(new MatchResult(fileFromGroup) { MatchValue = matchValue, CompleteMatch = completeMatch, CompleteMismatch = completeMismatch });
        }

        return duplicates;
    }

    private void OnFileSystemError(string path, string message, Exception? exception = null) =>
        FileSystemError?.Invoke(this, new FileSystemErrorEventArgs(path, message, exception));

    #region Drive Lanes Implementation

    private sealed class DriveLanes
    {
        internal sealed record DriveLane(int Order, int Width, SemaphoreSlim Slots);

        private readonly Dictionary<string, DriveLane> _laneByRoot = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, DriveLane> _laneByPhysicalDrive = [];
        private int _lanesCreated;

        //The distinct lanes of the group ordered by lane creation. Not thread-safe, the lanes are resolved up front before the parallel phase starts.
        public DriveLane[] GetOrderedLanes(IComparableFile[] candidateGroup) =>
            candidateGroup
                .Select(file => GetLane(file.FileData.FullName))
                .DistinctBy(lane => lane.Order)
                .OrderBy(lane => lane.Order)
                .ToArray();

        private DriveLane GetLane(string filePath)
        {
            var rootPath = GetRootPath(filePath);
            if (_laneByRoot.TryGetValue(rootPath, out var lane))
                return lane;

            //Different drive letters can be partitions of one physical disk and must share a lane.
            //Roots with no resolvable physical drive (network shares and the like) each get a lane of their own.
            var physicalDriveNumber = rootPath.Length >= 2 && rootPath[1] == ':'
                ? Drives.GetPhysicalDriveNumber(rootPath)
                : DriveData.PHYSICAL_DRIVE_NUMBER_UNKNOWN;

            if (physicalDriveNumber != DriveData.PHYSICAL_DRIVE_NUMBER_UNKNOWN && _laneByPhysicalDrive.TryGetValue(physicalDriveNumber, out lane))
            {
                _laneByRoot.Add(rootPath, lane);
                return lane;
            }

            var laneWidth = physicalDriveNumber != DriveData.PHYSICAL_DRIVE_NUMBER_UNKNOWN && Drives.GetIncursSeekPenalty(physicalDriveNumber) == false
                ? SSD_LANE_WIDTH
                : 1;

            lane = new DriveLane(_lanesCreated++, laneWidth, new SemaphoreSlim(laneWidth, laneWidth));
            _laneByRoot.Add(rootPath, lane);
            if (physicalDriveNumber != DriveData.PHYSICAL_DRIVE_NUMBER_UNKNOWN)
                _laneByPhysicalDrive.Add(physicalDriveNumber, lane);
            return lane;
        }

        private static string GetRootPath(string path)
        {
            if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
                path = @"\\" + path[8..];
            else if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
                path = path[4..];
            return Path.GetPathRoot(path) ?? path;
        }
    }

    #endregion
}
