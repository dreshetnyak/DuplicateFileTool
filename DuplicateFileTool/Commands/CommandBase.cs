using System.Windows.Input;

namespace DuplicateFileTool.Commands;

internal interface ICancellable
{
    bool CanCancel { get; }

    event EventHandler CanCancelChanged;

    void Cancel();
}

internal abstract class CommandBase(bool enabled = true) : NotifyPropertyChanged, ICommand, ICancellable
{
    #region Can Execute Implementation

    private bool _enabled = enabled;
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
                return;
            _enabled = value;
            OnCanExecuteChanged();
        }
    }

    public event EventHandler? CanExecuteChanged;

    protected virtual void OnCanExecuteChanged() => 
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    public bool CanExecute(object? parameter) => Enabled;

    #endregion

    #region Can Cancel Implementation

    private bool _canCancel;
    public bool CanCancel
    {
        get => _canCancel;
        set
        {
            if (_canCancel == value)
                return;
            _canCancel = value;
            OnCanCancelChanged();
        }
    }

    public event EventHandler? CanCancelChanged;

    protected virtual void OnCanCancelChanged() => 
        CanCancelChanged?.Invoke(this, EventArgs.Empty);

    public virtual void Cancel() { }

    #endregion

    public abstract void Execute(object? parameter);
}