using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace FileBadger
{
    internal class DuplicatesEngine : NotifyPropertyChanged
    {
        internal enum SearchStep { StandBy, SearchingFiles, SearchingCandidates, SearchingDuplicates, Done }

        #region Backing Fields
        private SearchStep _currentStep;
        private string _currentPath = "";
        private int _includedFilesCount;
        private int _progressPercentage;
        private int _currentFileIndex;
        private int _totalFilesCount;
        private int _candidateGroupsCount;
        private int _candidateFilesCount;
        private long _candidatesTotalSize;
        private int _duplicateGroupsCount;
        private int _duplicateFilesCount;
        private long _duplicatedTotalSize;

        #endregion

        private FilesSearch Files { get; }
        private DuplicateCandidates Candidates { get; }
        private DuplicatesSearch Duplicates { get; }

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

        public ObservableCollection<FileSystemErrorEventArgs> FileSystemErrors { get; }
        
        public DuplicatesEngine()
        {
            FileSystemErrors = new ObservableCollection<FileSystemErrorEventArgs>();

            Files = new FilesSearch();
            Files.FilesSearchProgress += OnFilesSearchProgress;
            Files.FileSystemError += OnFileSystemError;

            Candidates = new DuplicateCandidates();
            Candidates.CandidatesSearchProgress += OnCandidatesSearchProgress;
            
            Duplicates = new DuplicatesSearch();
            Duplicates.DuplicatesSearchProgress += OnDuplicatesSearchProgress;
            Duplicates.FileSystemError += OnFileSystemError;
        }

        public async Task<List<List<MatchResult>>> FindDuplicates(
            IReadOnlyCollection<SearchPath> searchPaths, 
            IInclusionPredicate inclusionPredicate, 
            ICandidatePredicate duplicateCandidatePredicate,
            IComparableFileFactory comparableFileFactory,
            CancellationToken cancellationToken)
        {
            CurrentStep = SearchStep.SearchingFiles;
            var files = await Files.Find(searchPaths, inclusionPredicate, cancellationToken);

            CurrentStep = SearchStep.SearchingCandidates;
            var duplicateCandidates = await Candidates.Find(files, duplicateCandidatePredicate, comparableFileFactory, cancellationToken);

            CurrentStep = SearchStep.SearchingDuplicates;
            var duplicates = await Duplicates.Find(duplicateCandidates, comparableFileFactory.ComparerConfig, cancellationToken);

            CurrentStep = SearchStep.Done;
            CurrentStep = SearchStep.StandBy;

            return duplicates;
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
            FileSystemErrors.Add(eventArgs);
        }
    }
}
