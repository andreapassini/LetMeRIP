// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PluginManager.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------


using Photon.Common.Plugins;
using Photon.Hive.Configuration;
using Photon.Plugins.Common;

namespace Photon.Hive.Plugin
{
    using System;
    using System.IO;
    using System.Text;
    using System.Reflection;
    using System.Collections.Generic;

    using ExitGames.Logging;

    public class PluginTraits
    {
        #region Constants and Fields

        private const string IsPersistentTag = "IsPersistent";
        private const string AsyncJoinTag = "AsyncJoin";

        private bool isPersistent;
        private bool asyncJoin;
        #endregion

        #region Properties

        public bool IsPersistent
        {
            get { return this.isPersistent; }
        }

        public bool AsyncJoin
        {
            get { return this.asyncJoin; }
        }

        public bool AllowAsyncJoin
        {
            get { return this.IsPersistent && this.AsyncJoin; }
        }

        #endregion

        #region Publics

        public static PluginTraits Create(PluginInfo pluginInfo)
        {
            var result = new PluginTraits();

            ReadValue(pluginInfo, IsPersistentTag, ref result.isPersistent);
            if (result.IsPersistent)
            {
                result.asyncJoin = true;//this value will be used if there is no requested key
                ReadValue(pluginInfo, AsyncJoinTag, ref result.asyncJoin);
            }

            return result;
        }

        #endregion

        #region Methods

        private static void ReadValue(PluginInfo pluginInfo, string name, ref bool result)
        {
            var value = FindValue(pluginInfo, name);
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            bool parseResult;
            if (bool.TryParse(value, out parseResult))
            {
                result = parseResult;
            }
        }

        private static string FindValue(PluginInfo pluginInfo, string name)
        {
            var result = string.Empty;
            if (pluginInfo.ConfigParams != null)
            {
                pluginInfo.ConfigParams.TryGetValue(name, out result);
            }
            return result;
        }

        #endregion
    }

    public class PluginInstance : IPluginInstance
    {
        public IGamePlugin Plugin { get; set; }

        public EnvironmentVersion Version { get; set; }
    }

    public class PluginManager : IPluginManager
    {
        #region Constants and Fields

        protected static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        private Dictionary<string, string> pluginConfig;

        private PluginTraits pluginTraits = new PluginTraits();

        private IPluginFactory pluginFactory = null;
        private readonly IPluginLogMessagesCounter logMessagesCounter = NullPluginLogMessageCounter.Instance;

#if NETCOREAPP
        private static readonly System.Runtime.Loader.AssemblyLoadContext globalAppContext =
            System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly());
        private PluginAssemblyLoader assemblyLoader;
#endif

        #endregion

        #region Constructors and Destructors

        public PluginManager(PluginInfo pluginInfo, string basePath, IPluginLogMessagesCounter logMessagesCounter)
        {
            this.logMessagesCounter = logMessagesCounter ?? NullPluginLogMessageCounter.Instance;

            this.UpdateConfiguration(pluginInfo, basePath);
        }

