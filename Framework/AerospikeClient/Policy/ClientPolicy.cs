/* 
 * Copyright 2012-2019 Aerospike, Inc.
 *
 * Portions may be licensed to Aerospike, Inc. under one or more contributor
 * license agreements.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not
 * use this file except in compliance with the License. You may obtain a copy of
 * the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations under
 * the License.
 */
using System.Collections.Generic;

namespace Aerospike.Client
{
	/// <summary>
	/// Client initialization arguments.
	/// </summary>
	public class ClientPolicy
	{
		/// <summary>
		/// User authentication to cluster.  Leave null for clusters running without restricted access.
		/// <para>Default: null</para>
		/// </summary>
		public string user;

		/// <summary>
		/// Password authentication to cluster.  The password will be stored by the client and sent to server
		/// in hashed format.  Leave null for clusters running without restricted access.
		/// <para>Default: null</para>
		/// </summary>
		public string password;

		/// <summary>
		/// Expected cluster name.  If populated, server nodes must return this cluster name in order to
		/// join the client's view of the cluster. Should only be set when connecting to servers that
		/// support the "cluster-name" info command.
		/// <para>Default: null</para>
		/// </summary>
		public string clusterName;

		/// <summary>
		/// Authentication mode used when user/password is defined.
		/// <para>Default: AuthMode.INTERNAL</para>
		/// </summary>
		public AuthMode authMode = AuthMode.INTERNAL;

		/// <summary>
		/// Initial host connection timeout in milliseconds.  The timeout when opening a connection 
		/// to the server host for the first time.
		/// <para>Default: 1000</para>
		/// </summary>
		public int timeout = 1000;

		/// <summary>
		/// Login timeout in milliseconds.  The timeout used when user authentication is enabled and
		/// a node login is being performed.
		/// <para>Default: 5000</para>
		/// </summary>
		public int loginTimeout = 5000;

		/// <summary>
		/// Maximum number of connections allowed per server node.  Synchronous transactions
		/// will go through retry logic and potentially fail with "ResultCode.NO_MORE_CONNECTIONS"
		/// if the maximum number of connections would be exceeded.
		/// <para>
		/// The number of connections used per node depends on how many concurrent threads issue
		/// database commands plus sub-threads used for parallel multi-node commands (batch, scan,
		/// and query). One connection will be used for each thread.
		/// </para>
		/// <para>
		/// This field is ignored by asynchronous transactions since these transactions are already
		/// bound by asyncMaxCommands by default. Each async command has a one-to-one relationship with
		/// connections.
		/// </para>
		/// <para>Default: 300</para>
		/// </summary>
		public int maxConnsPerNode = 300;

		/// <summary>
		/// Number of synchronous connection pools used for each node.  Machines with 8 cpu cores or
		/// less usually need just one connection pool per node.  Machines with a large number of cpu
		/// cores may have their synchronous performance limited by contention for pooled connections.
		/// Contention for pooled connections can be reduced by creating multiple mini connection pools
		/// per node.
		/// <para>Default: 1</para>
		/// </summary>
		public int connPoolsPerNode = 1;

		/// <summary>
		/// Maximum socket idle in seconds.  Socket connection pools will discard sockets
		/// that have been idle longer than the maximum.  The value is limited to 24 hours (86400).
		/// <para>
		/// It's important to set this value to a few seconds less than the server's proto-fd-idle-ms
		/// (default 60000 milliseconds or 1 minute), so the client does not attempt to use a socket 
		/// that has already been reaped by the server.
		/// </para>
		/// <para>
		/// Connection pools are now implemented by a LIFO stack.  Connections at the tail of the
		/// stack will always be the least used.  These connections are checked for maxSocketIdle
		/// once every 30 tend iterations (usually 30 seconds).
		/// </para>
		/// <para>Default: 55</para>
		/// </summary>
		public int maxSocketIdle = 55;

		/// <summary>
		/// Interval in milliseconds between cluster tends by maintenance thread.
		/// <para>Default: 1000</para>
		/// </summary>
		public int tendInterval = 1000;
	
		/// <summary>
		/// Throw exception if all seed connections fail on cluster instantiation.
		/// <para>Default: true</para>
		/// </summary>
		public bool failIfNotConnected = true;

		/// <summary>
		/// Default read policy that is used when read command's policy is null.
		/// </summary>
		public Policy readPolicyDefault = new Policy();

		/// <summary>
		/// Default write policy that is used when write command's policy is null.
		/// </summary>
		public WritePolicy writePolicyDefault = new WritePolicy();

