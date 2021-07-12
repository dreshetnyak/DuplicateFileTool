using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DuplicateFileTool
{
    internal abstract class NotifyPropertyChanged : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    internal class PropertiesChangeTracker<T> : NotifyPropertyChanged, IDisposable
    {
        private bool _hasChanged;
        private T TrackedObject { get; }

        public bool HasChanged
        {
            get => _hasChanged;
            set
            {
                _hasChanged = value; 
                OnPropertyChanged();
            }
        }

        public PropertiesChangeTracker(T trackedObject)
        {
            TrackedObject = trackedObject;
            Subscribe();
        }

        public void Dispose()
        {
            Unsubscribe();
        }

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
            var trackedObject = TrackedObject;
            foreach (var property in trackedObject.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.PropertyType.ImplementsInterface(typeof(INotifyPropertyChanged)) && property.GetValue(trackedObject) is INotifyPropertyChanged propertyValue)
                    yield return propertyValue;
            }
        }
        
        private void OnPropertyChanged(object sender, PropertyChangedEventArgs eventArgs)
        {
            HasChanged = true;
        }
    }
}