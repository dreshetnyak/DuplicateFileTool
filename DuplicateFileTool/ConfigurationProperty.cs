using System.ComponentModel;
using System.Globalization;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool;

#region Validarion Rules

internal sealed class DefaultValidationRule : ValidationRule
{
    public override ValidationResult Validate(object? value, CultureInfo cultureInfo) { return new(true, null); }
}

[Localizable(true)]
internal sealed class LongValidationRule(long? minValue = null, long? maxValue = null) : ValidationRule
{
    private long? MinValue { get; } = minValue;
    private long? MaxValue { get; } = maxValue;

    public override ValidationResult Validate(object? value, CultureInfo cultureInfo)
    {
        if (value is null)
            return new ValidationResult(false, Resources.Error_The_value_is_not_a_valid_number);

        long longValue;
        switch (value)
        {
            case int intValue:
                longValue = intValue;
                break;
            case long value64:
                longValue = value64;
                break;
            default:
                return new ValidationResult(false, Resources.Error_The_value_is_not_a_an_integer);
        }

        if (MinValue.HasValue && longValue < MinValue.Value)
            return new ValidationResult(false, Resources.Error_The_value_is_less_than_the_allowed_minimum);

        if (MaxValue.HasValue && longValue > MaxValue.Value)
            return new ValidationResult(false, Resources.Error_The_value_is_greater_than_the_allowed_maximum);

        return new ValidationResult(true, null);
    }
}

[Localizable(true)]
internal sealed class PositiveFiniteDoubleValidationRule : ValidationRule
{
    public override ValidationResult Validate(object? value, CultureInfo cultureInfo)
    {
        if (value is not double doubleValue || !double.IsFinite(doubleValue))
            return new ValidationResult(false, Resources.Error_The_value_is_not_a_valid_number);

        return doubleValue > 0
            ? new ValidationResult(true, null)
            : new ValidationResult(false, Resources.Error_The_value_is_less_than_the_allowed_minimum);
    }
}

#endregion

#region Configuration Property

internal interface IConfigurationProperty<T> : INotifyPropertyChanged, INotifyDataErrorInfo
{
    string Name { get; }                // Short name of the property to be displayed before the property value entry
    string Description { get; }         // Long description of the configuration property that should be available on demand
    bool IsReadOnly { get; }            // If set - the parameter cannot be changed by the user
    bool IsHidden { get; }              // Indicates if this property should be isHidden in the configuration UI
    ValidationRule Validator { get; }   // Validation Rule for the value
    public T DefaultValue { get; }      // Default value
    T? Value { get; set; }               // The property value
    T[] Options { get; }                // In a case of en Enum will contain all Enum values
    bool TrySetValue(T? value);          // Validates and stores the value, retaining the last valid value on failure
}

internal sealed class ConfigurationProperty<T> : IConfigurationProperty<T>
{
    #region Backing Fields
    private T? _value;
    private bool _isValid;
    private bool _isInvalid;
    private object? _validationError;

    #endregion

    public string Name { get; }                 // Short name of the property to be displayed before the property value entry
    public string Description { get; }          // Long description of the configuration property that should be available on demand
    public bool IsReadOnly { get; }             // If set - the parameter cannot be changed by the user
    public bool IsHidden { get; }               // Indicates if this property should be isHidden in the configuration UI
    public ValidationRule Validator { get; }    // Validation Rule for the value
    public T DefaultValue { get; }              // Default property value

    public T? Value                             // The property value
    {
        get => _value;
        set => TrySetValue(value);
    }
    public T[] Options { get; }

    public bool IsValid
    {
        get => _isValid;
        private set
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
        private set
        {
            if (_isInvalid == value)
                return;
            _isInvalid = value;
            OnPropertyChanged();
        }
    }

    public ConfigurationProperty(string name, string description, T defaultValue, ValidationRule? validator = null, bool isReadOnly = false, bool isHidden = false)
    {
        Name = name;
        Description = description;
        Validator = validator ?? new DefaultValidationRule();
        _value = DefaultValue = defaultValue;
        IsReadOnly = isReadOnly;
        IsHidden = isHidden;
        Options = GetOptions();

        Validate();
    }

    public bool TrySetValue(T? value)
    {
        if (IsReadOnly)
            return false;

        var validationResult = Validator.Validate(value, CultureInfo.CurrentCulture);
        SetValidationResult(validationResult);
        if (!validationResult.IsValid)
            return false;

        if (EqualityComparer<T?>.Default.Equals(_value, value))
            return true;

        _value = value;
        OnPropertyChanged(nameof(Value));
        return true;
    }

    private static T[] GetOptions()
    {
        return typeof(T) is { IsEnum: true } 
            ? Enum.GetValues(typeof(T)).Cast<T>().ToArray() 
            : [];
    }

    private void Validate()
    {
        SetValidationResult(Validator.Validate(Value, CultureInfo.CurrentCulture));
    }

    private void SetValidationResult(ValidationResult validationResult)
    {
        var hadErrors = HasErrors;
        var previousError = _validationError;

        _validationError = validationResult.IsValid ? null : validationResult.ErrorContent;
        IsValid = validationResult.IsValid;
        IsInvalid = !validationResult.IsValid;

        if (hadErrors == HasErrors && Equals(previousError, _validationError))
            return;

        OnPropertyChanged(nameof(HasErrors));
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Value)));
    }

    public override string ToString()
    {
        return Value?.ToString() ?? "";
    }

    #region INotifyPropertyChanged Implementation

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool HasErrors => IsInvalid;
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public IEnumerable GetErrors(string? propertyName)
    {
        if (!string.IsNullOrEmpty(propertyName) && propertyName != nameof(Value))
            return Array.Empty<object>();

        return HasErrors
            ? new[] { _validationError ?? Resources.Error_The_value_is_not_a_valid_number }
            : Array.Empty<object>();
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = "") => 
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion
}

#endregion