		/// <summary>
		/// Default scan policy that is used when scan command's policy is null.
		/// </summary>
		public ScanPolicy scanPolicyDefault = new ScanPolicy();

		/// <summary>
		/// Default query policy that is used when query command's policy is null.
		/// </summary>
		public QueryPolicy queryPolicyDefault = new QueryPolicy();

		/// <summary>
		/// Default batch policy that is used when batch command's policy is null.
		/// </summary>
		public BatchPolicy batchPolicyDefault = new BatchPolicy();

		/// <summary>
		/// Default info policy that is used when info command's policy is null.
		/// </summary>
		public InfoPolicy infoPolicyDefault = new InfoPolicy();

		/// <summary>
		/// Secure connection policy for servers that require TLS connections.
		/// Secure connections are only supported for AerospikeClient synchronous commands.
		/// <para>
		/// Secure connections are not supported for asynchronous commands because AsyncClient 
		/// uses the best performing SocketAsyncEventArgs.  Unfortunately, SocketAsyncEventArgs is
		/// not supported by the provided SslStream.
		/// </para>
		/// <para>Default: null (Use normal sockets)</para>
		/// </summary>
		public TlsPolicy tlsPolicy;

		/// <summary>
		/// A IP translation table is used in cases where different clients use different server 
		/// IP addresses.  This may be necessary when using clients from both inside and outside 
		/// a local area network.  Default is no translation.
		/// <para>
		/// The key is the IP address returned from friend info requests to other servers.  The 
		/// value is the real IP address used to connect to the server.
		/// </para>
		/// <para>Default: null (no IP address translation)</para>
		/// </summary>
		public Dictionary<string, string> ipMap;

		/// <summary>
		/// Should use "services-alternate" instead of "services" in info request during cluster
		/// tending.  "services-alternate" returns server configured external IP addresses that client
		/// uses to talk to nodes.  "services-alternate" can be used in place of providing a client "ipMap".
		/// <para>Default: false (use original "services" info request)</para>
		/// </summary>
		public bool useServicesAlternate;

		/// <summary>
		/// Track server rack data.  This field is useful when directing read commands to the server node
		/// that contains the key and exists on the same rack as the client.  This serves to lower cloud
		/// provider costs when nodes are distributed across different racks/data centers.
		/// <para>
		/// <see cref="Aerospike.Client.ClientPolicy.rackId"/>, <see cref="Aerospike.Client.Replica.PREFER_RACK"/>
		/// and server rack configuration must also be set to enable this functionality.
		/// </para>
		/// <para>Default: false</para>
		/// </summary>
		public bool rackAware;

		/// <summary>
		/// Rack where this client instance resides.
		/// <para>
		/// <see cref="Aerospike.Client.ClientPolicy.rackAware"/>, <see cref="Aerospike.Client.Replica.PREFER_RACK"/>
		/// and server rack configuration must also be set to enable this functionality.
		/// </para>
		/// <para>Default: 0</para>
		/// </summary>
		public int rackId;

		/// <summary>
		/// Copy client policy from another client policy.
		/// </summary>
		public ClientPolicy(ClientPolicy other)
		{
			this.user = other.user;
			this.password = other.password;
			this.clusterName = other.clusterName;
			this.authMode = other.authMode;
			this.timeout = other.timeout;
			this.loginTimeout = other.loginTimeout;
			this.maxConnsPerNode = other.maxConnsPerNode;
			this.connPoolsPerNode = other.connPoolsPerNode;
			this.maxSocketIdle = other.maxSocketIdle;
			this.tendInterval = other.tendInterval;
			this.failIfNotConnected = other.failIfNotConnected;
			this.readPolicyDefault = new Policy(other.readPolicyDefault);
			this.writePolicyDefault = new WritePolicy(other.writePolicyDefault);
			this.scanPolicyDefault = new ScanPolicy(other.scanPolicyDefault);
			this.queryPolicyDefault = new QueryPolicy(other.queryPolicyDefault);
			this.batchPolicyDefault = new BatchPolicy(other.batchPolicyDefault);
			this.infoPolicyDefault = new InfoPolicy(other.infoPolicyDefault);
			this.tlsPolicy = (other.tlsPolicy != null) ? new TlsPolicy(other.tlsPolicy) : null;
			this.ipMap = other.ipMap;
			this.useServicesAlternate = other.useServicesAlternate;
			this.rackAware = other.rackAware;
			this.rackId = other.rackId;
		}

		/// <summary>
		/// Default constructor.
		/// </summary>
		public ClientPolicy()
		{
		}
	}
}
