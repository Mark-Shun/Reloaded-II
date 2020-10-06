﻿using System;
using System.Linq;
using Ninject;
using Reloaded.Mod.Launcher.Misc;
using Reloaded.Mod.Loader.IO;
using Reloaded.Mod.Loader.IO.Config;

namespace Reloaded.Mod.Launcher
{
    public static class IoC
    {
        /// <summary>
        /// The standard NInject Kernel.
        /// </summary>
        public static IKernel Kernel { get; } = new StandardKernel();

        /// <summary>
        /// Sets up the IoC container with any relevant bindings on first access of the class.
        /// </summary>
        static IoC()
        {
            // Setup loader configuration.
            LoaderConfig config;
            try
            {
                config = LoaderConfig.ReadConfiguration();
            }
            catch (Exception ex)
            {
                config = new LoaderConfig();
                config.SanitizeConfig();
                LoaderConfig.WriteConfiguration(config);
                Errors.HandleException(ex, "Failed to parse Reloaded-II launcher configuration.\n" +
                                           "This is a rare bug, your settings have been reset.\n" +
                                           "If you have encountered this please report this to the GitHub issue tracker.\n" +
                                           "Any information on how to reproduce this would be very, very welcome.\n");
            }

            Kernel.Bind<LoaderConfig>().ToConstant(config);
        }

        /// <summary>
        /// Retrieves a service (class) from the IoC of a specified type.
        /// </summary>
        public static T Get<T>()
        {
            return Kernel.Get<T>();
        }

        /// <summary>
        /// Retrieves a constant service/class.
        /// If none is registered, binds it as the new constant to then be re-acquired.
        /// </summary>
        public static T GetConstant<T>()
        {
            var value = Kernel.Get<T>();

            if (! IsExplicitlyBound<T>())
            {
                Kernel.Bind<T>().ToConstant(value);
            }

            return value;
        }

        /// <summary>
        /// Returns true if a type has been bound by the user, else false.
        /// </summary>
        public static bool IsExplicitlyBound<T>()
        {
            return !Kernel.GetBindings(typeof(T)).All(x => x.IsImplicit);
        }
    }
}