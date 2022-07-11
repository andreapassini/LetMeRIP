// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LoadBalancer.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the LoadBalancer type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using ExitGames.Logging;
using Photon.Common.LoadBalancer.Common;
using Photon.Common.LoadBalancer.Configuration;
using Photon.Common.LoadBalancer.LoadShedding;
using Photon.Common.LoadBalancer.Priorities;

namespace Photon.Common.LoadBalancer
{
    #region using directives

    

    #endregion

    /// <summary>
    ///   Represents a collection of server instances which can be accessed
    ///   randomly based on their current lod level. 
    /// </summary>
    /// <typeparam name = "TServer">
    ///   The type of the server instances.
    /// </typeparam>
    /// <remarks>
    /// Each server instance gets a weight assigned based on the current load level of that server. 
    /// The TryGetServer method gets a random server based on this weight. A server with a higher
    /// weight will be returned more often than a server with a lower weight.
    /// The default values for this weights are the following:
    /// 
    /// LoadLevel.Lowest  = 40
    /// LoadLevel.Low     = 30
    /// LoadLevel.Normal  = 20
    /// LoadLevel.High    = 10
    /// LoadLevel.Highest = 0
    /// 
    /// If there is for example one server for eac load level, the server with load level lowest 
    /// will be returned 50% of the times, the one with load level low 30% and so on. 
    /// </remarks>
    public class LoadBalancer<TServer> where TServer : IComparable<TServer>
    {
        #region Constants and Fields
        // Event triggering on add/remove/update server
        public event EventHandler ServerListUpdated;

        // ReSharper disable StaticFieldInGenericType
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private static readonly Func<TServer, bool> trueFilter = s => { return true; };

        // ReSharper restore StaticFieldInGenericType

        // dictionary for fast server instance lookup
        //        private readonly Dictionary<TServer, ServerStateData<TServer>> serverList;
        private readonly SortedDictionary<int, ServerBunch<TServer>> servers;

        // stores the available server instances ordered by their weight
        private readonly LinkedList<ServerStateData<TServer>> availableServers = new LinkedList<ServerStateData<TServer>>();

        // list of the weights for each possible load level
        private int[] loadLevelWeights;

        // pseudo-random number generator for getting a random server
        private readonly Random random;

        // stores the sum of the weights of all server instances
        private int serversInUseWeight;

        // stores the sum of the load levels of all server instances
        // used to calculate the average load level
        private int totalWorkload;

        private int serversInUseWorkload;

        // watch files 
        private readonly FileSystemWatcher fileWatcher;

        private int serverCount;

        private int currentPriority;

