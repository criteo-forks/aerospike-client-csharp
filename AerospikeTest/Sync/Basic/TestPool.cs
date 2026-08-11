/*
 * Copyright 2012-2026 Aerospike, Inc.
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
using Aerospike.Client;

namespace Aerospike.Test
{
	[TestClass]
	public class TestPool
	{
		[TestMethod]
		public void PoolOrder()
		{
			Pool<int> pool = new(0, 3);

			Assert.IsTrue(pool.Enqueue(1));
			Assert.IsTrue(pool.Enqueue(2));
			Assert.IsTrue(pool.Enqueue(3));
			Assert.AreEqual(3, pool.PeekFirst());
			Assert.AreEqual(1, pool.PeekLast());

			Assert.IsTrue(pool.TryDequeueLast(out int last));
			Assert.AreEqual(1, last);
			Assert.IsTrue(pool.EnqueueLast(0));

			Assert.IsTrue(pool.TryDequeue(out int first));
			Assert.AreEqual(3, first);
			Assert.IsTrue(pool.TryDequeue(out first));
			Assert.AreEqual(2, first);
			Assert.IsTrue(pool.TryDequeue(out first));
			Assert.AreEqual(0, first);
		}

		[TestMethod]
		public void PoolConcurrentBound()
		{
			const int capacity = 64;
			Pool<int> pool = new(0, capacity);
			int accepted = 0;

			Parallel.For(0, capacity * 16, value =>
			{
				if (pool.Enqueue(value))
				{
					Interlocked.Increment(ref accepted);
				}
			});

			Assert.AreEqual(capacity, accepted);
			Assert.AreEqual(capacity, pool.Count);
			Assert.IsFalse(pool.Enqueue(-1));
		}

		[TestMethod]
		public void PoolConcurrentTotalBound()
		{
			const int capacity = 64;
			Pool<int> pool = new(0, capacity);
			int accepted = 0;

			Parallel.For(0, capacity * 16, _ =>
			{
				if (pool.TryIncrTotal())
				{
					Interlocked.Increment(ref accepted);
				}
			});

			Assert.AreEqual(capacity, accepted);
			Assert.AreEqual(capacity, pool.Total);
		}

		[TestMethod]
		public void PoolConcurrentReuse()
		{
			const int capacity = 64;
			const int iterations = 10000;
			Pool<int> pool = new(0, capacity);
			int[] leased = new int[capacity];
			int errors = 0;

			for (int i = 0; i < capacity; i++)
			{
				Assert.IsTrue(pool.Enqueue(i));
			}

			Parallel.For(0, Environment.ProcessorCount * 2, _ =>
			{
				for (int i = 0; i < iterations; i++)
				{
					SpinWait spin = new();
					int item;

					while (!pool.TryDequeue(out item))
					{
						spin.SpinOnce();
					}

					if (Interlocked.Exchange(ref leased[item], 1) != 0)
					{
						Interlocked.Increment(ref errors);
					}

					if (Interlocked.Exchange(ref leased[item], 0) != 1)
					{
						Interlocked.Increment(ref errors);
					}

					if (!pool.Enqueue(item))
					{
						Interlocked.Increment(ref errors);
					}
				}
			});

			Assert.AreEqual(0, errors);
			Assert.AreEqual(capacity, pool.Count);
		}
	}
}
