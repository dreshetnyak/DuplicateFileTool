using System;
using System.ComponentModel;
using System.Threading;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool
{
    #region File Comparer Configuration Base

    internal interface IFileComparerConfig
    {
        IConfigurationProperty<int> MatchThreshold { get; }
        IConfigurationProperty<int> CompleteMatch { get; }
        IConfigurationProperty<int> CompleteMismatch { get; }
    }

    [Localizable(true)]
    internal abstract class FileComparerConfig : IFileComparerConfig
    {
        public IConfigurationProperty<int> MatchThreshold { get; protected set; } = new ConfigurationProperty<int>(
            Resources.Config_Comparer_MatchThreshold_Name,
            Resources.Config_Comparer_MatchThreshold_Description,
            10000, new LongValidationRule(0, int.MaxValue));
        
        public IConfigurationProperty<int> CompleteMatch { get; protected set; } = new ConfigurationProperty<int>(
            Resources.Config_Comparer_CompleteMatch_Name,
            Resources.Config_Comparer_CompleteMatch_Description,
            10000, new LongValidationRule(0, int.MaxValue));

        public IConfigurationProperty<int> CompleteMismatch { get; protected set; } = new ConfigurationProperty<int>(
            Resources.Config_Comparer_CompleteMismatch_Name,
            Resources.Config_Comparer_CompleteMismatch_Description,
            0, new LongValidationRule(0, int.MaxValue));
    }

    #endregion

    #region Candidate Predicate Interface

    internal interface ICandidatePredicate
    {
        bool IsCandidate(FileData firstFile, FileData secondFile);
    }

    #endregion
    
    #region Comparable File Factory

    internal interface IComparableFileFactory
    {
        public IFileComparerConfig Config { get; }

        public IComparableFile Create(FileData file);
    }

    #endregion

    #region File Comparer Base

    internal interface IFileComparer
    {
        Guid Guid { get; }
        IConfigurationProperty<string> Name { get; }
        IConfigurationProperty<string> Description { get; }
        IFileComparerConfig Config { get; }
        ICandidatePredicate CandidatePredicate { get; }
        IComparableFileFactory ComparableFileFactory { get; }
    }

    [Localizable(true)]
    internal abstract class FileComparer : IFileComparer
    {
        public Guid Guid { get; }
        public IConfigurationProperty<string> Name { get; }
        public IConfigurationProperty<string> Description { get; }
        public IFileComparerConfig Config { get; protected set; }
        public ICandidatePredicate CandidatePredicate { get; protected set; }
        public IComparableFileFactory ComparableFileFactory { get; protected set; }

        protected FileComparer(Guid guid, string name, string description)
        {
            Guid = guid;
            
            Name = new ConfigurationProperty<string>(
                Resources.Config_Comparer_Name_Name, 
                Resources.Config_Comparer_Name_Description, 
                name, 
                isReadOnly: true);

            Description = new ConfigurationProperty<string>(
                Resources.Config_Comparer_Description_Name, 
                Resources.Config_Comparer_Description_Description, 
                description, 
                isReadOnly: true);
        }
    }

    #endregion

    #region Comparable File

    internal interface IComparableFile
    {
        FileData FileData { get; }

        int CompareTo(IComparableFile otherFile, CancellationToken cancellationToken);
    }

    #endregion
}
