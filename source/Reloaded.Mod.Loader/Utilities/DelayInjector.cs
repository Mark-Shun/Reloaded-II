namespace Reloaded.Mod.Loader.Utilities;

/// <summary>
/// Mass hooks Windows APIs to inject a callback which will be used to run a function once.
/// Then all hooks are undone.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Difficult to test in unit tests. Mostly covered by tests in Reloaded.Hooks. This is tested on executables using Steam DRM.")]
public unsafe class DelayInjector
{
    private List<DllEntry> _dlls;
    private List<IAsmHook> _hooks = new();
    private List<IReverseWrapper<Macros.AsmAction>> _wrappers = new();

    private Action _method;
    private int* _asmEntryDllOrdinal;
    private int* _asmEntryFunctionOrdinal;
    private Logger _logger;

    public DelayInjector(IReloadedHooks hooks, Action action, Logger logger)
    {
        _logger = logger;

        // Allocate space for code to identify entry point from hooks.
        // DO NOT CHANGE, UNLESS ALSO CHANGING ASSEMBLY BELOW
        _asmEntryDllOrdinal = (int*)NativeMemory.Alloc(sizeof(int) * 2);
        _asmEntryFunctionOrdinal = _asmEntryDllOrdinal + 1;

        var assemblyFolder = Path.GetDirectoryName(typeof(DelayInjector).Assembly.Location);
        var predefinedDllPath = Path.Combine(assemblyFolder, "DelayInjectHooks.json");

        _method = action;
        _dlls = JsonSerializer.Deserialize<List<DllEntry>>(File.ReadAllText(predefinedDllPath));

        for (var x = 0; x < _dlls.Count; x++)
        {
            var dll = _dlls[x];
            var handle = Native.Kernel32.GetModuleHandle(dll.Name);
            if (handle == IntPtr.Zero)
                continue;

            for (var y = 0; y < dll.Functions.Length; y++)
            {
                var functionAddr = Native.Kernel32.GetProcAddress(handle, dll.Functions[y]);
                if (functionAddr == IntPtr.Zero)
                    continue;

                _hooks.Add(CreateHook((long) functionAddr, x, y, hooks));
            }
        }
    }

    /// <summary>
    /// Creates a hook for a specified address.
    /// </summary>
    /// <param name="address">The address to hook.</param>
    /// <param name="dllOrdinal">Index of the DLL in the DLL list.</param>
    /// <param name="functionOrdinal">Index of the function in the DLL's function list.</param>
    /// <param name="hooks">The hooking library to use.</param>
    private unsafe IAsmHook CreateHook(long address, int dllOrdinal, int functionOrdinal, IReloadedHooks hooks)
    {
        var utilities = hooks.Utilities;
            
        // Assumes function uses Microsoft call convention on x64.
        var asmCode = new string[]
        {
            $"{Macros._use32}",

            // Backup
            Macros.Is64Bit ? "sub rsp, 8" : "", // Stack alignment.
            $"{Macros.PushAll}",
            Macros.Is64Bit ? Macros.PushSseCallConvRegistersx64 : "", // Push Microsoft Call Convention SSE Float Registers

            $"mov dword [dword 0x{(long)_asmEntryDllOrdinal:X}], {dllOrdinal}",
            $"mov dword [dword 0x{(long)_asmEntryFunctionOrdinal:X}], {functionOrdinal}",

            utilities.GetAbsoluteCallMnemonics<Macros.AsmAction>(HookImpl, out var wrapper),

            // Restore
            Macros.Is64Bit ? Macros.PopSseCallConvRegistersx64 : "", // Pop Microsoft Call Convention SSE Float Registers
            $"{Macros.PopAll}",
            Macros.Is64Bit ? "add rsp, 8" : "", // Stack alignment.
        };

        _wrappers.Add(wrapper);
        return hooks.CreateAsmHook(asmCode, address).Activate();
    }

    /// <summary>
    /// Runs the action and undoes all hooks.
    /// </summary>
    private void HookImpl()
    {
        // Dispose first to prevent potential recursion.
        var entryDll = _dlls[*_asmEntryDllOrdinal];
        _logger.LogWriteLineAsync($"Delay Injection via Entry Point: {entryDll.Name}, {entryDll.Functions[*_asmEntryFunctionOrdinal]}", _logger.ColorInformation);
        Dispose();
        _method?.Invoke();
    }

    /// <summary>
    /// Disables all performed hooks.
    /// </summary>
    private void Dispose()
    {
        foreach (var hook in _hooks)
            hook.Disable();

        _wrappers.Clear();
        NativeMemory.Free(_asmEntryDllOrdinal);
    }

    private struct DllEntry
    {
        public string Name { get; set; }
        public string[] Functions { get; set; }
    }
}