/*
 * Aerospike Client - C# Library
 *
 * Copyright 2013 by Aerospike, Inc. All rights reserved.
 *
 * Availability of this source code to partners and customers includes
 * redistribution rights covered by individual contract. Please check your
 * contract for exact rights and responsibilities.
 */
using System;
using System.Text;
using System.Collections.Generic;

namespace Aerospike.Client
{
	/// <summary>
	/// Parse node partitions using new protocol. This is more code than a String.split() implementation, 
	/// but it's faster because there are much fewer interim strings.
	/// </summary>
	public sealed class PartitionTokenizerNew
	{
		private const string ReplicasName = "replicas-master";

		private readonly byte[] buffer;
		private int length;
		private int offset;

		public PartitionTokenizerNew(Connection conn)
		{
			// Use low-level info methods and parse byte array directly for maximum performance.
			// Send format:	   replicas-master\n
			// Receive format: replicas-master\t<ns1>:<base 64 encoded bitmap>;<ns2>:<base 64 encoded bitmap>... \n
			Info info = new Info(conn, ReplicasName);
			this.length = info.GetLength();

			if (length == 0)
			{
				throw new AerospikeException.Parse(ReplicasName + " is empty");
			}
			this.buffer = info.GetBuffer();
			this.offset = ReplicasName.Length + 1; // Skip past name and tab
		}

		public Dictionary<string, Node[]> UpdatePartition(Dictionary<string, Node[]> map, Node node)
		{
			int begin = offset;
			bool copied = false;

			while (offset < length)
			{
				if (buffer[offset] == ':')
				{
					// Parse namespace.
					string ns = ByteUtil.Utf8ToString(buffer, begin, offset - begin).Trim();

					if (ns.Length <= 0 || ns.Length >= 32)
					{
						string response = GetTruncatedResponse();
						throw new AerospikeException.Parse("Invalid partition namespace " + ns + ". Response=" + response);
					}
					begin = ++offset;

					// Parse partition id.
					while (offset < length)
					{
						byte b = buffer[offset];

						if (b == ';' || b == '\n')
						{
							break;
						}
						offset++;
					}

					if (offset == begin)
					{
						string response = GetTruncatedResponse();
						throw new AerospikeException.Parse("Empty partition id for namespace " + ns + ". Response=" + response);
					}
					Node[] nodeArray;

					if (!map.TryGetValue(ns, out nodeArray))
					{
						if (!copied)
						{
							// Make shallow copy of map.
							map = new Dictionary<string, Node[]>(map);
							copied = true;
						}
						nodeArray = new Node[Node.PARTITIONS];
						map[ns] = nodeArray;
					}

					int bitMapLength = offset - begin;
					char[] chars = Encoding.ASCII.GetChars(buffer, begin, bitMapLength);
					byte[] restoreBuffer = Convert.FromBase64CharArray(chars, 0, chars.Length);

					for (int i = 0; i < Node.PARTITIONS; i++)
					{
						if ((restoreBuffer[i >> 3] & (0x80 >> (i & 7))) != 0)
						{
							//Log.info("Map: " + namespace + ',' + i + ',' + node);
							nodeArray[i] = node;
						}
					}
					begin = ++offset;
				}
				else
				{
					offset++;
				}
			}
			return (copied)? map : null;
		}

		private string GetTruncatedResponse()
		{
			int max = (length > 200) ? 200 : length;
			return ByteUtil.Utf8ToString(buffer, 0, max);
		}
	}
}