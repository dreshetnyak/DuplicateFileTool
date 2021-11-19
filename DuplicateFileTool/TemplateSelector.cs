using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace DuplicateFileTool
{
    internal class ParameterTemplate
    {
        public string Enabled { get; }
        public string Disabled { get; }

        public ParameterTemplate(string enabled, string disabled)
        {
            Enabled = enabled;
            Disabled = disabled;
        }
    }

    internal abstract class TemplateSelectorBase : DataTemplateSelector
    {
        protected string DefaultTemplateName { get; set; }
        protected Dictionary<Type, ParameterTemplate> ParameterTemplates { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item == null || container is not FrameworkElement element)
                return null;

            var itemType = item.GetType();
            if (!itemType.ImplementsInterfaceGeneric(typeof(IConfigurationProperty<>)))
                return (DataTemplate)element.FindResource(DefaultTemplateName);

            var isReadOnlyProperty = itemType.GetProperty(nameof(IConfigurationProperty<int>.IsReadOnly));
            if (isReadOnlyProperty == null)
                return (DataTemplate)element.FindResource(DefaultTemplateName);
            var isReadOnly = (bool)isReadOnlyProperty.GetValue(item);

            var valueProperty = itemType.GetProperty(nameof(IConfigurationProperty<int>.Value));
            if (valueProperty == null)
                return (DataTemplate)element.FindResource(DefaultTemplateName);
            var valueType = valueProperty.PropertyType;
            if (valueType.IsEnum)
                valueType = typeof(Enum);

            return ParameterTemplates.ContainsKey(valueType) 
                ? (DataTemplate)element.FindResource(isReadOnly ? ParameterTemplates[valueType].Disabled : ParameterTemplates[valueType].Enabled)
                : (DataTemplate)element.FindResource(DefaultTemplateName);
        }
    }

    internal class ConfigTemplateSelector : TemplateSelectorBase
    {
        public ConfigTemplateSelector()
        {
            DefaultTemplateName = "TextBlockTemplate";
            ParameterTemplates = new Dictionary<Type, ParameterTemplate>
            {
                [typeof(string)] = new("TextBlockTemplate", "TextBlockTemplate"),
                [typeof(int)] = new("TextBlockTemplate", "TextBlockTemplate"),
                [typeof(long)] = new("TextBlockTemplate", "TextBlockTemplate"),
                [typeof(bool)] = new("CheckBoxTemplate", "CheckBoxTemplate"),
                [typeof(Enum)] = new("TextBlockTemplate", "TextBlockTemplate"),
                [typeof(Guid)] = new("TextBlockTemplate", "TextBlockTemplate")
            };
        }
    }

    internal class ConfigEditTemplateSelector : TemplateSelectorBase
    {
        public ConfigEditTemplateSelector()
        {
            DefaultTemplateName = "TextBlockTemplate";
            ParameterTemplates = new Dictionary<Type, ParameterTemplate>
            {
                [typeof(string)] = new("TextBoxTemplate", "TextBlockTemplate"),
                [typeof(int)] = new("TextBoxTemplate", "TextBlockTemplate"),
                [typeof(long)] = new("TextBoxTemplate", "TextBlockTemplate"),
                [typeof(bool)] = new("CheckBoxTemplate", "CheckBoxTemplate"),
                [typeof(Enum)] = new("ComboBoxTemplate", "TextBlockTemplate"),
                [typeof(Guid)] = new("TextBlockTemplate", "TextBlockTemplate")
            };
        }
    }
}