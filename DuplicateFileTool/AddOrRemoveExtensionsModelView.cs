using System.ComponentModel;
using System.Runtime.CompilerServices;
using DuplicateFileTool.Annotations;

namespace DuplicateFileTool
{
    public class AddOrRemoveExtensionsModelView : INotifyPropertyChanged
    {

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
