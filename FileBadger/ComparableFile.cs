using System.Threading;

namespace DuplicateFileTool
{
    internal interface IComparerConfig
    {
        string ComparerId { get; }
        string ComparerName { get; }
        string ComparerDescription { get; }
        int MatchThreshold { get; }
        int CompleteMatch { get; }
        int CompleteMismatch { get; }
    }

    internal abstract class ComparerConfig : IComparerConfig
    {
        public string ComparerId { get; protected set; } 
        public string ComparerName { get; protected set; } 
        public string ComparerDescription { get; protected set; }
        public int MatchThreshold { get; protected set; } = 10000;
        public int CompleteMatch { get; protected set; } = 10000;
        public int CompleteMismatch { get; protected set; } = 0;
    }

    internal interface IComparableFile
    {
        FileData FileData { get; }

        int CompareTo(IComparableFile otherFile, CancellationToken cancellationToken);
    }

    internal interface IComparableFileFactory
    {
        public IComparerConfig ComparerConfig { get; }

        public IComparableFile Create(FileData file);
    }
}
