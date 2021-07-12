using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Controls;
using DuplicateFileTool.Annotations;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool
{
    #region Validarion Rules

    internal class DefaultValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo) { return new(true, null); }
    }

    [Localizable(true)]
    internal class IntValidationRule : ValidationRule
    {
        private int? MinValue { get; }
        private int? MaxValue { get; }

        public IntValidationRule(int? minValue = null, int? maxValue = null)
        {
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (ReferenceEquals(value, null))
                return new ValidationResult(false, Resources.Error_The_value_is_not_a_valid_number);

            if (value is not int intValue)
                return new ValidationResult(false, Resources.Error_The_value_is_not_a_an_integer);

            if (MinValue.HasValue && intValue < MinValue.Value)
                return new ValidationResult(false, Resources.Error_The_value_is_less_than_the_allowed_minimum);

            if (MaxValue.HasValue && intValue > MaxValue.Value)
                return new ValidationResult(false, Resources.Error_The_value_is_greater_than_the_allowed_maximum);

            return new ValidationResult(true, null);
        }
    }

    #endregion

    #region Configuration Property Base

    internal interface IConfigurationProperty<T> : INotifyPropertyChanged
    {
        string Name { get; }                // Short name of the property to be displayed before the property value entry
        string Description { get; }         // Long description of the configuration property that should be available on demand
        bool IsReadOnly { get; }            // If set - the parameter cannot be changed by the user
        bool IsHidden { get; }              // Indicates if this property should be isHidden in the configuration UI
        ValidationRule Validator { get; }   // Validation Rule for the value
        public T DefaultValue { get; }      // Default value
        T Value { get; set; }               // The property value
    }

    internal class ConfigurationProperty<T> : IConfigurationProperty<T>
    {
        #region Backing Fields
        private T _value;

        #endregion

        public string Name { get; }                 // Short name of the property to be displayed before the property value entry
        public string Description { get; }          // Long description of the configuration property that should be available on demand
        public bool IsReadOnly { get; }             // If set - the parameter cannot be changed by the user
        public bool IsHidden { get; }               // Indicates if this property should be isHidden in the configuration UI
        public ValidationRule Validator { get; }    // Validation Rule for the value
        public T DefaultValue { get; }              // Default property value

        public T Value                              // The property value
        {
            get => _value;
            set
            {
                if (IsReadOnly)
                    return;
                _value = value;
                OnPropertyChanged();
            }
        }

        public ConfigurationProperty(string name, string description, T defaultValue, ValidationRule validator = null, bool isReadOnly = false, bool isHidden = false)
        {
            Name = name;
            Description = description;
            Validator = validator ?? new DefaultValidationRule();
            Value = DefaultValue = defaultValue;
            IsReadOnly = isReadOnly;
            IsHidden = isHidden;
        }

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    #endregion

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
            10000, new IntValidationRule(0, int.MaxValue));
        
        public IConfigurationProperty<int> CompleteMatch { get; protected set; } = new ConfigurationProperty<int>(
            Resources.Config_Comparer_CompleteMatch_Name,
            Resources.Config_Comparer_CompleteMatch_Description,
            10000, new IntValidationRule(0, int.MaxValue));

        public IConfigurationProperty<int> CompleteMismatch { get; protected set; } = new ConfigurationProperty<int>(
            Resources.Config_Comparer_CompleteMismatch_Name,
            Resources.Config_Comparer_CompleteMismatch_Description,
            0, new IntValidationRule(0, int.MaxValue));
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
