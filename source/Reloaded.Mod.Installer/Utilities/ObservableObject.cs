namespace Reloaded.Mod.Installer.Utilities;

/// <summary>
/// An abstract class that implements the bare minimum of the INotifyPropertyChanged interface.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void RaisePropertyChangedEvent(string propertyName)
    {
        var handler = PropertyChanged;
        handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}