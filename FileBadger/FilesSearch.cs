using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileBadger
{
    internal class FilesSearchProgressEventArgs : EventArgs
    {
        public string DirPath { get; }
        public int FoundFilesCount { get; }
        public FilesSearchProgressEventArgs(string dirPath, int foundFilesCount) { DirPath = dirPath; FoundFilesCount = foundFilesCount; }
    }
    internal delegate void FilesSearchProgressEventHandler(object sender, FilesSearchProgressEventArgs eventArgs);
   
    internal class FilesSearch
    {
        public event FilesSearchProgressEventHandler FilesSearchProgress;
        public event FileSystemErrorEventHandler FileSystemError;

        public async Task<List<FileData>> Find(IReadOnlyCollection<SearchPath> paths, IInclusionPredicate inclusionPredicate, CancellationToken cancellationToken)
        {
            var fileSearchResults = new List<Task<List<FileData>>>();
            //var pathsArray = paths as SearchPath[] ?? paths.ToArray();

            foreach (var physicalDrivePartitions in FileSystem.GetDrivesInfo().GroupBy(drive => drive.PhysicalDriveNumber)) //Split the work by physical drives
            {
                var physicalDrivePaths = paths.Where(path =>
                    physicalDrivePartitions.Any(driveInfo =>
                        driveInfo.DriveLetter == new DirectoryInfo(path).Root.FullName)).ToArray();

                fileSearchResults.Add(Task.Run(() => FindFiles(physicalDrivePaths, inclusionPredicate, cancellationToken), cancellationToken));
            }

            var searchResults = new List<FileData>();
            foreach (var task in fileSearchResults)
                searchResults.AddRange(await task);

            return searchResults;
        }

        private List<FileData> FindFiles(IEnumerable<string> paths, IInclusionPredicate inclusionPredicate, CancellationToken cancellationToken)
        {
            var foundFiles = new List<FileData>();

            try
            {
                foreach (var path in paths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    foreach (var foundPath in FindPathFiles(path, foundFiles.Count, inclusionPredicate, cancellationToken))
                    {
                        if (inclusionPredicate.IsFileIncluded(foundPath))
                            foundFiles.Add(foundPath);
                    }
                }
            }
            catch (DirectoryAccessFailedException ex)
            {
                OnFileSystemError(ex.DirectoryPath, ex.Message, ex);
            }

            return foundFiles;
        }

        private IEnumerable<FileData> FindPathFiles(string targetPath, int foundFilesCount, IInclusionPredicate inclusionPredicate, CancellationToken cancellationToken)
        {
            foreach (var fileData in new DirectoryEnumeration(targetPath)) //DirectoryEnumeration throws
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!inclusionPredicate.IsFileIncluded(fileData))
                    continue;

                if (!fileData.Attributes.IsDirectory)
                {
                    OnFilesSearchProgress(fileData.FullName, foundFilesCount++);
                    yield return fileData;
                    continue;
                }
                
                foreach (var subDirFileData in FindPathFiles(fileData.FullName, foundFilesCount, inclusionPredicate, cancellationToken))
                {
                    OnFilesSearchProgress(fileData.FullName, foundFilesCount++);
                    yield return subDirFileData;
                }
            }
        }

        protected virtual void OnFilesSearchProgress(string dirPath, int foundFilesCount)
        {
            FilesSearchProgress?.Invoke(this, new FilesSearchProgressEventArgs(dirPath, foundFilesCount));
        }

        protected virtual void OnFileSystemError(string path, string message, Exception exception = null)
        {
            FileSystemError?.Invoke(this, new FileSystemErrorEventArgs(path, message, exception));
        }
    }
}