        public PluginManager(string basePath)
        {
            try
            {
                var settings = PluginSettings.Default;
                if (settings.Enabled & settings.Plugins.Count > 0)
                {
                    var pluginSettings = settings.Plugins[0];

                    if (Log.IsInfoEnabled)
                    {
                        Log.InfoFormat("Plugin configured: name={0}", pluginSettings.Name);

                        if (pluginSettings.CustomAttributes.Count > 0)
                        {
                            foreach (var att in pluginSettings.CustomAttributes)
                            {
                                Log.InfoFormat("\tAttribute: {0}={1}", att.Key, att.Value);
                            }
                        }
                    }

                    var pluginInfo = new PluginInfo
                        {
                            Name = pluginSettings.Name, 
                            Version = pluginSettings.Version, 
                            AssemblyName = pluginSettings.AssemblyName,
                            Type = pluginSettings.Type, 
                            ConfigParams = pluginSettings.CustomAttributes
                        };

                    this.UpdateConfiguration(pluginInfo, string.IsNullOrEmpty(basePath) ? Environment.CurrentDirectory : basePath);
                }
                else
                {
                    if (Log.IsInfoEnabled)
                    {
                        Log.Info($"Plugins usage is disabled");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        protected PluginManager()
        {
        }

#if NETFRAMEWORK
        static PluginManager()
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }
#endif        
        #endregion

        #region Properties
        public bool Initialized { get; protected set; }

        public Type Type4Load { get; set; }

        public string PluginPath { get; private set; }

        public PluginTraits PluginTraits { get { return this.pluginTraits; } }

        public EnvironmentVersion EnvironmentVersion { get; private set; }

        #endregion

        #region Implemented Interfaces

        #region IPluginManager

        public virtual IPluginInstance GetGamePlugin(IPluginHost sink, string pluginName)
        {
#if PLUGINS_0_9
            if (this.Type4Load == null)
#else
            if (this.pluginFactory == null)
#endif
            {
                if (string.IsNullOrEmpty(pluginName) || pluginName == "Default")
                {
                    return GetDefaultPlugin(sink);
                }

                return this.LoadErrorPlugin(sink, "PluginManager initialization failed.");
            }

            IGamePlugin plugin;
            try
            {
#if PLUGINS_0_9
                var obj = Activator.CreateInstance(this.Type4Load);
                plugin = (IGamePlugin)obj;
#else
                plugin = this.CreatePluginWithFactory(sink, pluginName);
                //plugin.HostVersion = this.EnvironmentVersion.CurrentVersion;
                //plugin.BuildVersion = this.EnvironmentVersion.BuiltWithVersion;


                return new PluginInstance { Plugin = plugin, Version = this.EnvironmentVersion };

                //return plugin;
#endif

            }
            catch (Exception e)
            {
                var errorMsg = ExceptionToString(e);
                Log.Error(errorMsg);
                return this.LoadErrorPlugin(sink, errorMsg);
            }

#if PLUGINS_0_9
            if (plugin.SetupInstance(sink, this.pluginConfig))
            {
                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("Plugin of type {0} was successfuly created", this.type4Load.Name);
                }

                if (string.IsNullOrEmpty(pluginName) || pluginName == plugin.Name)
                {
                    return plugin;                    
                }
                return this.LoadErrorPlugin(sink, "Plugin missmatch.");
            }
            Log.WarnFormat("Plugin of type {0} was not successfuly setup. Error Message={1}", this.Type4Load.Name, "Plugin internal error");
            return this.LoadErrorPlugin(sink, errorMsg);
#endif
        }

        #endregion

        #endregion

        #region Methods

        public static IPluginInstance GetDefaultPlugin(IPluginHost sink)
        {
            string errorMsg;
            IGamePlugin plugin = new PluginBase();
            plugin.SetupInstance(sink, null, out errorMsg);
            return new PluginInstance {Plugin = plugin, Version = new EnvironmentVersion()};
        }

        public static IPluginInstance GetErrorPlugin(IPluginHost sink, string msg)
        {
            string errorMsg;
            IGamePlugin plugin = new ErrorPlugin(msg);
            plugin.SetupInstance(sink, null, out errorMsg);
            return new PluginInstance {Plugin = plugin, Version = new EnvironmentVersion()};
        }

        public void UpdateConfiguration(PluginInfo pluginInfo, string basePath)
        {
            try
            {
                var path = Path.Combine(basePath, pluginInfo.Path);
                this.UpdatePluginTraits(pluginInfo);
                this.Initialized = this.SetupPluginManager(path, pluginInfo.Type, pluginInfo.ConfigParams);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        private bool SetupPluginManager(string path, string typeName, Dictionary<string, string> config)
        {
            this.pluginConfig = config;
            this.PluginPath = path;

            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("Plugin path is set to '{0}'", this.PluginPath);
            }

            var currentPluginsVersion = typeof(PluginBase).Assembly.GetName().Version;


            try
            {
#if NETFRAMEWORK
                var asm = Assembly.LoadFile(path);
#else
                this.assemblyLoader?.Dispose();
                this.assemblyLoader = new PluginAssemblyLoader(path, globalAppContext);
                var asm = this.assemblyLoader.Assembly;
#endif

                if (Log.IsInfoEnabled)
                {
                    Log.InfoFormat(
                        "Loaded Assembly Name={0}, Version={1}, Culture={2}, PublicKey token={3}, Path={4}",
                        asm.GetName().Name,
                        asm.GetName().Version,
                        asm.GetName().CultureInfo.Name,
                        (BitConverter.ToString(asm.GetName().GetPublicKeyToken())),
                        path);
                }

                var pluginVersion = new Version();
                bool photonHivePluginFound = false;
                foreach (AssemblyName an in asm.GetReferencedAssemblies())
                {
                    Log.InfoFormat(
                        "Referenced Assembly Name={0}, Version={1}, Culture={2}, PublicKey token={3}, Path={4}",
                        an.Name,
                        an.Version,
                        an.CultureInfo.Name,
                        (BitConverter.ToString(an.GetPublicKeyToken())),
                        an.CodeBase);

                    if (an.Name == "PhotonHivePlugin")
                    {
                        pluginVersion = an.Version;
                        photonHivePluginFound = true;
                    }
                }

                if (!photonHivePluginFound)
                {
                    Log.ErrorFormat("Can not load plugin assembly. It does not refer PhotonHivePlugin.dll, assembly:{0}", asm.GetName().Name);
                    return false;
                }

                if (pluginVersion.Major != currentPluginsVersion.Major || pluginVersion.Minor > currentPluginsVersion.Minor)
                {
                    Log.ErrorFormat("Plugins version Missmatch. PhotonHivePlugin Version:{0}; Plugin Name:{1} Version:{2}", currentPluginsVersion, asm.GetName().Name, pluginVersion);
                    return false;
                }

                this.EnvironmentVersion = new EnvironmentVersion
                {
                    BuiltWithVersion = pluginVersion, 
                    HostVersion = currentPluginsVersion
                };

                this.CreateKnownType(typeName, asm);

#if !PLUGINS_0_9
                this.pluginFactory = Activator.CreateInstance(this.Type4Load) as IPluginFactory;

                if (this.pluginFactory == null)
                {
                    Log.ErrorFormat("Type {0} does not implement IPluginFactory interface", this.Type4Load.Name);
                    return false;
                }

                var pluginFactory2 = this.pluginFactory as IPluginFactory2;
                if (pluginFactory2 != null)
                {
                    pluginFactory2.SetFactoryHost(new FactoryHost(this.logMessagesCounter), new FactoryParams { PluginConfig = config });
                }
#endif
                Log.InfoFormat("Plugin manager (version={0}) is setup. type={1};path={2};version={3}", 
                    currentPluginsVersion, typeName, path, pluginVersion);

                return true;
            }
            catch(Exception e)
            {
                Log.Error(string.Format("Got Exception during either loading of assembly {0} or type {1}. Exception Msg: {2}", path, typeName, e.Message), e);
            }
            return false;
        }

        private void UpdatePluginTraits(PluginInfo pluginInfo)
        {
            this.pluginTraits = PluginTraits.Create(pluginInfo);
        }

#if NETFRAMEWORK
        public static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var asm = LoadAssemblyFromApplicationContext(args.Name);

            if (asm != null)
            {
                return asm;
            }

            if (args.RequestingAssembly == null)
            {
                return null;
            }

            var path = GetAssemblyLoadPath(args);

            return LoadAssemblyFromFile(args.Name, path);
        }

        private static string GetAssemblyLoadPath(ResolveEventArgs args)
        {
            var requestingAssemblyPath = args.RequestingAssembly.Location;
            var path = Path.GetDirectoryName(requestingAssemblyPath);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var idx = args.Name.IndexOf(',');
            var asmName = (idx != -1 ? args.Name.Substring(0, idx) : args.Name) + ".dll";
            path = Path.Combine(path, asmName);

            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("Assembly requests assembly. name1:{0}, name2:{2}, path1:{1}, path2:{3}",
                    args.RequestingAssembly.FullName, requestingAssemblyPath, args.Name, path);
                Log.DebugFormat("Loading assembly '{0}' from path '{1}'", args.Name, path);
            }
            return path;
        }

        /// <summary>
        /// this method is called only for netframework version
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static Assembly LoadAssemblyFromApplicationContext(string name)
        {
            if (name.Contains("PhotonHive,")
                || name.Contains("PhotonHivePlugin,")
                || name.Contains("Photon.Plugins.Common"))
            {
                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("Loading assembly '{0}' from Application context", name);
                }

                var asm = Assembly.Load(name);
                if (asm != null)
                {
                    if (Log.IsDebugEnabled)
                    {
                        Log.DebugFormat("Assembly '{0}' are loaded successfully from Application context", name);
                    }
                }
                else
                {
                    if (Log.IsDebugEnabled)
                    {
                        Log.DebugFormat("Failed to load assembly '{0}' from Application context", name);
                    }
                }
                return asm;
            }
            return null;
        }

