using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FileBadger
{
    //TODO organize this file

    [AttributeUsage(AttributeTargets.Class)]
    internal class FileComparerAttribute : Attribute
    {
        public string Guid { get; }
        public string Name { get; }
        public string Description { get; }
        public Type ConfigurationType { get; }
        
        public FileComparerAttribute(string guid, string name, string description, Type configurationType)
        {
            Guid = guid;
            Name = name;
            Description = description;
            ConfigurationType = configurationType;
        }
    }

    #region ConfigurationProperty Attribute

    [AttributeUsage(AttributeTargets.Property)]
    internal class ConfigurationPropertyAttribute : Attribute
    {
        public string Name { get; }                     // Short name of the property to be displayed before the property value entry
        public string Description { get; }              // Long description of the configuration property that should be available on demand

        public ConfigurationPropertyAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    internal class DefaultValueAttribute : Attribute
    {
        public object DefaultValue { get; }

        public DefaultValueAttribute(object defaultValue)
        {
            DefaultValue = defaultValue;
        }
    }
    
    #endregion

    #region Validarion Rule Attributes

    internal interface IValidationRule
    {
        (bool IsValid, string Message) IsValid(object value);
    }

    internal abstract class IntValidationRule : Attribute, IValidationRule
    {
        public abstract (bool IsValid, string Message) IsValid(object value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool TryParseInt(object value, out int parsedIntValue, out string message)
        {
            if (ReferenceEquals(value, null))
            {
                parsedIntValue = 0;
                message = "The value is not a valid number";
                return false;
            }

            if (!(value is int))
            {
                parsedIntValue = 0;
                message = "The value is not a an integer";
                return false;
            }

            parsedIntValue = (int)value;
            message = "";
            return true;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    internal class IntRangeValidationRuleAttribute : IntValidationRule
    {
        public int MinValue { get; }
        public int MaxValue { get; }

        public IntRangeValidationRuleAttribute(int minValue, int maxValue)
        {
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public override (bool IsValid, string Message) IsValid(object value)
        {
            if (!TryParseInt(value, out var intValue, out var message))
                return (false, message);

            if (intValue < MinValue)
                return (false, $"The value is less than the allowed minimum of {MinValue:N0}");
            return intValue > MaxValue 
                ? (false, $"The value is greater than the allowed maximum of {MaxValue:N0}") 
                : (true, "");
        }
    }

    #endregion

}
