namespace Reloaded.Mod.Launcher.Lib.Commands.Mod;

/// <summary>
/// Command allowing you to delete an individual mod.
/// </summary>
public class DeleteModCommand : WithCanExecuteChanged, ICommand
{
    private readonly PathTuple<ModConfig>? _modTuple;

    /// <inheritdoc />
    public DeleteModCommand(PathTuple<ModConfig>? modTuple)
    {
        _modTuple = modTuple;
    }

    /* ICommand. */
    /// <inheritdoc />
    public bool CanExecute(object? parameter) => _modTuple != null;

    /// <inheritdoc />
    public void Execute(object? parameter)
    {
        // Delete folder contents.
        var directory = Path.GetDirectoryName(_modTuple!.Path) ?? throw new InvalidOperationException(Resources.ErrorFailedToGetDirectoryOfMod.Get());
        Directory.Delete(directory, true);
    }
}