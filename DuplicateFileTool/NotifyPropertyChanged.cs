using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DuplicateFileTool;

internal abstract class NotifyPropertyChanged : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "") => 
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal sealed class PropertiesChangeTracker<T> : NotifyPropertyChanged, IDisposable
{
    private bool _hasChanged;
    private T TrackedObject { get; }

    public bool HasChanged
    {
        get => _hasChanged;
        set
        {
            if (_hasChanged == value)
                return;
            _hasChanged = value;
            OnPropertyChanged();
        }
    }

    public PropertiesChangeTracker(T trackedObject)
    {
        TrackedObject = trackedObject ?? throw new ArgumentNullException(nameof(trackedObject));
        Subscribe();
    }

    public void Dispose() => Unsubscribe();

    private void Subscribe()
    {
        foreach (var trackableObject in GetTrackableObjects())
            trackableObject.PropertyChanged += OnPropertyChanged;
    }

    private void Unsubscribe()
    {
        foreach (var trackableObject in GetTrackableObjects())
            trackableObject.PropertyChanged -= OnPropertyChanged;
    }

    private IEnumerable<INotifyPropertyChanged> GetTrackableObjects()
    {
        foreach (var property in TrackedObject!.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.PropertyType.ImplementsInterface(typeof(INotifyPropertyChanged)) && property.GetValue(TrackedObject) is INotifyPropertyChanged propertyValue)
                yield return propertyValue;
        }
    }
        
    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs) => 
        HasChanged = true;
}