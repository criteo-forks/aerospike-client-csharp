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
using System.Numerics;

namespace Aerospike.Client
{
	/// <summary>
	/// A bounded LIFO pool that spreads concurrent access across several small shards.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The pool creates a power-of-two shard count, up to twice the processor count.
	/// Small pools use one shard because extra shards add no useful concurrency.
	/// The constructor divides the configured capacity across fixed arrays.
	/// Therefore, the sum of all shard capacities is always the configured capacity.
	/// </para>
	/// <para>
	/// A thread selects its first shard from <see cref="Environment.CurrentManagedThreadId"/>.
	/// It uses a mask because the shard count is a power of two.
	/// If that shard cannot serve the operation, the thread checks each remaining shard.
	/// A volatile count read skips shards that are clearly empty or full.
	/// The method validates that count after it takes the shard lock.
	/// It never holds more than one shard lock at a time.
	/// A scan can return false if concurrent work moves the available slot between shards.
	/// </para>
	/// <para>
	/// Each shard stores its items as a small array-backed LIFO stack.
	/// The pool keeps LIFO order inside each shard, but it does not define a global order.
	/// Tail operations rotate across shards and use the oldest item in the selected shard.
	/// Connection maintenance scans available items, so it does not depend on a global tail.
	/// </para>
	/// <para>
	/// The fixed arrays enforce the item limit without a shared counter on every checkout and return.
	/// <see cref="TryIncrTotal"/> uses compare-and-exchange to reserve a connection slot before creation.
	/// That reservation path keeps the number of live connections at or below <see cref="Capacity"/>.
	/// </para>
	/// <para>
	/// <see cref="Count"/> reads each shard separately.
	/// It returns a useful snapshot, but concurrent operations can change the pool during that read.
	/// </para>
	/// <para>
	/// This design favors small, independent critical sections over one complex lock-free data structure.
	/// It removes the pool-wide contention point without adding allocations to the hot path.
	/// </para>
	/// </remarks>
	public sealed class Pool<T>
	{
		private readonly Shard[] shards;
		private readonly int shardMask;
		private readonly int capacity;
		private int tailIter;
		internal readonly int minSize;
		private int total; // total items: inUse + inPool

		/// <summary>
		/// Construct the pool.
		/// </summary>
		public Pool(int minSize, int maxSize)
		{
			this.minSize = minSize;
			capacity = maxSize;
			int maxShards = Math.Min(Math.Max(1, maxSize), Math.Max(1, Environment.ProcessorCount * 2));
			int shardCount = maxSize <= 4 ? 1 : 1 << BitOperations.Log2((uint)maxShards);
			shards = new Shard[shardCount];
			shardMask = shardCount - 1;
			int shardCapacity = maxSize / shardCount;
			int remainder = maxSize - (shardCapacity * shardCount);

			for (int i = 0; i < shardCount; i++)
			{
				shards[i] = new Shard(shardCapacity + (i < remainder ? 1 : 0));
			}
		}

