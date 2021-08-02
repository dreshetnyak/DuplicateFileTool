using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DuplicateFileTool
{
    internal sealed class FilesSearchProgressEventArgs : EventArgs
    {
        public string Path { get; }
        public bool IsDirectory { get; }
        public int FoundFilesCount { get; }
        public FilesSearchProgressEventArgs(string path, bool isDirectory, int foundFilesCount) { Path = path; IsDirectory = isDirectory; FoundFilesCount = foundFilesCount; }
    }
    internal delegate void FilesSearchProgressEventHandler(object sender, FilesSearchProgressEventArgs eventArgs);
   
    internal class FilesSearch
    {
        public event FilesSearchProgressEventHandler FilesSearchProgress;
        public event FileSystemErrorEventHandler FileSystemError;

        public async Task<IReadOnlyCollection<FileData>> Find(IReadOnlyCollection<SearchPath> searchPaths, IInclusionPredicate inclusionPredicate, CancellationToken cancellationToken)
        {
            var fileSearchResults = new List<Task<IReadOnlyCollection<FileData>>>();

            foreach (var physicalDrivePartitions in FileSystem.GetDrivesInfo().GroupBy(drive => drive.PhysicalDriveNumber)) //Split the work by physical drives
            {
                var physicalDrivePaths = searchPaths.Where(searchPath =>
                    physicalDrivePartitions.Any(driveInfo =>
                        driveInfo.DriveLetter == new DirectoryInfo(searchPath.Path).Root.FullName)).ToArray();

                if (physicalDrivePaths.Length == 0)
                    continue;

                fileSearchResults.Add(Task.Run(() => FindFiles(physicalDrivePaths, inclusionPredicate, cancellationToken), cancellationToken));
            }

            var searchResults = new List<FileData>();
            foreach (var task in fileSearchResults)
                searchResults.AddRange(await task);

            return searchResults;
        }

        private IReadOnlyCollection<FileData> FindFiles(IEnumerable<SearchPath> searchPaths, IInclusionPredicate inclusionPredicate, CancellationToken cancellationToken)
        {
            var foundFiles = new List<FileData>();

            var searchPathsList = searchPaths.ToList();
            var includePaths = GetPaths(searchPathsList, InclusionType.Include);
            var excludePaths = GetPaths(searchPathsList, InclusionType.Exclude);

            try
            {
                foreach (var path in includePaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    foreach (var foundPath in FindPathFiles(path, excludePaths, foundFiles.Count, inclusionPredicate, cancellationToken))
                    {
                        if (inclusionPredicate.IsIncluded(foundPath))
                            foundFiles.Add(foundPath);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DirectoryAccessFailedException ex)
            {
                OnFileSystemError(ex.DirectoryPath, ex.Message, ex);
            }
            catch (Exception ex)
            {
                OnFileSystemError("", ex.Message, ex);
            }

            return foundFiles;
        }

        private static IReadOnlyCollection<string> GetPaths(IEnumerable<SearchPath> searchPaths, InclusionType inclusionType)
        {
            var paths = searchPaths
                .Where(searchPath => searchPath.PathInclusionType == inclusionType)
                .Select(searchPath => searchPath.Path)
                .ToList();

            paths.RemoveAll(path1 => paths.Any(path2 => !ReferenceEquals(path1, path2) && path1.StartsWith(path2, StringComparison.OrdinalIgnoreCase)));

            return paths;
        }

        private IEnumerable<FileData> FindPathFiles(string path, IReadOnlyCollection<string> excludePaths, int foundFilesCount, IInclusionPredicate inclusionPredicate, CancellationToken cancellationToken)
        {
            using var directoryEnumerator = new DirectoryEnumeration(path).GetEnumerator();
            var moreItems = false;
            do
            {
                FileData fileData;
                try
                {
                    if (!(moreItems = directoryEnumerator.MoveNext()))
                        continue;
                    fileData = directoryEnumerator.Current;
                    if (fileData == null)
                        continue;
                }
                catch (FileSystemException ex)
                {
                    OnFileSystemError(ex.FileFullName, ex.Message, ex);
                    continue;
                }
                catch (Exception ex)
                {
                    OnFileSystemError(path, ex.Message, ex);
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!inclusionPredicate.IsIncluded(fileData))
                    continue;

                if (!fileData.Attributes.IsDirectory)
                {
                    OnFilesSearchProgress(fileData.FullName, false, foundFilesCount++);
                    yield return fileData;
                    continue;
                }

                if (excludePaths.Any(excludePath => fileData.FullName.StartsWith(excludePath, StringComparison.OrdinalIgnoreCase)))
                    continue;

                OnFilesSearchProgress(fileData.FullName, true, foundFilesCount);

                foreach (var subDirFileData in FindPathFiles(fileData.FullName, excludePaths, foundFilesCount, inclusionPredicate, cancellationToken))
                {
                    OnFilesSearchProgress(fileData.FullName, false, foundFilesCount++);
                    yield return subDirFileData;
                }

            } while (moreItems);
        }

        protected virtual void OnFilesSearchProgress(string dirPath, bool isDirectory, int foundFilesCount)
        {
            FilesSearchProgress?.Invoke(this, new FilesSearchProgressEventArgs(dirPath, isDirectory, foundFilesCount));
        }

        protected virtual void OnFileSystemError(string path, string message, Exception exception = null)
        {
            FileSystemError?.Invoke(this, new FileSystemErrorEventArgs(path, message, exception));
        }
    }
}
