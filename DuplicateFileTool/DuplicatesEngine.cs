using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DuplicateFileTool.Converters;

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

    internal class DuplicatesEngine : NotifyPropertyChanged
    {
        internal enum SearchStep { StandBy, SearchingFiles, SearchingCandidates, SearchingDuplicates, Done }

        #region Backing Fields
        private SearchStep _currentStep;
        private string _currentPath = "";
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

        #endregion

        private FilesSearch Files { get; } = new();
        private DuplicateCandidates Candidates { get; } = new();
        private DuplicatesSearch Duplicates { get; } = new();
        private DuplicatesRemover DuplicatesRemover { get; } = new();

        public SearchStep CurrentStep
        {
            get => _currentStep;
            private set
            {
                _currentStep = value; 
                OnPropertyChanged();
            }
        }
        public string CurrentPath
        {
            get => _currentPath;
            private set
            {
                _currentPath = value; 
                OnPropertyChanged();
            }
        }
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

        public ObservableCollection<ErrorMessage> Errors { get; } = new();
        //public ObservableCollection<FileSystemError> FileSystemErrors { get; } = new();
        public ObservableCollection<DuplicateGroup> DuplicateGroups { get; } = new();

        public DuplicatesEngine()
        {
            Files.FilesSearchProgress += OnFilesSearchProgress;
            Files.FileSystemError += OnFileSystemError;
            
            Candidates.CandidatesSearchProgress += OnCandidatesSearchProgress;

            Duplicates.DuplicatesSearchProgress += OnDuplicatesSearchProgress;
            Duplicates.FileSystemError += OnFileSystemError;
            Duplicates.DuplicatesGroupFound += (_, args) => Application.Current.Dispatcher.Invoke(() => DuplicateGroups.Add(new DuplicateGroup(args.DuplicatesGroup)));

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

            CurrentStep = SearchStep.SearchingFiles;
            var files = await Files.Find(searchPaths, inclusionPredicate, cancellationToken);

            CurrentStep = SearchStep.SearchingCandidates;
            var duplicateCandidates = await Candidates.Find(files, duplicateCandidatePredicate, comparableFileFactory, cancellationToken);

            CurrentStep = SearchStep.SearchingDuplicates;
            await Duplicates.Find(duplicateCandidates, comparableFileFactory.Config, cancellationToken);

            CurrentStep = SearchStep.Done;
            CurrentStep = SearchStep.StandBy;
            ProgressPercentage = 0;
        }

        private void OnFilesSearchProgress(object sender, FilesSearchProgressEventArgs eventArgs)
        {
            CurrentPath = eventArgs.DirPath;
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
            DuplicateGroupsCount = eventArgs.DuplicateGroupsCount;
            DuplicateFilesCount = eventArgs.DuplicateFilesCount;
            DuplicatedTotalSize = eventArgs.DuplicatedTotalSize;
        }

        private void UpdateProgress(string currentPath, int totalFilesCount, int currentFileIndex)
        {
            CurrentPath = currentPath;
            TotalFilesCount = totalFilesCount;
            CurrentFileIndex = currentFileIndex;
            ProgressPercentage = (int)((double)currentFileIndex * 10000 / totalFilesCount);
        }

        private void OnFileSystemError(object sender, FileSystemErrorEventArgs eventArgs)
        {
            var fileSystemError = eventArgs.FileSystemError;
            Errors.Add(new ErrorMessage(fileSystemError.Path, fileSystemError.Message, MessageType.Error));
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
        }

        #endregion
    }
}
