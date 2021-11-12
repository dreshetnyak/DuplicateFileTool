using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Documents;
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
    internal class LongValidationRule : ValidationRule
    {
        private long? MinValue { get; }
        private long? MaxValue { get; }

        public LongValidationRule(long? minValue = null, long? maxValue = null)
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

    #region Configuration Property

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
        private bool _isValid;
        private bool _isInvalid;

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
                if (IsReadOnly || Equals(_value, value))
                    return;
                _value = value;
                OnPropertyChanged();
                Validate();
            }
        }

        private static bool Equals(T currentValue, T newValue)
        {
            return !ReferenceEquals(currentValue, null)
                ? !ReferenceEquals(newValue, null) && currentValue.Equals(newValue)
                : !ReferenceEquals(newValue, null);
        }

        public bool IsValid
        {
            get => _isValid;
            set
            {
                if (_isValid == value)
                    return;
                _isValid = value;
                OnPropertyChanged();
            }
        }
        public bool IsInvalid
        {
            get => _isInvalid;
            set
            {
                if (_isInvalid == value)
                    return;
                _isInvalid = value;
                OnPropertyChanged();
            }
        }

        public ConfigurationProperty(string name, string description, T defaultValue, ValidationRule validator = null, bool isReadOnly = false, bool isHidden = false)
        {
            Name = name;
            Description = description;
            Validator = validator ?? new DefaultValidationRule();
            _value = DefaultValue = defaultValue;
            IsReadOnly = isReadOnly;
            IsHidden = isHidden;
            Validate();
        }

        private void Validate()
        {
            var isValid = Validator == null || Validator.Validate(Value, CultureInfo.CurrentCulture).IsValid;
            IsValid = isValid;
            IsInvalid = !isValid;
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
}
