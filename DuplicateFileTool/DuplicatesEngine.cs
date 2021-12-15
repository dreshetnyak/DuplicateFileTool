using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DuplicateFileTool.Converters;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool
{
    [DebuggerDisplay("{FileData.FullName,nq}")]
    internal class DuplicateFile : NotifyPropertyChanged
    {
        private bool _isMarkedForDeletion;
        private bool _isSelected;

        public static event EventHandler ItemSelected;

        private DuplicateGroup ParentGroup { get; }
        public FileData FileData { get; }
        public int MatchValue { get; }
        public int CompleteMatch { get; }
        public int CompleteMismatch { get; }

        public string FileFullName => FileData.FullName;
        public string FileSize => FileData.Size.BytesLengthToString();
        public bool IsMarkForDeletionVisible => !IsMarkedForDeletion && ParentGroup.DuplicateFiles.Count(file => !file.IsMarkedForDeletion) > 1;
        public bool IsMarkedForDeletion
        {
            get => _isMarkedForDeletion;
            set
            {
                if (_isMarkedForDeletion == value)
                    return;
                _isMarkedForDeletion = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsMarkForDeletionVisible));

                foreach (var duplicatedFile in ParentGroup.DuplicateFiles)
                {
                    if (duplicatedFile == this)
                        continue;
                    duplicatedFile.OnPropertyChanged();
                    duplicatedFile.OnPropertyChanged(nameof(IsMarkForDeletionVisible));
                }
            }
        }
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;
                _isSelected = value;
                ItemSelected?.Invoke(this, EventArgs.Empty);
            }
        }

        public DuplicateFile(DuplicateGroup parentGroup, MatchResult matchResult)
        {
            ParentGroup = parentGroup;
            FileData = matchResult.ComparableFile.FileData;
            MatchValue = matchResult.MatchValue;
            CompleteMatch = matchResult.CompleteMatch;
            CompleteMismatch = matchResult.CompleteMismatch;
        }
    }

    [DebuggerDisplay("Group: {GroupNumber,nq}, Files: {FilesCount,nq}, Duplicated: {DuplicatedSizeText,nq}")]
    internal class DuplicateGroup : NotifyPropertyChanged
    {
        private int _groupNumber;
        private int _filesCount;
        private long _duplicatedSize;
        private string _duplicatedSizeText;
        private bool _isSelected;

        public int GroupNumber
        {
            get => _groupNumber;
            set
            {
                if (_groupNumber == value)
                    return;
                _groupNumber = value;
                OnPropertyChanged();
            }
        }
        public int FilesCount
        {
            get => _filesCount;
            set
            {
                if (_filesCount == value)
                    return;
                _filesCount = value; 
                OnPropertyChanged();
            }
        }
        public long DuplicatedSize
        {
            get => _duplicatedSize;
            set
            {
                if (_duplicatedSize == value)
                    return;
                _duplicatedSize = value;
                OnPropertyChanged();
            }
        }
        public string DuplicatedSizeText
        {
            get => _duplicatedSizeText;
            set
            {
                if (_duplicatedSizeText == value)
                    return;
                _duplicatedSizeText = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<DuplicateFile> DuplicateFiles { get; }

        public DuplicateGroup(IEnumerable<MatchResult> duplicateFiles)
        {
            DuplicateFiles = new ObservableCollection<DuplicateFile>();
            foreach (var duplicateFile in duplicateFiles.OrderBy(df => df.ComparableFile.FileData.FullName))
                DuplicateFiles.Add(new DuplicateFile(this, duplicateFile));
            OnDuplicateFilesCollectionChanged(this);
            DuplicateFiles.CollectionChanged += OnDuplicateFilesCollectionChanged;
        }

        private void OnDuplicateFilesCollectionChanged(object sender, NotifyCollectionChangedEventArgs eventArgs = null)
        {
            FilesCount = DuplicateFiles.Count;
            var duplicatedSize = GetDuplicatedSize();
            DuplicatedSize = duplicatedSize;
            DuplicatedSizeText = duplicatedSize.BytesLengthToString();
        }

        private long GetDuplicatedSize()
        {
            var size = 0L;
            var matchExcluded = false;
            foreach (var duplicateFile in DuplicateFiles)
            {
                if (!matchExcluded && duplicateFile.MatchValue == duplicateFile.CompleteMatch)
                    matchExcluded = true;
                else
                    size += duplicateFile.FileData.Size;
            }

            return size;
        }
    }

    [Localizable(true)]
    internal class DuplicatesEngine : NotifyPropertyChanged
    {
        #region Backing Fields
        private int _includedFilesCount;
        private int _progressPercentage;
        private string _progressText;
        private int _currentFileIndex;
        private int _totalFilesCount;
        private int _candidateGroupsCount;
        private int _candidateFilesCount;
        private long _candidatesTotalSize;
        private int _duplicateGroupsCount;
        private int _duplicateFilesCount;
        private long _duplicatedTotalSize;
        private long _toBeDeletedSize;
        private long _toBeDeletedCount;

        #endregion

        private FilesSearch Files { get; } = new();
        private DuplicateCandidates Candidates { get; } = new();
        private DuplicatesSearch Duplicates { get; } = new();
        private DuplicatesRemover DuplicatesRemover { get; } = new();

        public int IncludedFilesCount
        {
            get => _includedFilesCount;
            private set
            {
                _includedFilesCount = value; 
                OnPropertyChanged();
            }
        }
        public int ProgressPercentage //0 - 10000
        {
            get => _progressPercentage;
            private set
            {
                _progressPercentage = value; 
                OnPropertyChanged();
            }
        }
        public string ProgressText
        {
            get => _progressText;
            set
            {
                _progressText = value;
                OnPropertyChanged();
            }
        }
        public int CurrentFileIndex
        {
            get => _currentFileIndex;
            private set
            {
                _currentFileIndex = value; 
                OnPropertyChanged();
            }
        }
        public int TotalFilesCount
        {
            get => _totalFilesCount;
            private set
            {
                _totalFilesCount = value; 
                OnPropertyChanged();
            }
        }

        public int CandidateGroupsCount
        {
            get => _candidateGroupsCount;
            private set
            {
                _candidateGroupsCount = value; 
                OnPropertyChanged();
            }
        }
        public int CandidateFilesCount
        {
            get => _candidateFilesCount;
            private set
            {
                _candidateFilesCount = value;
                OnPropertyChanged();
            }
        }
        public long CandidatesTotalSize
        {
            get => _candidatesTotalSize;
            private set
            {
                _candidatesTotalSize = value; 
                OnPropertyChanged();
            }
        }

        public int DuplicateGroupsCount
        {
            get => _duplicateGroupsCount;
            private set
            {
                _duplicateGroupsCount = value; 
                OnPropertyChanged();
            }
        }
        public int DuplicateFilesCount
        {
            get => _duplicateFilesCount;
            private set
            {
                _duplicateFilesCount = value; 
                OnPropertyChanged();
            }
        }
        public long DuplicatedTotalSize
        {
            get => _duplicatedTotalSize;
            private set
            {
                _duplicatedTotalSize = value; 
                OnPropertyChanged();
            }
        }
        public long ToBeDeletedSize
        {
            get => _toBeDeletedSize;
            set
            {
                _toBeDeletedSize = value;
                OnPropertyChanged();
            }
        }
        public long ToBeDeletedCount
        {
            get => _toBeDeletedCount;
            set
            {
                _toBeDeletedCount = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ErrorMessage> Errors { get; } = new();
        public ObservableCollection<DuplicateGroup> DuplicateGroups { get; } = new();

        public DuplicatesEngine()
        {
            Files.FilesSearchProgress += OnFilesSearchProgress;
            Files.FileSystemError += OnFileSystemError;
            
            Candidates.CandidatesSearchProgress += OnCandidatesSearchProgress;
            Candidates.FileSystemError += OnFileSystemError;

            Duplicates.DuplicatesSearchProgress += OnDuplicatesSearchProgress;
            Duplicates.FileSystemError += OnFileSystemError;
            Duplicates.DuplicatesGroupFound += (_, args) => Application.Current?.Dispatcher.Invoke(() => DuplicateGroups.Add(new DuplicateGroup(args.DuplicatesGroup) { GroupNumber = DuplicateGroups.Count + 1 }));

            DuplicateGroups.CollectionChanged += OnDuplicateGroupsCollectionChanged;
            
            DuplicatesRemover.DeletionMessage += OnDeletionMessage;
            DuplicatesRemover.DeletionStateChanged += DeletionStateChanged;
        }

        #region Finding Duplicates

        public async Task FindDuplicates(
            IReadOnlyCollection<SearchPath> searchPaths,
            IInclusionPredicate<FileData> inclusionPredicate,
            ICandidatePredicate duplicateCandidatePredicate,
            IComparableFileFactory comparableFileFactory,
            CancellationToken cancellationToken)
        {
            Clear();

            List<IComparableFile[]> duplicateCandidates = null;
            try
            {
                var files = await Files.Find(searchPaths, inclusionPredicate, cancellationToken);
                duplicateCandidates = await Candidates.Find(files, duplicateCandidatePredicate, comparableFileFactory, cancellationToken);
                await Duplicates.Find(duplicateCandidates, comparableFileFactory.Config, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                ProgressText = Resources.Ui_Progress_Duplicates_Search_Cancelled;
                ProgressPercentage = 0;
                return;
            }
            finally
            {
                if (duplicateCandidates != null)
                    DisposeComparableFiles(duplicateCandidates);
                GC.Collect();
            }

            ProgressText = Resources.Ui_Progress_Duplicates_Search_Done;
            ProgressPercentage = 0;
        }

        public void Clear()
        {
            Errors.Clear();
            DuplicateGroups.Clear();
            IncludedFilesCount = 0;
            CurrentFileIndex = 0;
            TotalFilesCount = 0;
            CandidateGroupsCount = 0;
            CandidateFilesCount = 0;
            CandidatesTotalSize = 0;
            DuplicateGroupsCount = 0;
            DuplicateFilesCount = 0;
            DuplicatedTotalSize = 0;
            ToBeDeletedSize = 0;
            ToBeDeletedCount = 0;
            ProgressText = "";
            ProgressPercentage = 0;
            GC.Collect();
        }

        private static void DisposeComparableFiles(IEnumerable<IComparableFile[]> fileGroups)
        {
            foreach (var fileGroup in fileGroups)
            {
                foreach (var file in fileGroup)
                {
                    if (file is not IDisposable disposableFile)
                        return;
                    disposableFile.Dispose();
                }
            }
        }

        private void OnFilesSearchProgress(object sender, FilesSearchProgressEventArgs eventArgs)
        {
            if (eventArgs.IsDirectory) 
                ProgressText = Resources.Ui_Progress_Scanning + eventArgs.Path;
            IncludedFilesCount = eventArgs.FoundFilesCount;
        }

        private void OnCandidatesSearchProgress(object sender, CandidatesSearchProgressEventArgs eventArgs)
        {
            ProgressText = Resources.Ui_Progress_Analyzing + eventArgs.FilePath;
            UpdateProgressStats(eventArgs.TotalFilesCount, eventArgs.CurrentFileIndex);
            CandidateGroupsCount = eventArgs.CandidateGroupsCount;
            CandidateFilesCount = eventArgs.CandidateFilesCount;
            CandidatesTotalSize = eventArgs.CandidatesTotalSize;
        }

        private void OnDuplicatesSearchProgress(object sender, DuplicatesSearchProgressEventArgs eventArgs)
        {
            var filePath = eventArgs.FilePath;
            if (filePath != null)
                ProgressText = Resources.Ui_Progress_Comparing + filePath;
            if (!eventArgs.HasStats) 
                return;
            UpdateProgressStats(eventArgs.TotalFilesCount, eventArgs.CurrentFileIndex);
            DuplicateFilesCount = eventArgs.DuplicateFilesCount;
            DuplicatedTotalSize = eventArgs.DuplicatedTotalSize;
        }

        private void UpdateProgressStats(int totalFilesCount, int currentFileIndex)
        {
            TotalFilesCount = totalFilesCount;
            CurrentFileIndex = currentFileIndex;
            ProgressPercentage = (int)((double)currentFileIndex * 10000 / totalFilesCount);
        }

        private void OnFileSystemError(object sender, FileSystemErrorEventArgs eventArgs)
        {
            var fileSystemError = eventArgs.FileSystemError;
            var path = fileSystemError.Path;
            var message = fileSystemError.Message;
            Application.Current.Dispatcher.Invoke(() => Errors.Add(new ErrorMessage(path, message, MessageType.Error)));
        }

        private void OnDuplicateGroupsCollectionChanged(object sender, NotifyCollectionChangedEventArgs eventArgs)
        {
            DuplicateGroupsCount = DuplicateGroups.Count;
        }

        #endregion

        #region Removing Duplicates

        public async Task RemoveDuplicates(ObservableCollection<DuplicateGroup> duplicates, bool removeEmptyDirs, bool deleteToRecycleBin, CancellationToken cancellationToken)
        {
            await DuplicatesRemover.RemoveDuplicates(duplicates, removeEmptyDirs, deleteToRecycleBin, cancellationToken);
            ProgressPercentage = 0;
        }

        private void OnDeletionMessage(object sender, DeletionMessageEventArgs eventArgs)
        {
            var deletionMessage = eventArgs.Message;
            ProgressText = deletionMessage.Text;
            var deletionMessageType = deletionMessage.Type;
            if (deletionMessageType is MessageType.Error or MessageType.Warning)
                Application.Current.Dispatcher.Invoke(() => Errors.Add(new ErrorMessage(deletionMessage.Path, deletionMessage.Text, deletionMessageType)));
        }

        private void DeletionStateChanged(object sender, DeletionStateEventArgs eventArgs)
        {
            var deletionState = eventArgs.State;
            var total = (double)deletionState.TotalFilesForDeletionCount;
            var current = (double)deletionState.CurrentFileForDeletionIndex;
            ProgressPercentage = (int)(current * 10000 / total);
            var deletedSizeDelta = deletionState.DeletedSizeDelta;
            ToBeDeletedSize += deletedSizeDelta;
            DuplicatedTotalSize += deletedSizeDelta;
            ToBeDeletedCount += deletionState.DeletedCountDelta;
        }

        #endregion
    }
}