        private FeedbackLevel priorityUpThreshold = FeedbackLevel.Highest;
        private FeedbackLevel priorityDownThreshold = FeedbackLevel.Highest;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        ///   Initializes a new instance of the <see cref = "LoadBalancer{TServer}" /> class. Use default weights for each load level. 
        /// </summary>
        public LoadBalancer()
        {
            this.random = new Random(); 
            this.servers = new SortedDictionary<int, ServerBunch<TServer>>();
            this.loadLevelWeights = DefaultConfiguration.GetDefaultWeights();
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref = "LoadBalancer{TServer}" /> class.
        /// </summary>
        /// <param name="configFilePath">
        /// The full path (absolute or relative) to a config file that specifies a Weight for each LoadLevel. 
        /// The possible load levels and their values are defined  int the 
        /// <see cref="FeedbackLevel"/> enumeration. 
        /// See the LoadBalancing.config for an example.
        /// </param>
        public LoadBalancer(string configFilePath)
        {
            this.servers = new SortedDictionary<int, ServerBunch<TServer>>();

            this.random = new Random();

            this.InitializeFromConfig(configFilePath);

            string fullPath = Path.GetFullPath(configFilePath);
            string path = Path.GetDirectoryName(fullPath);
            if (path == null)
            {
                log.InfoFormat("Could not watch for configuration file. No path specified.");
                return;
            }

            string filter = Path.GetFileName(fullPath);
            if (filter == null)
            {
                log.InfoFormat("Could not watch for configuration file. No file specified.");
                return;
            }

            this.fileWatcher = new FileSystemWatcher(path, filter);
            this.fileWatcher.Changed += this.ConfigFileChanged;
            this.fileWatcher.Created += this.ConfigFileChanged;
            this.fileWatcher.Deleted += this.ConfigFileChanged;
            this.fileWatcher.Renamed += this.ConfigFileChanged;
            this.fileWatcher.EnableRaisingEvents = true;
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref = "LoadBalancer{TServer}" /> class.
        /// </summary>
        /// <param name="loadLevelWeights">
        /// A list of weights which should be used for each available load level.
        /// This list must contain a value for each available load level and
        /// must be ordered by the load levels value. 
        /// The possible load levels and their values are defined  int the 
        /// <see cref="FeedbackLevel"/> enumeration.
        /// </param>
        public LoadBalancer(int[] loadLevelWeights)
        {
            if (loadLevelWeights == null)
            {
                throw new ArgumentNullException("loadLevelWeights");
            }

            const int feedbackLevelCount = (int)FeedbackLevel.LEVELS_COUNT;
            if (loadLevelWeights.Length != feedbackLevelCount)
            {
                throw new ArgumentOutOfRangeException(
                    "loadLevelWeights", 
                    string.Format(
                        "Parameter loadLevelWeights must have a length of {0}. One weight for each possible load level", 
                        feedbackLevelCount));
            }

            this.servers = new SortedDictionary<int, ServerBunch<TServer>>();
            this.random = new Random();
            this.loadLevelWeights = loadLevelWeights;
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref = "LoadBalancer{TServer}" /> class.
        /// </summary>
        /// <remarks>
        /// This overload is used for unit testing to provide a fixed seed for the 
        /// random number generator.
        /// </remarks>
        public LoadBalancer(int[] loadLevelWeights, int seed, 
            FeedbackLevel priorityUpThreshold = FeedbackLevel.Highest, FeedbackLevel priorityDownThreshold = FeedbackLevel.Highest)
            : this(loadLevelWeights)
        {
            this.PriorityUpThreshold = priorityUpThreshold;
            this.priorityDownThreshold = priorityDownThreshold;
            this.random = new Random(seed);
        }

        #endregion

        #region Delegates


        #endregion

        #region Properties

        /// <summary>
        ///   Gets the average workload of all server instances.
        /// </summary>
        public FeedbackLevel AverageWorkload
        {
            get
            {
                if (this.serverCount == 0)
                {
                    return FeedbackLevel.Highest;
                }

                return (FeedbackLevel)(int)Math.Round((double)this.totalWorkload / this.serverCount);
            }
        }

        /// <summary>
        /// we use this property only for tests
        /// </summary>
        public FeedbackLevel AverageWorkloadForAvailableServers
        {
            get
            {
                if (this.serverCount == 0)
                {
                    return FeedbackLevel.Highest;
                }

                return (FeedbackLevel)(int)Math.Round((double)this.totalWorkload / this.availableServers.Count);
            }
        }

        public int AverageWorkloadPercentage
        {
            get
            {
                lock (this.servers)
                {
                    if (this.serverCount == 0)
                    {
                        return 100;
                    }

                    return (int) Math.Round((double) this.totalWorkload/(this.serverCount*(int) FeedbackLevel.LEVELS_COUNT)*100);
                }
            }
        }

        public bool HasAvailableServers
        {
            get
            {
                lock (this.servers)
                {
                    return this.availableServers.Count != 0;
                }
            }
        }

        public int TotalWorkload
        {
            get
            {
                return this.totalWorkload; 
            }
        }

        public int ServersInUseWeight
        {
            get
            {
                return this.serversInUseWeight;
            }
        }

        public int[] LoadLevelWeights
        {
            get
            {
                return this.loadLevelWeights;
            }
        }

        public FeedbackLevel PriorityDownThreshold
        {
            get { return this.priorityDownThreshold; }
            set { this.priorityDownThreshold = value; }
        }

        public FeedbackLevel PriorityUpThreshold
        {
            get { return this.priorityUpThreshold; }
            set { this.priorityUpThreshold = value; }
        }

        public float ReserveRatio { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        ///   Attempts to add a server instance.
        /// </summary>
        /// <param name = "server">The server instance to add.</param>
        /// <param name = "loadLevel">The current workload of the server instance.</param>
        /// <param name="priority">Priority of server</param>
        /// <returns>
        ///   True if the server instance was added successfully. If the server instance already exists, 
        ///   this method returns false.
        /// </returns>
        public bool TryAddServer(TServer server, FeedbackLevel loadLevel, byte priority = 0)
        {
            lock (this.servers)
            {
                var bunch = this.GetOrAddBunch(priority);

                if (log.IsDebugEnabled)
                {
                    log.Debug($"Adding server: server={server} workload={loadLevel}, priority={priority}");
                }

                var serverState = bunch.TryAddServer(server, loadLevel, this.GetLoadLevelWeight(loadLevel));
                if (serverState == null)
                {
                    return false;
                }

                bunch.UpdateTotalWorkload(serverState, FeedbackLevel.Lowest, loadLevel);
                this.UpdateTotalWorkload(FeedbackLevel.Lowest, loadLevel);

                ++this.serverCount;

                var outOfReserve = bunch.UpdateReserve(serverState);
                if (outOfReserve != null)
                {
                    serverState = outOfReserve;// now server is in reserve and we will not be able to add to list of available servers anyway
                }

                if (priority <= this.currentPriority && serverState.Weight > 0 && !serverState.IsReserved)
                {
                    this.AddToAvailableServers(serverState);
                }

                if (priority <= this.currentPriority)
                {
                    this.UpdateLoadBalancerState();
                }

                if (log.IsDebugEnabled)
                {
                    log.Debug($"Added server: server={serverState} workload={loadLevel}");
                }

                //Notify that server list changed
                this.OnServerListUpdatedEvent(null);
                return true;
            }
        }

        /// <summary>
        ///   Tries to get a free server instance.
        /// </summary>
        /// <param name = "server">
        ///   When this method returns, contains an available server instance 
        ///   or null if no available server instances exists.
        /// </param>
        /// <returns>
        ///   True if a server instance with enough remaining workload is found; otherwise false.
        /// </returns>
        public bool TryGetServer(out TServer server)
        {
            return this.TryGetServer(out server, trueFilter);
        }

        /// <summary>
        ///   Tries to get a free server instance.
        /// </summary>
        /// <param name = "server">
        ///   When this method returns, contains the server instance with the fewest workload
        ///   or null if no server instances exists.
        /// </param>
        /// <param name = "loadLevel">
        ///   When this method returns, contains an available server instance 
        ///   or null if no available server instances exists.
        /// </param>
        /// <returns>
        ///   True if a server instance with enough remaining workload is found; otherwise false.
        /// </returns>
        public bool TryGetServer(out TServer server, Func<TServer, bool> filter)
        {
            lock (this.servers)
            {
                if (this.availableServers.Count == 0)
                {
                    //loadLevel = FeedbackLevel.Highest;
                    server = default(TServer);
                    return false;
                }

                // Get a random weight between 0 and the sum of the weight of all server instances
                var randomWeight = this.random.Next(this.serversInUseWeight);
                int weight = 0;

                // Iterate through the server instances and add sum the weights of each instance.
                // If the sum of the weights is greater than the generated random value
                // the current server instance in the loop will be returned.
                // Using this method ensures that server instances with a higher weight will
                // be hit more often than one with a lower weight.
                var node = this.availableServers.First;
                while (node != null)
                {
                    if(node.Value.ServerState != ServerState.Normal)
                    {
                        continue;
                    }

                    weight += node.Value.Weight;
                    if (weight > randomWeight && filter(node.Value.Server))
                    {
                        server = node.Value.Server;
                        //loadLevel = node.Value.LoadLevel;
                        return true;
                    }

                    node = node.Next;
                }

                log.WarnFormat("Failed to get a server instance based on the weights");

                node = this.availableServers.First;
                while (node != null)
                {
                    if (node.Value.ServerState != ServerState.Normal)
                    {
                        continue;
                    }

                    if (filter(node.Value.Server))
                    {
                        server = node.Value.Server;
                        return true;
                    }

                    node = node.Next;
                }

                // this should never happen but better log out a warning and 
                // return an available server instance
                log.WarnFormat("Failed to get a server instance based on the weights and filter");
                server = default(TServer);// this.availableServers.First.Value.Server;
                //loadLevel = this.availableServers.First.Value.LoadLevel;
                return false;
            }
        }

        /// <summary>
        ///   Tries to remove a server instance.
        /// </summary>
        /// <param name = "server">The server instance to remove.</param>
        /// <param name="priority">Server priority</param>
        /// <returns>
        ///   True if the server instance was removed successfully. 
        ///   If the server instance does not exist, this method returns false.
        /// </returns>
        public bool TryRemoveServer(TServer server, byte priority = 0)
        {
            lock (this.servers)
            {
                var bunch = this.GetBunch(priority);
                if (bunch == null)
                {
                    return false;
                }

                ServerStateData<TServer> serverState;
                if (bunch.TryGetServer(server, out serverState) == false)
                {
                    return false;
                }

                ServerStateData<TServer> takenFromReserve;
                bunch.RemoveServer(serverState, out takenFromReserve);
                --this.serverCount;

                this.RemoveFromAvailableServers(serverState);
                if (takenFromReserve != null)
                {
                    // we got new server from reserve. add to list
                    this.AddToAvailableServers(takenFromReserve);
                }

                bunch.UpdateTotalWorkload(serverState, serverState.LoadLevel, FeedbackLevel.Lowest);
                this.UpdateTotalWorkload(serverState.LoadLevel, FeedbackLevel.Lowest);

                if (serverState.Priority <= this.currentPriority)
                {
                    this.UpdateLoadBalancerState();
                }

                if (log.IsDebugEnabled)
                {
                    log.DebugFormat("Removed server: workload={0}", serverState.LoadLevel);
                }

                //Notify that server list changed
                this.OnServerListUpdatedEvent(null);
                return true;
            }
        }

        /// <summary>
        ///   Tries to update a server instance.
        /// </summary>
        /// <param name = "server">The server to update.</param>
        /// <param name = "newLoadLevel">The current workload of the server instance.</param>
        /// <param name="priority">Server priority</param>
        /// <returns>
        ///   True if the server instance was updated successfully. 
        ///   If the server instance does not exist, this method returns false.
        /// </returns>
        public bool TryUpdateServer(TServer server, FeedbackLevel newLoadLevel, byte priority = 0, ServerState state = ServerState.Normal)
        {
            lock (this.servers)
            {
                var bunch = this.GetBunch(priority);
                if (bunch == null)
                {
                    return false;
                }

                // check if server instance exits
                ServerStateData<TServer> serverState;
                if (bunch.TryGetServer(server, out serverState) == false)
                {
                    return false;
                }

                // check if load level or server state has changed
                if (serverState.LoadLevel == newLoadLevel && serverState.ServerState == state)
                {
                    return true;
                }

                if (log.IsDebugEnabled)
                {
                    log.Debug($"Updating server: server={serverState}, oldWorkload={serverState.LoadLevel}, newWorkload={newLoadLevel}");
                }

                var oldLoad = serverState.LoadLevel;
                var oldState = serverState.ServerState;

                serverState.LoadLevel = newLoadLevel;
                serverState.ServerState = state;

                var newWeight = this.GetLoadLevelWeight(newLoadLevel);

                // check if the weight for the server instance has changed
                // if it has not changed we don't have to update the list of available servers
                if (serverState.Weight != newWeight && !serverState.IsInReserve)
                {
                    if (serverState.Priority <= this.currentPriority)
                    {
                        this.RemoveFromAvailableServers(serverState);

                        serverState.Weight = newWeight;
                        if (serverState.Weight > 0)
                        {
                            this.AddToAvailableServers(serverState);
                        }
                    }
                    else
                    {
                        serverState.Weight = newWeight;
                    }
                }

                // apply new state
                this.UpdateTotalWorkload(oldLoad, newLoadLevel);
                bunch.UpdateTotalWorkload(serverState, oldLoad, newLoadLevel);

                if (serverState.Priority <= this.currentPriority)
                {
                    this.UpdateLoadBalancerState();
                }

                if (oldState != ServerState.Normal && serverState.ServerState == ServerState.Normal 
                    && serverState.Weight > 0 && !this.availableServers.Contains(serverState)
                    && !serverState.IsInReserve)
                {
                    this.AddToAvailableServers(serverState);
                }

                if (serverState.ServerState != ServerState.Normal)
                {
                    this.RemoveFromAvailableServers(serverState);
                    if (log.IsDebugEnabled)
                    {
                        log.Debug($"Removing server from available servers since state changed. server={serverState}, new state={serverState.ServerState}");
                    }
                }
                //Notify that server list changed
                this.OnServerListUpdatedEvent(null);
                return true;
            }
        }

        /// <summary>
        /// Get states of all stored servers regardless of their status (online/offline/oor)
        /// </summary>
        /// <returns></returns>
        public IList<ServerStateData<TServer>> GetServerStates()
        {
            var result = new List<ServerStateData<TServer>>();
            lock (this.servers)
            {
                foreach (var bunch in this.servers)
                {
                    foreach (var server in bunch.Value.GetServers())
                    {
                        result.Add(server);
                    }
                }
            }
            return result;
        }

        public void DumpState()
        {
            log.WarnFormat("LoadBalancer servers count {0}. Available server count {1}", 
                this.serverCount, 
                this.availableServers.Count);
        }

        #endregion

        #region Methods

        private void OnServerListUpdatedEvent(EventArgs e)
        {
            EventHandler handler = ServerListUpdated;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private void InitializeFromConfig(string configFilePath)
        {
            string message;
            LoadBalancerSection section;

            int[] weights = null;

            if (!ConfigurationLoader.TryLoadFromFile(configFilePath, out section, out message))
            {
                log.WarnFormat(
                    "Could not initialize LoadBalancer from configuration: Invalid configuration file {0}. Using default settings... ({1})",
                    configFilePath,
                    message);
            }

            if (section != null)
            {
                this.ReserveRatio = section.ReserveRatio;

                this.priorityDownThreshold = section.ValueDown;
                if (section.ValueDown == FeedbackLevel.Highest)
                {
                    this.priorityDownThreshold = section.ValueUp == 0 ? section.ValueUp : section.ValueUp - 1;
                }
                this.PriorityUpThreshold = section.ValueUp;

                // load weights from config file & sort:
                var dict = new SortedDictionary<int, int>();
                foreach (LoadBalancerWeight weight in section.LoadBalancerWeights)
                {
                    dict.Add((int)weight.Level, weight.Value);
                }

                if (dict.Count == (int)FeedbackLevel.Highest + 1)
                {
                    weights = new int[dict.Count];
                    dict.Values.CopyTo(weights, 0);

                    log.InfoFormat("Initialized Load Balancer from configuration file: {0}", configFilePath);
                }
                else
                {
                    log.WarnFormat(
                        "Could not initialize LoadBalancer from configuration: {0} is invalid - expected {1} entries, but found {2}. Using default settings...",
                        configFilePath,
                        (int)FeedbackLevel.Highest + 1,
                        dict.Count);
                }
            }

            if (weights == null)
            {
                weights = DefaultConfiguration.GetDefaultWeights();
            }

            this.loadLevelWeights = weights;
        }

        private void UpdateTotalWorkload(FeedbackLevel oldLoadLevel, FeedbackLevel newLoadLevel)
        {
            this.totalWorkload -= (int)oldLoadLevel;
            this.totalWorkload += (int)newLoadLevel;
        }

        private int GetLoadLevelWeight(FeedbackLevel loadLevel)
        {
            return this.loadLevelWeights[(int)loadLevel]; 
        }

        private void AddToAvailableServers(ServerStateData<TServer> serverState)
        {
            this.serversInUseWeight += serverState.Weight;

            serverState.Node = this.availableServers.AddLast(serverState);
        }

        private void RemoveFromAvailableServers(ServerStateData<TServer> serverState)
        {
            if (serverState.Node == null)
            {
                return;
            }

            this.serversInUseWeight -= serverState.Weight;
            this.availableServers.Remove(serverState.Node);
            serverState.Node = null;
        }

        private void ConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            log.InfoFormat("Configuration file for LoadBalancer Weights {0}\\{1} {2}. Reinitializing...", e.FullPath, e.Name, e.ChangeType);
            
            this.InitializeFromConfig(e.FullPath);
            
            this.UpdateWeightForAllServers();
        }

        private void UpdateWeightForAllServers()
        {
            lock (this.servers)
            {
                foreach (var serverBunch in this.servers.Values)
                {
                    foreach (var serverState in serverBunch.GetServers())
                    {
                        var newWeight = this.GetLoadLevelWeight(serverState.LoadLevel);

                        // check if the weight for the server instance has changes
                        // if it has not changed we don't have to update the list of available servers
                        if (newWeight == serverState.Weight)
                        {
                            if (log.IsDebugEnabled)
                            {
                                log.DebugFormat("LoadBalancer Weight did NOT change for server {0}: loadLevel={1}, weight={2}",
                                    serverState.Server, serverState.LoadLevel, serverState.Weight);
                            }
                        }
                        else if (serverState.IsInAvailableList)
                        {
                            if (log.IsDebugEnabled)
                            {
                                log.DebugFormat(
                                    "LoadBalancer Weight did change for server {0}: loadLevel={1}, oldWeight={2}, newWeight={3}",
                                    serverState.Server,
                                    serverState.LoadLevel,
                                    serverState.Weight,
                                    newWeight);
                            }

                            this.RemoveFromAvailableServers(serverState);
                            serverState.Weight = newWeight;

                            if (serverState.Weight > 0)
                            {
                                this.AddToAvailableServers(serverState);
                            }
                        }
                    }
                }
            }
        }

        private ServerBunch<TServer> GetOrAddBunch(byte priority)
        {
            ServerBunch<TServer> output;
            this.servers.TryGetValue(priority, out output);
            if (output == null)
            {
                output = new ServerBunch<TServer>(priority, this.ReserveRatio, this.priorityUpThreshold);
                this.servers[priority] = output;
            }
            return output;
        }

        private ServerBunch<TServer> GetBunch(int priority)
        {
            ServerBunch<TServer> output;
            this.servers.TryGetValue(priority, out output);
            return output;
        }

        private int CalcCurrentPriority(out FeedbackLevel level, out FeedbackLevel levelOfTheRest)
        {
            ServerBunch<TServer> lastBunch = null;
            int levelOfTheRestCalc = 0;
            int orderNumber = 1;
            foreach (var serverBunch in this.servers)
            {
                if (serverBunch.Value.ServersCount != 0)
                {
                    level = serverBunch.Value.AverageWorkload;
                    levelOfTheRestCalc += (int)serverBunch.Value.AverageWorkload;
                    if (level < this.PriorityUpThreshold)
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.Debug($"new candidate for current priority is {serverBunch.Value.Priority} with load={level} and load of the rest={(FeedbackLevel)levelOfTheRestCalc}");
                        }
                        // we calculate here average load of servers without this serverBunch. Server that will take over load in case we take out this 'serverBunch'
                        levelOfTheRest = (FeedbackLevel)(levelOfTheRestCalc /(double)orderNumber);
                        return serverBunch.Value.Priority;
                    }

                    ++orderNumber;
                }

                lastBunch = serverBunch.Value;
            }
            level = FeedbackLevel.Highest;
            levelOfTheRest = FeedbackLevel.Highest;
            return lastBunch != null ? lastBunch.Priority : 0;
        }

        private void UpdateLoadBalancerState()
        {
            this.TryTakeServerFromReserve();

            FeedbackLevel bunchLoadLevel;
            FeedbackLevel restLoadLevel;
            var newPriority = this.CalcCurrentPriority(out bunchLoadLevel, out restLoadLevel);

            if (newPriority == this.currentPriority)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug("new candidate for current priority is equal to current");
                }
                this.TryReturnServerToReserve();
                return;
            }

            if (newPriority < this.currentPriority)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug($"new priority is less than current. we try to get rid of extra servers. new priority={newPriority}, current={this.currentPriority}");
                }
                if (bunchLoadLevel > this.priorityDownThreshold)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug($"candidate for current priority has load higher then valueDown. priority={newPriority}, load={bunchLoadLevel}. current is still={this.currentPriority}");
                    }
                    return;
                }

                if (restLoadLevel > this.priorityDownThreshold)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug($"rest of servers have load higher then valueDown. priority={newPriority}, load={restLoadLevel}. current is still={this.currentPriority}");
                    }
                    return;
                }

                if (log.IsDebugEnabled)
                {
                    log.Debug($"new priority is accepted. priority={newPriority}, load={restLoadLevel}.");
                }
                for (var i = newPriority + 1; i <= this.currentPriority; ++i)
                {
                    var serverBunch = this.GetBunch(i);
                    if (serverBunch != null)
                    {
                        this.RemoveServersFromAvailableListOnPriorityChange(serverBunch);
                    }
                }
            }
            else
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug($"new priority is greater than current. we try to add extra servers. new priority={newPriority}, current={this.currentPriority}");
                }
                for (var i =  this.currentPriority + 1; i <= newPriority; ++i)
                {
                    var serverBunch = this.GetBunch(i);
                    if (serverBunch != null)
                    {
                        this.AddServersToAvailableListOnPriorityChange(serverBunch);
                    }
                }
            }

            this.currentPriority = newPriority;

            this.TryReturnServerToReserve();
        }

        private void TryReturnServerToReserve()
        {
            var currentBunch = this.GetBunch(this.currentPriority);

            var serverReturnThreshold = Math.Min(currentBunch.ReturnToReserveThreshold, (int)this.PriorityDownThreshold);

            if (currentBunch.ServersInUseAverageWorkload <= serverReturnThreshold)
            {
                var serverFromReserve = currentBunch.GetFirstReservedServerInUsage();
                if (serverFromReserve != null)
                {
                    this.RemoveFromAvailableServers(serverFromReserve);
                    currentBunch.ReturnServerIntoReserve(serverFromReserve);
                }
            }
        }

        private void TryTakeServerFromReserve()
        {
            var currentBunch = this.GetBunch(this.currentPriority);

            if (currentBunch.ServersInUseAverageWorkload >= (int) this.PriorityUpThreshold)
            {
                var serverFromReserve = currentBunch.GetServerFromReserve();
                if (serverFromReserve != null)
                {
                    this.AddToAvailableServers(serverFromReserve);
                }
            }
        }

        private void RemoveServersFromAvailableListOnPriorityChange(ServerBunch<TServer> serverBunch)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug($"Removing priority {serverBunch.Priority} with load {serverBunch.AverageWorkload} from available servers");
            }
            foreach (var server in serverBunch.GetServers())
            {
                if (server.IsInAvailableList)
                {
                    this.RemoveFromAvailableServers(server);
                    server.Node = null;
                    if (server.IsReserved)
                    {
                        serverBunch.ReturnServerIntoReserve(server);
                    }
                }
            }
        }

        private void AddServersToAvailableListOnPriorityChange(ServerBunch<TServer> serverBunch)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug($"Adding priority {serverBunch.Priority} with load {serverBunch.AverageWorkload} to available servers");
            }

            foreach (var server in serverBunch.GetServers())
            {
                if (/*!server.IsInAvailableList && */server.Weight > 0 && !server.IsReserved)
                {
                    this.AddToAvailableServers(server);
                }
            }
        }

        private FeedbackLevel GetAverageServersInUseWorkload()
        {
            return (FeedbackLevel)Math.Round((double) this.serversInUseWorkload / this.availableServers.Count);
        }

        #endregion
    }
}