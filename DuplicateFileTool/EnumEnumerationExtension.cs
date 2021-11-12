using System;
using System.Windows.Markup;

namespace DuplicateFileTool
{
    internal class EnumEnumerationExtension : MarkupExtension
    {
        private object EnumObj { get; }

        public EnumEnumerationExtension(object enumObj)
        {
            EnumObj = enumObj;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (EnumObj == null)
                return Array.Empty<object>();

            var enumObjType = EnumObj.GetType();
            return enumObjType.IsEnum 
                ? Enum.GetValues(enumObjType)
                : Array.Empty<object>();
        }
    }
}
