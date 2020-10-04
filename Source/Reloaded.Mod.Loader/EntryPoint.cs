﻿using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Reloaded.Mod.Loader.IO;
using Reloaded.Mod.Loader.Server;
using Reloaded.Mod.Loader.Utilities;
using Reloaded.Mod.Loader.Utilities.Hooking;
using Reloaded.Mod.Shared;
using static System.Environment;
using static Reloaded.Mod.Loader.Utilities.LogMessageFormatter;

namespace Reloaded.Mod.Loader
{
    /// <summary>
    /// Provides an entry point to this .NET Core application for the native C++ bootstrapper.
    /// </summary>
    // ReSharper disable UnusedMember.Global
    public static class EntryPoint
    {
        // DO NOT RENAME THIS CLASS OR ITS PUBLIC METHODS
        private static Stopwatch _stopWatch;
        private static Loader _loader;
        private static Host _server;
        private static MemoryMappedFile _memoryMappedFile;
        private static BasicPeParser _basicPeParser;
        private static IndirectHook<ExitProcess> _exitProcessHook;

        /* Ensures DLL Resolution */
        public static void Main() { } // Dummy for R2R images.
        private static void SetupLoader()
        {
            try
            {
                // Setup mod loader.
                _stopWatch = new Stopwatch();
                _stopWatch.Start();

                AppDomain.CurrentDomain.UnhandledException += LogUnhandledException;
                ExecuteTimed("Create Loader", CreateLoader);
                var createHostTask = Task.Run(() => ExecuteTimed("Create Loader Host (Async)", CreateHost));
                var checkDrmTask   = Task.Run(() => ExecuteTimed("Parsing PE Header, Checking DRM (Async)", PerformPeOperations));
                ExecuteTimed("Loading Mods (Total)", LoadMods);

                checkDrmTask.Wait();
                createHostTask.Wait();
                _loader?.Console?.WriteLineAsync(AddLogPrefix($"Total Loader Initialization Time: {_stopWatch.ElapsedMilliseconds}ms"));
                _stopWatch.Reset();
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        private static void LoadMods()     => _loader.LoadForCurrentProcess();
        private static void CreateLoader() => _loader = new Loader();
        private static void CreateHost()   => _server = new Host(_loader);
        private static unsafe void PerformPeOperations()
        {
            _basicPeParser = new BasicPeParser(Environment.GetCommandLineArgs()[0]);

            // Check for Steam DRM.
            DRMNotifier.PrintWarnings(_basicPeParser, _loader.Console);

            // Hook native import for ExitProcess. (So we can save log on exit)
            if (ImportAddressTable.TryGetFunctionPtrAddress("kernel32.dll", "ExitProcess", out var address))
            {
                _exitProcessHook = new IndirectHook<ExitProcess>(address, SaveLogOnExitProcess).Activate();
            }
        }

        private static void SaveLogOnExitProcess(uint uExitCode)
        {
            _loader?.Console?.WriteLineAsync(AddLogPrefix("ExitProcess Hook: Log End"));
            _loader?.Console?.Shutdown();
            _exitProcessHook.OriginalFunction(uExitCode);
        }

        private static void LogUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = (Exception)e.ExceptionObject;
            var message = $"Unhandled Exception: {exception.Message}\n" +
                          $"Stack Trace: {exception.StackTrace}";

            _loader?.Console?.WriteLine(AddLogPrefix(message), _loader.Console.ColorRed);
        }

        /* Initialize Mod Loader (DLL_PROCESS_ATTACH) */

        /// <summary>
        /// Initializes the mod loader.
        /// Returns the port on the local machine (but that wouldn't probably be used).
        /// </summary>
        public static int Initialize(IntPtr unusedPtr, int unusedSize)
        {
            EnableProfileOptimization();

            // Write port as a Memory Mapped File, to allow Mod Loader's Launcher to discover the mod port.
            // (And to stop bootstrapper from loading loader again).
            int pid             = Process.GetCurrentProcess().Id;
            _memoryMappedFile   = MemoryMappedFile.CreateOrOpen(ServerUtility.GetMappedFileNameForPid(pid), sizeof(int));
            var view            = _memoryMappedFile.CreateViewStream();
            var binaryWriter    = new BinaryWriter(view);
            binaryWriter.Write((int)0);

            // Setup Loader
            SetupLoader();

            // Only write port on completed initialization.
            // If port is 0, assume in loading state
            binaryWriter.Seek(-sizeof(int), SeekOrigin.Current);
            binaryWriter.Write(_server.Port);
            return _server?.Port ?? 0;
        }

        /* Utility Functions */

        /// <summary>
        /// Executes a given function and prints running stats of how long it took to execute.
        /// </summary>
        public static void ExecuteTimed(string text, Action action)
        {
            long initialTime = _stopWatch.ElapsedMilliseconds;
            action();
            _loader?.Console?.WriteLineAsync(AddLogPrefix($"{text} | Time: {_stopWatch.ElapsedMilliseconds - initialTime}ms"));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void HandleException(Exception ex)
        {
            // This method is singled out to avoid loading System.Windows.Forms at startup; because it is lazy loaded.
            var errorMessage = $"Failed to Load Reloaded-II.\n{ex.Message}\n{ex.StackTrace}\nA log is available at: {_loader?.Logger?.FlushPath}";
            _loader?.Console?.WaitForConsoleInit();
            _loader?.Console?.WriteLine(errorMessage, _loader.Console.ColorRed);
            _loader?.Logger?.Flush();
            MessageBox.Show(errorMessage);
        }

        private static void EnableProfileOptimization()
        {
            // Start Profile Optimization
            var profileRoot = Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), LoaderConfigReader.ReloadedFolderName, "ProfileOptimization");
            Directory.CreateDirectory(profileRoot);
            
            // Define the folder where to save the profile files
            ProfileOptimization.SetProfileRoot(profileRoot);

            // Start profiling and save it in Startup.profile
            ProfileOptimization.StartProfile("Loader.profile");
        }
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void ExitProcess(uint uExitCode);
    }
    // ReSharper restore UnusedMember.Global
}
