/*
 *  Copyright 2016-2025 Michael Zillgith
 *
 *  This file is part of lib60870.NET
 *
 *  lib60870.NET is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  lib60870.NET is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with lib60870.NET.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  See COPYING file for the complete license text.
 */
#define CONFIG_USE_SEMAPHORES
using lib60870.CS101;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace lib60870.CS104
{
    /// <summary>
    /// Server events concerning listening
    /// </summary>
    public class ServerStateEvent {

        private bool nowListening;
        private bool fatal;
        private Exception occurredException;
        /// <summary>
        /// Either true (by Start()) or false (by Stop() or exception)
        /// </summary>
        public bool NowListening
        {
            get { return nowListening; }
        }
        /// <summary>
        /// Is it a fatal exception (while listening) or not (no exception or while closing)
        /// </summary>
        public bool Fatal
        {
            get { return fatal; }
        }
        /// <summary>
        /// Either null (by Start() and Stop() or caught exception)
        /// </summary>
        public Exception OccurredException
        {
            get { return occurredException; }
        }
        internal ServerStateEvent(bool nowListening, bool fatal, Exception occurredException)
        {
            this.nowListening = nowListening;
            this.fatal = fatal;
            this.occurredException = occurredException;
        }
    }
    public delegate void ServerStateEventHandler(object parameter, ServerStateEvent stateEvent);

    /// <summary>
    /// Connection request handler is called when a client tries to connect to the server.
    /// </summary>
    /// <param name="parameter">User provided parameter</param>
    /// <param name="ipAddress">IP address of the connecting client</param>
    /// <returns>true if the connection has to be accepted, false otherwise</returns>
    public delegate bool ConnectionRequestHandler(object parameter, IPAddress ipAddress);

    /// <summary>
    /// Connection events for the Server
    /// </summary>
    /// 

    public enum ClientConnectionEvent
    {
        /// <summary>
        /// A new connection is opened
        /// </summary>
        OPENED,

        /// <summary>
        /// The connection entered active state
        /// </summary>
        ACTIVE,

        /// <summary>
        /// The connection entered inactive state
        /// </summary>
        INACTIVE,

        /// <summary>
        /// The connection is closed
        /// </summary>
        CLOSED
    }


    public delegate void ConnectionEventHandler(object parameter, ClientConnection connection, ClientConnectionEvent eventType);

    /// <summary>
    /// Server mode (redundancy group support)
    /// </summary>
    public enum ServerMode
    {
        /// <summary>
        /// There is only one redundancy group. There can only be one active connections.
        /// All other connections are standy connections.
        /// </summary>
        SINGLE_REDUNDANCY_GROUP,

        /// <summary>
        /// Every connection is an own redundancy group. This enables simple multi-client server.
        /// </summary>
        CONNECTION_IS_REDUNDANCY_GROUP,

        /// <summary>
        /// Mutliple redundancy groups. Each redundancy group can have only a single active connection.
        /// Each redundancy group has its own event queue.
        /// </summary>
        MULTIPLE_REDUNDANCY_GROUPS
    }

    /// <summary>
    /// Specifies queue behavior when queue is full
    /// </summary>
    public enum EnqueueMode
    {
        /// <summary>
        /// Remove the oldest ASDU from the queue and add the new ASDU.
        /// </summary>
        REMOVE_OLDEST,

        /// <summary>
        /// Don't add the new ASDU when the queue is full.
        /// </summary>
        IGNORE,

        /// <summary>
        /// Don't add the new ASDU when the queue is full and throw an exception.
        /// </summary>
        THROW_EXCEPTION
    }

    /// <summary>
    /// Representation of a redundancy group. A redundancy group is a group of connections that share a unique
    /// event queue. Only one connection in a redundancy group can be active.
    /// </summary>
    public class RedundancyGroup
    {
        internal ASDUQueue asduQueue = null;
        internal Server server = null;

        private string name = "";

        // if list is empty this is the "catch-all" redundancy group that handles clients
        // that are not assigned to a specific redundancyGroup
        private List<IPAddress> AllowedClients = null;

        private List<ClientConnection> connections = new List<ClientConnection>();

        public RedundancyGroup()
        {
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="lib60870.CS104.RedundancyGroup"/> class.
        /// </summary>
        /// <param name="name">an optional name for debugging purposes.</param>
        public RedundancyGroup(string name)
        {
            this.name = name;
        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>the name, or null if no name is set</value>
        public string Name
        {
            get { return name; }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is a catch all group.
        /// All clients that are not explicitely assigned to a specific group are handled
        /// by the catch all group.
        /// </summary>
        /// <value><c>true</c> if this instance is a catch all group; otherwise, <c>false</c>.</value>
        public bool IsCatchAll
        {
            get
            {
                if (AllowedClients == null)
                    return true;
                else
                    return false;
            }
        }

        /// <summary>
        /// Adds a client specified by the IP address
        /// </summary>
        /// <param name="ipAddress">IP address of the client.</param>
        public void AddAllowedClient(IPAddress ipAddress)
        {
            if (AllowedClients == null)
                AllowedClients = new List<IPAddress>();

            AllowedClients.Add(ipAddress);
        }

        /// <summary>
        /// Adds a client specified by the IP address
        /// </summary>
        /// <param name="ipAddress">IP address of the client.</param>
        public void AddAllowedClient(string ipString)
        {
            IPAddress ipAddress = IPAddress.Parse(ipString);

            AddAllowedClient(ipAddress);
        }

        internal void AddConnection(ClientConnection connection)
        {
            connections.Add(connection);
        }


        internal void RemoveConnection(ClientConnection connection)
        {
            connections.Remove(connection);
        }

        internal bool Matches(IPAddress ipAddress)
        {
            bool matches = false;

            if (AllowedClients != null)
            {
                foreach (IPAddress allowedClient in AllowedClients)
                {
                    if (allowedClient.Equals(ipAddress))
                    {
                        matches = true;
                        break;
                    }
                }
            }

            return matches;
        }

        private bool HasConnection(ClientConnection con)
        {
            foreach (ClientConnection connection in connections)
            {
                if (connection == con)
                {
                    return true;
                }
            }

            return false;
        }

        internal void Activate(ClientConnection activeConnection)
        {
            if (HasConnection(activeConnection))
            {
                foreach (ClientConnection connection in connections)
                {
                    if (connection != activeConnection)
                    {
                        if (connection.IsActive)
                        {
                            server.CallConnectionEventHandler(connection, ClientConnectionEvent.INACTIVE);
                            connection.IsActive = false;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Enqueues the ASDU to the redundancy group specific message queue.
        /// This function is called by <see cref="lib60870.CS104.Server.EnqueueASDU"/>. If the Server.EnqueuASDU method
        /// is used this method should not be called!
        /// </summary>
        /// <param name="asdu">The ASDU to enqueue.</param>
        public void EnqueueASDU(ASDU asdu)
        {
            if (asduQueue != null)
            {
                asduQueue.EnqueueAsdu(asdu);
            }
        }
    }

    /// <summary>
    /// This class represents a single IEC 60870-5 server (slave or controlled station). It is also the
    /// main access to the CS 104 server API.
    /// </summary>
    public class Server : CS101.Slave
    {
        private string localHostname = "0.0.0.0";
        private int localPort = 2404;

        private bool running = false;

        private Socket listeningSocket;

        private int maxQueueSize = 1000;
        private int maxHighPrioQueueSize = 1000;
        private int maxOpenConnections = 10;

        private static readonly SemaphoreSlim queueLock = new SemaphoreSlim(1, 1);

        internal int? fileTimeout = null;

        private int receiveTimeoutInMs = 1000; /* maximum allowed time between SOF byte and last message byte */

        private List<RedundancyGroup> redGroups = new List<RedundancyGroup>();



        private ServerMode serverMode = ServerMode.SINGLE_REDUNDANCY_GROUP;

        /// <summary>
        /// Gets or sets the server mode (behavior regarding redundancy groups)
        /// </summary>
        /// <value>The server mode.</value>
        public ServerMode ServerMode
        {
            get { return serverMode; }
            set { serverMode = value; }
        }



        private EnqueueMode enqueueMode = EnqueueMode.REMOVE_OLDEST;

        /// <summary>
        /// Gets or sets the mode of ASDU queue behaviour. Default mode is
        /// EnqueueMode.REMOVE_OLDEST.
        /// </summary>
        /// <value>the mode of ASDU queue behaviour</value>
        public EnqueueMode EnqueueMode
        {
            get { return enqueueMode; }
            set { enqueueMode = value; }
        }

        private void DebugLog(string msg)
        {
            if (debugOutput)
            {
                Console.Write("CS104 SLAVE: ");
                Console.WriteLine(msg);
            }
        }

        /// <summary>
        /// Gets or sets the maximum size of the ASDU queue. Setting this property has no
        /// effect after calling the Start method.
        /// </summary>
        /// <value>The size of the max queue.</value>
        public int MaxQueueSize
        {
            get
            {
                return maxQueueSize;
            }
            set
            {
                maxQueueSize = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum size of the ASDU high priotity queue. Setting this property has no
        /// effect after calling the Start method.
        /// </summary>
        /// <value>The size of the max  high priotity queue.</value>
        public int MaxHighPrioQueueSize
        {
            get
            {
                return maxHighPrioQueueSize;
            }
            set
            {
                maxHighPrioQueueSize = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of open TCP connections
        /// </summary>
        /// <value>The maximum number of open TCP connections.</value>
        public int MaxOpenConnections
        {
            get
            {
                return maxOpenConnections;
            }
            set
            {
                maxOpenConnections = value;
            }
        }

        private APCIParameters apciParameters;
        private ApplicationLayerParameters alParameters;

        /// <summary>
        /// Get active application layer parameters (modify only before starting the server!)
        /// </summary>
        /// <returns>application layer parameters object used by the server</returns>
        public ApplicationLayerParameters GetApplicationLayerParameters()
        {
            return alParameters;
        }

        /// <summary>
        /// Get active APCI parameters (modify only before starting the server!)
        /// </summary>
        /// <returns>APCI parameters object used by the server</returns>
        public APCIParameters GetAPCIParameters()
        {
            return apciParameters;
        }

        private TlsSecurityInformation securityInfo = null;

        // List of all open connections
        private List<ClientConnection> allOpenConnections = new List<ClientConnection>();

        /// <summary>
        /// Create a new server using default connection parameters
        /// </summary>
        public Server()
        {
            apciParameters = new APCIParameters();
            alParameters = new ApplicationLayerParameters();

        }

        /// <summary>
        /// Create a new server using default connection parameters and TLS configuration
        /// </summary>
        /// <param name="securityInfo">TLS layer configuation, or null when not using TLS</param>
        public Server(TlsSecurityInformation securityInfo)
        {
            apciParameters = new APCIParameters();
            alParameters = new ApplicationLayerParameters();

            this.securityInfo = securityInfo;

            if (securityInfo != null)
                localPort = 19998;
        }

        /// <summary>
        /// Create a new server using the provided connection parameters.
        /// </summary>
        /// <param name="apciParameters">APCI parameters</param>
        /// <param name="alParameters">application layer parameters</param>
        public Server(APCIParameters apciParameters, ApplicationLayerParameters alParameters)
        {
            this.apciParameters = apciParameters;
            this.alParameters = alParameters;
        }

        /// <summary>
        /// Create a new server using the provided connection parameters.
        /// </summary>
        /// <param name="apciParameters">APCI parameters</param>
        /// <param name="alParameters">application layer parameters</param>
        public Server(APCIParameters apciParameters, ApplicationLayerParameters alParameters, TlsSecurityInformation securityInfo)
        {
            this.apciParameters = apciParameters;
            this.alParameters = alParameters;
            this.securityInfo = securityInfo;

            if (securityInfo != null)
                localPort = 19998;
        }

        /// <summary>
        /// Adds a redundancy group to the server. Each redundancy group has its own event queue.
        /// </summary>
        /// <param name="redundancyGroup">Redundancy group.</param>
        public void AddRedundancyGroup(RedundancyGroup redundancyGroup)
        {
            redGroups.Add(redundancyGroup);
        }

        public ServerStateEventHandler serverStateEventHandler = null;
        public object serverStateEventHandlerParameter = null;
        /// <summary>
        /// Sets a callback handler for server state changes.
        /// </summary>
        /// <param name="handler">Handler.</param>
        /// <param name="parameter">Parameter.</param>
        public void SetServerStateEventHandler(ServerStateEventHandler handler, object parameter)
        {
            this.serverStateEventHandler = handler;
            this.serverStateEventHandlerParameter = parameter;
        }

        public ConnectionRequestHandler connectionRequestHandler = null;
        public object connectionRequestHandlerParameter = null;

        /// <summary>
        /// Sets a callback handler for connection request. The user can allow (returning true) or deny (returning false)
        /// the connection attempt. If no handler is installed every new connection will be accepted. 
        /// </summary>
        /// <param name="handler">Handler.</param>
        /// <param name="parameter">Parameter.</param>
        public void SetConnectionRequestHandler(ConnectionRequestHandler handler, object parameter)
        {
            connectionRequestHandler = handler;
            connectionRequestHandlerParameter = parameter;
        }

        private ConnectionEventHandler connectionEventHandler = null;
        private object connectionEventHandlerParameter = null;

        /// <summary>
        /// Sets the connection event handler. The connection event handler will be called whenever a new
        /// connection was opened, closed, activated, or inactivated.
        /// </summary>
        /// <param name="handler">Handler.</param>
        /// <param name="parameter">Parameter.</param>
        public void SetConnectionEventHandler(ConnectionEventHandler handler, object parameter)
        {
            connectionEventHandler = handler;
            connectionEventHandlerParameter = parameter;
        }

        /// <summary>
        /// Gets the number of connected master/client stations.
        /// </summary>
        /// <value>The number of open connections.</value>
        public int OpenConnections
        {
            get
            {
                return allOpenConnections.Count;
            }
        }

        /// <summary>
        /// Maximum allowed time for receiving a single message
        /// </summary>
        public int ReceiveTimeout
        {
            get
            {
                return receiveTimeoutInMs;
            }
            set
            {
                receiveTimeoutInMs = value;
            }
        }

        private int GetEntryCount(ASDUQueue queue)
        {
            int count = 0;
            if (queue != null)
            {
                try
                {
                    queueLock.Wait();
                    count = queue.NumberOfAsduInQueue;
                    queueLock.Release();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            return count;
        }

        /// <summary>
        /// Returns the number of elements in the low priority queue
        /// </summary>
        /// <param name="redundancyGroup">the redundancy group object (only required for ServerMode.MULTIPLE_REDUNDANCY_GROUPS</param>
        /// <returns>The number of ASDUs in the queue, or 0 when the queue is empty</returns>
        public int GetNumberOfQueueEntries(RedundancyGroup redundancyGroup = null)
        {
            if (serverMode == ServerMode.CONNECTION_IS_REDUNDANCY_GROUP)
            {
                foreach (ClientConnection connection in allOpenConnections)
                {
                    if (connection.IsActive)
                    {
                        return GetEntryCount(connection.GetASDUQueue());
                    }
                }
            }
            else if (serverMode == ServerMode.MULTIPLE_REDUNDANCY_GROUPS)
            {
                if (redundancyGroup != null)
                {
                    return GetEntryCount(redundancyGroup.asduQueue);
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                RedundancyGroup singleGroup = redGroups[0];
                return GetEntryCount(singleGroup.asduQueue);
            }

            return 0;
        }

        private void ServerAcceptThread()
        {
            running = true;
            CallServerStateEventHandler(new ServerStateEvent(true, false, null));

            DebugLog("Waiting for connections...");

            while (running)
            {
                try
                {
                    Socket newSocket = listeningSocket.Accept();

                    if (newSocket != null)
                    {
                        newSocket.NoDelay = true;

                        DebugLog("New connection");

                        IPEndPoint ipEndPoint = (IPEndPoint)newSocket.RemoteEndPoint;

                        DebugLog("  from IP: " + ipEndPoint.Address.ToString());


                        bool acceptConnection = true;

                        if (OpenConnections >= maxOpenConnections)
                            acceptConnection = false;

                        if (acceptConnection && (connectionRequestHandler != null))
                        {
                            acceptConnection = connectionRequestHandler(connectionRequestHandlerParameter, ipEndPoint.Address);
                        }

                        if (acceptConnection)
                        {
                            ClientConnection connection = null;

                            if ((serverMode == ServerMode.SINGLE_REDUNDANCY_GROUP) || (serverMode == ServerMode.MULTIPLE_REDUNDANCY_GROUPS))
                            {
                                RedundancyGroup catchAllGroup = null;

                                RedundancyGroup matchingGroup = null;

                                /* get matching redundancy group */
                                foreach (RedundancyGroup redGroup in redGroups)
                                {
                                    if (redGroup.Matches(ipEndPoint.Address))
                                    {
                                        matchingGroup = redGroup;
                                        break;
                                    }

                                    if (redGroup.IsCatchAll)
                                        catchAllGroup = redGroup;
                                }

                                if (matchingGroup == null)
                                    matchingGroup = catchAllGroup;

                                if (matchingGroup != null)
                                {
                                    connection = new ClientConnection(newSocket, securityInfo, apciParameters, alParameters, this,
                                        matchingGroup.asduQueue, debugOutput);

                                    matchingGroup.AddConnection(connection);

                                    DebugLog("Add connection to group " + matchingGroup.Name);
                                }
                                else
                                {
                                    DebugLog("Found no matching redundancy group -> close connection");
                                    newSocket.Close();
                                }
                            }
                            else
                            {
                                connection = new ClientConnection(newSocket, securityInfo, apciParameters, alParameters, this,
                                    new ASDUQueue(maxQueueSize, enqueueMode, alParameters, DebugLog), debugOutput);
                            }

                            if (connection != null)
                            {
                                allOpenConnections.Add(connection);

                                CallConnectionEventHandler(connection, ClientConnectionEvent.OPENED);
                            }

                        }
                        else
                            newSocket.Close();
                    }
                }
                catch (Exception e)
                {
                    DebugLog("Exception: " + e.Message);
                    bool socketExceptionDuringShutdown = !running && (e is SocketException);
                    if (!socketExceptionDuringShutdown)
                    {
                        running = false;
                        CallServerStateEventHandler(new ServerStateEvent(false, true, e));
                    }
                }

            }
        }

        internal void Remove(ClientConnection connection)
        {
            CallConnectionEventHandler(connection, ClientConnectionEvent.CLOSED);

            if ((serverMode == ServerMode.MULTIPLE_REDUNDANCY_GROUPS) || (serverMode == ServerMode.SINGLE_REDUNDANCY_GROUP))
            {
                foreach (RedundancyGroup redGroup in redGroups)
                {
                    redGroup.RemoveConnection(connection);
                }
            }

            allOpenConnections.Remove(connection);
        }

        /// <summary>
        /// Sets the local IP address to bind the server. Default is "0.0.0.0" for
        /// all interfaces
        /// </summary>
        /// <param name="localAddress">Local IP address or hostname to bind.</param>
        public void SetLocalAddress(string localAddress)
        {
            localHostname = localAddress;
        }

        /// <summary>
        /// Sets the local TCP port to bind to. Default is 2404, or 19998 when using TLS.
        /// </summary>
        /// <param name="tcpPort">Local TCP port to bind.</param>
        public void SetLocalPort(int tcpPort)
        {
            localPort = tcpPort;
        }

        /// <summary>
        /// Start the server. Listen to client connections.
        /// </summary>
        public void Start()
        {
            IPAddress ipAddress = IPAddress.Parse(localHostname);
            IPEndPoint localEP = new IPEndPoint(ipAddress, localPort);

            // Create a TCP/IP  socket.
            listeningSocket = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);

            listeningSocket.Bind(localEP);

            listeningSocket.Listen(100);

            Thread acceptThread = new Thread(ServerAcceptThread);

            if (serverMode == ServerMode.SINGLE_REDUNDANCY_GROUP)
            {
                if (redGroups.Count > 0)
                {
                    RedundancyGroup singleGroup = redGroups[0];
                    redGroups.Clear();
                    redGroups.Add(singleGroup);
                }
                else
                {
                    RedundancyGroup singleGroup = new RedundancyGroup();
                    redGroups.Add(singleGroup);
                }
            }

            if (serverMode == ServerMode.SINGLE_REDUNDANCY_GROUP || serverMode == ServerMode.MULTIPLE_REDUNDANCY_GROUPS)
            {
                foreach (RedundancyGroup redGroup in redGroups)
                {
                    redGroup.asduQueue = new ASDUQueue(maxQueueSize, enqueueMode, alParameters, DebugLog);
                    redGroup.server = this;
                }
            }

            acceptThread.Start();
        }

        /// <summary>
        /// Stop the server. Close all open client connections.
        /// </summary>
        public void Stop()
        {
            running = false;
            CallServerStateEventHandler(new ServerStateEvent(false, false, null));

            try
            {
                try
                {
                    listeningSocket.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException ex)   // seems to be side effect of Shutdown(). No idea how to avoid it.
                {
                    DebugLog("SocketException: " + ex.Message + " with code " + ex.ErrorCode);
                    // CallServerStateEventHandler(new ServerStateEvent(false, false, ex));
                }

                listeningSocket.Close();

                // close all open connection
                foreach (ClientConnection connection in allOpenConnections)
                {
                    connection.Close();
                }
            }
            catch (Exception e)
            {
                DebugLog("Exception: " + e.Message);
                CallServerStateEventHandler(new ServerStateEvent(false, false, e));
            }
        }



        /// <summary>
        /// Check if the server is running (listening to client connections and handling connections) or not
        /// </summary>
        /// <returns>true when the server is running, false otherwise</returns>
        public bool IsRunning()
        {
            return running;
        }

        /// <summary>
        /// Enqueues the ASDU to the transmission queue.
        /// </summary>
        /// If an active connection exists the ASDU will be sent to the active client immediately. Otherwhise
        /// the ASDU will be added to the transmission queue for later transmission.
        /// <param name="asdu">ASDU to be sent</param>
        /// <exception cref="lib60870.CS101.ASDUQueueException">when the ASDU queue is full and mode is EnqueueMode.THROW_EXCEPTION.</exception>
        public void EnqueueASDU(ASDU asdu)
        {
            /*ASDUQueue queue = new ASDUQueue(MaxQueueSize, enqueueMode, alParameters, DebugLog);
            this.lowPrioQueue = queue;*/

            if (serverMode == ServerMode.CONNECTION_IS_REDUNDANCY_GROUP)
            {
                foreach (ClientConnection connection in allOpenConnections)
                {
                    if (connection.IsActive)
                    {
                        connection.GetASDUQueue().EnqueueAsdu(asdu);
                    }
                }
            }
            else
            {
                foreach (RedundancyGroup redGroup in redGroups)
                {
                    redGroup.EnqueueASDU(asdu);
                }
            }
        }

        public IEnumerable<ClientConnection> GetAllOpenConnections()
        {
            return allOpenConnections;
        }

        internal void CallServerStateEventHandler(ServerStateEvent e)
        {
            if (serverStateEventHandler != null)
                serverStateEventHandler(serverStateEventHandlerParameter, e);
        }

        internal void CallConnectionEventHandler(ClientConnection connection, ClientConnectionEvent e)
        {
            // if (connection.State == MasterConnectionState.M_CON_STATE_STARTED)
            // {
                if (connectionEventHandler != null)
                    connectionEventHandler(connectionEventHandlerParameter, connection, e);
            // }
        }

        internal void Activated(ClientConnection activeConnection)
        {
            CallConnectionEventHandler(activeConnection, ClientConnectionEvent.ACTIVE);

            if ((serverMode == ServerMode.SINGLE_REDUNDANCY_GROUP) || (serverMode == ServerMode.MULTIPLE_REDUNDANCY_GROUPS))
            {
                foreach (RedundancyGroup redGroup in redGroups)
                {
                    redGroup.Activate(activeConnection);
                }

            }

            activeConnection.State = MasterConnectionState.M_CON_STATE_STARTED;

        }

        internal void Deactivated(ClientConnection activeConnection)
        {

            CallConnectionEventHandler(activeConnection, ClientConnectionEvent.INACTIVE);

            activeConnection.State = MasterConnectionState.M_CON_STATE_UNCONFIRMED_STOPPED;
        }

        public override int FileTimeout
        {
            get
            {
                if (fileTimeout != null)
                    return FileTimeout;
                else
                    return -1;
            }

            set
            {
                fileTimeout = value;
            }
        }
    }

}
