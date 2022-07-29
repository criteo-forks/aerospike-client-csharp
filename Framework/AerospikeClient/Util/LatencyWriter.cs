/* 
 * Copyright 2012-2022 Aerospike, Inc.
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
using System;
using System.Text;
using System.IO;

namespace Aerospike.Client
{
	public sealed class LatencyWriter
    {
		internal readonly LatencyManager connLatency;
		internal readonly LatencyManager writeLatency;
		internal readonly LatencyManager readLatency;
		internal readonly LatencyManager batchLatency;
		private readonly StringBuilder lineBuilder;
		private readonly StreamWriter writer;

		public LatencyWriter(StatsPolicy policy)
        {
			connLatency = new LatencyManager(policy);
			writeLatency = new LatencyManager(policy);
			readLatency = new LatencyManager(policy);
			batchLatency = new LatencyManager(policy);
			lineBuilder = new StringBuilder(256);
			
			FileStream fs = new FileStream(policy.reportPath, FileMode.Append, FileAccess.Write);
			writer = new StreamWriter(fs);
			writer.WriteLine(writeLatency.PrintHeader(lineBuilder));
		}
	
		public void Write()
		{
			lock (writer)
			{
				writer.WriteLine(connLatency.PrintResults(lineBuilder, "conn"));
				writer.WriteLine(writeLatency.PrintResults(lineBuilder, "write"));
				writer.WriteLine(readLatency.PrintResults(lineBuilder, "read"));
				writer.WriteLine(readLatency.PrintResults(lineBuilder, "batch"));
			}
		}

		public void Close()
		{
			Write();

			lock (writer)
			{
				writer.Close();
			}
		}
	}
}
