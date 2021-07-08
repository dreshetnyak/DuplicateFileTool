namespace DuplicateFileTool
{
    internal class Observable<T> : NotifyPropertyChanged
    {
        private T _value;

        public T Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged();
            }
        }
    }

    internal class ObservableString : Observable<string>
    { }
}
