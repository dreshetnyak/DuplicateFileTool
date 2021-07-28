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

        public DuplicateFile(DuplicateGroup parentGroup, MatchResult matchResult)
        {
            ParentGroup = parentGroup;
            FileData = matchResult.ComparableFile.FileData;
            MatchValue = matchResult.MatchValue;
            CompleteMatch = matchResult.CompleteMatch;
            CompleteMismatch = matchResult.CompleteMismatch;
        }
    }

    [DebuggerDisplay("Group: {GroupNumber,nq}, Files: {FilesCount,nq}, Duplicated: {DuplicatedSize,nq}")]
    internal class DuplicateGroup : NotifyPropertyChanged
    {
        private int _groupNumber;
        private int _filesCount;
        private string _duplicatedSize;

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
        public string DuplicatedSize
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

        public ObservableCollection<DuplicateFile> DuplicateFiles { get; }

        public DuplicateGroup(IEnumerable<MatchResult> duplicateFiles)
        {
            DuplicateFiles = new ObservableCollection<DuplicateFile>();
            foreach (var duplicateFile in duplicateFiles)
                DuplicateFiles.Add(new DuplicateFile(this, duplicateFile));
            OnDuplicateFilesCollectionChanged(this);
            DuplicateFiles.CollectionChanged += OnDuplicateFilesCollectionChanged;
        }

        private void OnDuplicateFilesCollectionChanged(object sender, NotifyCollectionChangedEventArgs eventArgs = null)
        {
            FilesCount = DuplicateFiles.Count;
            DuplicatedSize = GetDuplicatedSize().BytesLengthToString();
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
            Duplicates.DuplicatesGroupFound += (_, args) => Application.Current.Dispatcher.Invoke(() => DuplicateGroups.Add(new DuplicateGroup(args.DuplicatesGroup)));

            DuplicateGroups.CollectionChanged += OnDuplicateGroupsCollectionChanged;
            
            DuplicatesRemover.DeletionMessage += OnDeletionMessage;
            DuplicatesRemover.DeletionStateChanged += DeletionStateChanged;
        }

        #region Finding Duplicates

        public async Task FindDuplicates(
            IReadOnlyCollection<SearchPath> searchPaths,
            IInclusionPredicate inclusionPredicate,
            ICandidatePredicate duplicateCandidatePredicate,
            IComparableFileFactory comparableFileFactory,
            CancellationToken cancellationToken)
        {
            DuplicateGroups.Clear();

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
            UpdateProgress(eventArgs.FilePath, eventArgs.TotalFilesCount, eventArgs.CurrentFileIndex);
            CandidateGroupsCount = eventArgs.CandidateGroupsCount;
            CandidateFilesCount = eventArgs.CandidateFilesCount;
            CandidatesTotalSize = eventArgs.CandidatesTotalSize;
        }

        private void OnDuplicatesSearchProgress(object sender, DuplicatesSearchProgressEventArgs eventArgs)
        {
            UpdateProgress(eventArgs.FilePath, eventArgs.TotalFilesCount, eventArgs.CurrentFileIndex);
            DuplicateFilesCount = eventArgs.DuplicateFilesCount;
            DuplicatedTotalSize = eventArgs.DuplicatedTotalSize;
        }

        private void UpdateProgress(string currentPath, int totalFilesCount, int currentFileIndex)
        {
            ProgressText = Resources.Ui_Progress_Comparing + currentPath;
            TotalFilesCount = totalFilesCount;
            CurrentFileIndex = currentFileIndex;
            ProgressPercentage = (int)((double)currentFileIndex * 10000 / totalFilesCount);
        }

        private void OnFileSystemError(object sender, FileSystemErrorEventArgs eventArgs)
        {
            var fileSystemError = eventArgs.FileSystemError;
            Errors.Add(new ErrorMessage(fileSystemError.Path, fileSystemError.Message, MessageType.Error));
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
                Errors.Add(new ErrorMessage(deletionMessage.Path, deletionMessage.Text, deletionMessageType));
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
        }

        #endregion
    }
}
