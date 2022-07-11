#if !NETFRAMEWORK
using ExitGames.Logging;
using System;
using System.Reflection;
using System.Runtime.Loader;
using System.IO;

namespace Photon.Common.Plugins
{
    internal class PluginAssemblyLoadContext : AssemblyLoadContext
    {
        #region .flds

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly AssemblyDependencyResolver dependencyResolver;
        private readonly string assemblyFolder;
        #endregion

        #region .ctrs

        internal PluginAssemblyLoadContext(AssemblyLoadContext paretnLoadConext, string assemblyFolder, string name = "") : base(name)
        {
            this.ParentContext = paretnLoadConext;
            this.dependencyResolver = new AssemblyDependencyResolver(assemblyFolder);
            this.assemblyFolder = assemblyFolder;
        }

        #endregion

        #region .properties

        public AssemblyLoadContext ParentContext { get; }

        #endregion

        #region .methods

        protected override Assembly Load(AssemblyName assemblyName)
        {
            try
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug($"Context {this} is trying to load '{assemblyName}'");
                }

                if (assemblyName.Name == "PhotonHive"
                    || assemblyName.Name == "PhotonHivePlugin"
                    || assemblyName.Name == "Photon.Plugins.Common")
                {
                    return LoadAssemblyFromParrentContext(assemblyName);
                }
            }
            catch (Exception e)
            {
                log.Error($"Exception during loading of '{assemblyName.FullName}'", e);
            }

            var filePath = this.dependencyResolver.ResolveAssemblyToPath(assemblyName);

            if (!File.Exists(filePath))
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug($"Loading of assembly '{assemblyName.FullName}' skipped. there is no file '{filePath}'");
                }
                return null;
            }

            if (log.IsDebugEnabled)
            {
                log.Debug($"Loading of assembly '{assemblyName.Name}', ctx:{this} from '{filePath}'");
            }

            return this.LoadFromAssemblyPath(filePath);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string libraryPath = this.dependencyResolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }

        private Assembly LoadAssemblyFromParrentContext(AssemblyName assemblyName)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug($"Loading assembly '{assemblyName}' from app context");
            }

            var asm = this.ParentContext.LoadFromAssemblyName(assemblyName);

            if (asm != null)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug($"Assembly '{assemblyName}' are loaded successfully from context: {this.ParentContext}");
                }
            }
            else
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug($"Failed to load assembly '{assemblyName}' from context: {this.ParentContext}");
                }
            }
            return asm;
        }

        public override string ToString()
        {
            return $"PluginCtx: {this.Name}";
        }
        #endregion
    }

    public sealed class PluginAssemblyLoader : IDisposable
    {
        #region .flds
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private readonly PluginAssemblyLoadContext pluginLoadContext;
        #endregion

        #region .ctrs

        public PluginAssemblyLoader(string path, AssemblyLoadContext parentLoadContext, string name = "")
        {
            if (log.IsDebugEnabled)
            {
                log.Debug($"Loading assembly: {path}");
            }

            this.pluginLoadContext = new PluginAssemblyLoadContext(parentLoadContext, path, name);

            this.Assembly = this.pluginLoadContext.LoadFromAssemblyPath(path);
        }

        #endregion

        #region .properties
        public Assembly Assembly { get; }
        #endregion

        #region .methods

        public void Dispose()
        {
        }

        #endregion
    }
}
#endif