		/// <summary>
		/// Insert item at the head of a shard.
		/// </summary>
		public bool Enqueue(T item)
		{
			int initialIndex = GetShardIndex();

			for (int offset = 0; offset < shards.Length; offset++)
			{
				Shard shard = shards[(initialIndex + offset) & shardMask];

				if (Volatile.Read(ref shard.count) >= shard.items.Length)
				{
					continue;
				}

				lock (shard)
				{
					if (shard.count < shard.items.Length)
					{
						shard.items[shard.count++] = item;
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// Insert item at the tail of a shard.
		/// </summary>
		public bool EnqueueLast(T item)
		{
			int initialIndex = GetShardIndex();

			for (int offset = 0; offset < shards.Length; offset++)
			{
				Shard shard = shards[(initialIndex + offset) & shardMask];

				if (Volatile.Read(ref shard.count) >= shard.items.Length)
				{
					continue;
				}

				lock (shard)
				{
					if (shard.count < shard.items.Length)
					{
						Array.Copy(shard.items, 0, shard.items, 1, shard.count);
						shard.items[0] = item;
						shard.count++;
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// Pop item from the head of a shard.
		/// </summary>
		public bool TryDequeue(out T item)
		{
			int initialIndex = GetShardIndex();

			for (int offset = 0; offset < shards.Length; offset++)
			{
				Shard shard = shards[(initialIndex + offset) & shardMask];

				if (Volatile.Read(ref shard.count) <= 0)
				{
					continue;
				}

				lock (shard)
				{
					if (shard.count > 0)
					{
						int index = --shard.count;
						item = shard.items[index];
						shard.items[index] = default(T);
						return true;
					}
				}
			}

			item = default(T);
			return false;
		}

		/// <summary>
		/// Peek at an item from the head of a shard.
		/// </summary>
		public T PeekFirst()
		{
			int initialIndex = GetShardIndex();

			for (int offset = 0; offset < shards.Length; offset++)
			{
				Shard shard = shards[(initialIndex + offset) & shardMask];

				if (Volatile.Read(ref shard.count) <= 0)
				{
					continue;
				}

				lock (shard)
				{
					if (shard.count > 0)
					{
						return shard.items[shard.count - 1];
					}
				}
			}
			return default;
		}

		/// <summary>
		/// Pop item from the tail of a shard.
		/// </summary>
		public bool TryDequeueLast(out T item)
		{
			int initialIndex = GetTailIndex();

			for (int offset = 0; offset < shards.Length; offset++)
			{
				Shard shard = shards[(initialIndex + offset) & shardMask];

				if (Volatile.Read(ref shard.count) <= 0)
				{
					continue;
				}

				lock (shard)
				{
					if (shard.count > 0)
					{
						item = shard.items[0];
						int count = --shard.count;
						Array.Copy(shard.items, 1, shard.items, 0, count);
						shard.items[count] = default(T);
						return true;
					}
				}
			}

			item = default(T);
			return false;
		}

		/// <summary>
		/// Peek at an item from the tail of a shard.
		/// </summary>
		public T PeekLast()
		{
			int initialIndex = (Volatile.Read(ref tailIter) + 1) & shardMask;

			for (int offset = 0; offset < shards.Length; offset++)
			{
				Shard shard = shards[(initialIndex + offset) & shardMask];

				if (Volatile.Read(ref shard.count) <= 0)
				{
					continue;
				}

				lock (shard)
				{
					if (shard.count > 0)
					{
						return shard.items[0];
					}
				}
			}
			return default;
		}

		/// <summary>
		/// Return item count.
		/// </summary>
		public int Count
		{
			get
			{
				int count = 0;

				foreach (Shard shard in shards)
				{
					count += Volatile.Read(ref shard.count);
				}
				return count;
			}
		}

		/// <summary>
		/// Return pool capacity.
		/// </summary>
		public int Capacity
		{
			get { return capacity; }
		}

		private int GetShardIndex()
		{
			return Environment.CurrentManagedThreadId & shardMask;
		}

		private int GetTailIndex()
		{
			return Interlocked.Increment(ref tailIter) & shardMask;
		}

		private sealed class Shard
		{
			internal readonly T[] items;
			internal int count;

			internal Shard(int capacity)
			{
				items = new T[capacity];
			}
		}

		/// <summary>
		/// Return number of connections that might be closed.
		/// </summary>
		public int Excess()
		{
			return Volatile.Read(ref total) - minSize;
		}

		/// <summary>
		/// Increment total connections unless the pool is at capacity.
		/// </summary>
		public bool TryIncrTotal()
		{
			int count = Volatile.Read(ref total);

			while (count < capacity)
			{
				int observed = Interlocked.CompareExchange(ref total, count + 1, count);

				if (observed == count)
				{
					return true;
				}
				count = observed;
			}
			return false;
		}

		/// <summary>
		/// Increment total connections.
		/// </summary>
		public int IncrTotal()
		{
			return Interlocked.Increment(ref total);
		}

		/// <summary>
		/// Decrement total connections.
		/// </summary>
		public int DecrTotal()
		{
			return Interlocked.Decrement(ref total);
		}

		/// <summary>
		/// Return total connections.
		/// </summary>
		public int Total
		{
			get { return Volatile.Read(ref total); }
		}
	}
}
