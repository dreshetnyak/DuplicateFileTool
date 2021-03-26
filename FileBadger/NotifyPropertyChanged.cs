using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileBadger
{
    internal abstract class NotifyPropertyChanged : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    internal abstract class TrackedChangeNotifier<T> : NotifyPropertyChanged
    {
        public bool HasChanged { get; set; }

        protected TrackedChangeNotifier()
        {
            PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs eventArgs)
        {
            if (HasChanged)
                return;
            if (GetType().HasPropertyWhereAttribute(eventArgs.PropertyName, typeof(T)))
                HasChanged = true;
        }
    }
}