        private static Assembly LoadAssemblyFromFile(string assemblyName, string path)
        {
            var asm = Assembly.LoadFile(path);

            if (asm != null)
            {
                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("Assembly '{0}' are loaded successfully from '{1}'", assemblyName, path);
                }
            }
            else
            {
                if (Log.IsWarnEnabled)
                {
                    Log.WarnFormat("Failed to load assembly '{0}' from path '{1}'", assemblyName, path);
                }
            }
            return asm;
        }

#endif
        private void CreateKnownType(string typeName, Assembly asm)
        {
            this.Type4Load = asm.GetType(typeName, true);

            Log.InfoFormat("Plugin Type {0} from assembly {1} was successfuly created", typeName, asm.FullName);
        }

        private static string ExceptionToString(Exception exception)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("{0} : {1}\n", exception.GetType().FullName, exception.Message);

            var ex = exception;
            DumpReflectionTypeLoadException(ex as ReflectionTypeLoadException, sb);
            while (ex.InnerException != null)
            {
                ex = ex.InnerException;
                sb.Append("--> ").AppendFormat("{0} : {1}\n", ex.GetType().FullName, ex.Message);
                DumpReflectionTypeLoadException(ex as ReflectionTypeLoadException, sb);
            }
            sb.AppendLine("Stack Trace:");
            sb.AppendLine(exception.StackTrace);
            return sb.ToString();
        }

        private static void DumpReflectionTypeLoadException(ReflectionTypeLoadException loadException, StringBuilder sb)
        {
            if (loadException != null)
            {
                sb.AppendLine("LoaderExceptions property value:");
                foreach (var loaderException in loadException.LoaderExceptions)
                {
                    sb.AppendFormat("[{0} : {1}]", loaderException.GetType().FullName, loaderException.Message).AppendLine();
                }
                sb.AppendLine("End of Loader Exceptions");
            }
        }

        private IGamePlugin CreatePluginWithFactory(IPluginHost sink, string pluginName)
        {
            string errorMsg;
            var plugin = this.pluginFactory.Create(sink, pluginName, this.pluginConfig, out errorMsg);
            if (plugin == null)
            {
                return this.LoadErrorPlugin(sink, errorMsg).Plugin;
            }

            if (Log.IsInfoEnabled)
            {
                Log.InfoFormat("Plugin successfully created:Type:{0}, path:{1}", this.Type4Load, this.PluginPath);
            }

            return plugin;
        }

        private IPluginInstance LoadErrorPlugin(IPluginHost sink, string errorMsg)
        {
            return GetErrorPlugin(sink, errorMsg);
        }

        #endregion
    }
}