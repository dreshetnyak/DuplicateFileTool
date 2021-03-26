using System.Threading;

namespace FileBadger
{
    internal interface IComparerConfig
    {
        string ComparerId { get; }
        string ComparerName { get; }
        string ComparerDescription { get; }
        int MatchThreshold { get; }
    }

    internal abstract class ComparerConfig
    {
        public string ComparerId { get; protected set; } 
        public string ComparerName { get; protected set; } 
        public string ComparerDescription { get; protected set; } 
        public int MatchThreshold { get; protected set; } = ComparableFile.CompleteMatch;
    }

    internal abstract class ComparableFile
    {
        public const int CompleteMatch = 10000;
        public const int CompleteMismatch = 0;

        public FileData FileData { get; protected set; }

        public abstract int CompareTo(ComparableFile otherFile, CancellationToken cancellationToken);
    }

    internal abstract class ComparableFileFactory
    {
        public IComparerConfig ComparerConfig { get; }

        protected ComparableFileFactory(IComparerConfig comparerConfig)
        {
            ComparerConfig = comparerConfig;
        }

        public abstract ComparableFile Create(FileData file);
    }
}
