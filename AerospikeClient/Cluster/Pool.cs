/* 
 * Copyright 2012-2021 Aerospike, Inc.
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
using System.Collections.Concurrent;
using System.Threading;

namespace Aerospike.Client
{
	public sealed class Pool<T> where T : class
	{
		private readonly T[] items;
		private int size;
		
		internal readonly int minSize;
		private int total;
		
		public Pool(int minSize, int maxSize)
		{
			this.minSize = minSize;
			items = new T[maxSize];

			size = 0;
		}

		public bool Enqueue(T item)
		{
			if (!TryIncrementSize()) 
				return false;

			while (true)
			{
				for (var i = 0; i < items.Length; i++)
				{
					var observedValue = items[i];
					if (observedValue != null)
					{
						continue;
					}
					if (Interlocked.CompareExchange(ref items[i], item, null) == null)
					{
						return true;
					}
				}
			}
		}

		private bool TryIncrementSize()
		{
			var curSize = size;
			do
			{
				if (curSize == items.Length) // observed full array
				{
					return false;
				}

				var observedSize = Interlocked.CompareExchange(ref size, curSize + 1, curSize);
				if (observedSize == curSize)
				{
					// successfully incremented, we'll be able to find a place in array
					break;
				}

				curSize = observedSize;
			} while (true);

			return true;
		}

		public bool TryDequeue(out T value)
		{
			if (!TryDecrementSize())
			{
				value = default;
				return false;
			}

			while (true)
			{
				for (var i = 0; i < items.Length; i++)
				{
					var observedValue = items[i];
					if (observedValue == null)
					{
						continue;
					}

					if (Interlocked.CompareExchange(ref items[i], null, observedValue) == observedValue)
					{
						value = observedValue;
						return true;
					}
				}
			}
		}

		private bool TryDecrementSize()
		{
			var curSize = size;
			do
			{
				if (curSize == 0) // nothing in the pool
				{
					return false;
				}
				
				var observedSize = Interlocked.CompareExchange(ref size, curSize - 1, curSize);
				if (observedSize == curSize)
				{
					// successfully decremented, we will be able to dequeue the item
					break;
				}

				curSize = observedSize;
			} while (true);

			return true;
		}

		public bool EnqueueLast(T item)
		{
			if (!TryIncrementSize())
				return false;
			
			while (true)
			{
				for (var i = items.Length - 1; i >= 0; i--)
				{
					var observedValue = items[i];
					if (observedValue != null)
					{
						continue;
					}
					if (Interlocked.CompareExchange(ref items[i], item, null) == null)
					{
						return true;
					}
				}
			}
		}

		public bool TryDequeueLast(out T value)
		{
			if (!TryDecrementSize())
			{
				value = default;
				return false;
			}
			
			while (true)
			{
				for (var i = items.Length - 1; i >= 0; i--)
				{
					var observedValue = items[i];
					if (observedValue == null)
					{
						continue;
					}

					if (Interlocked.CompareExchange(ref items[i], null, observedValue) == observedValue)
					{
						value = observedValue;
						return true;
					}
				}
			}
		}
		
		
		
		
		/// <summary>
		/// Return item count.
		/// </summary>
		public int Count
		{
			get
			{
				return size;
			}
		}

		/// <summary>
		/// Return pool capacity.
		/// </summary>
		public int Capacity
		{
			get { return items.Length; }
		}

		/// <summary>
		/// Return number of connections that might be closed.
		/// </summary>
		public int Excess()
		{
			return total - minSize;
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
			get { return total; }
		}
	}
	
	
	// /// <summary>
	// /// Concurrent bounded LIFO stack with ability to pop from head or tail.
	// /// <para>
	// /// The standard library concurrent stack, ConcurrentStack, does not
	// /// allow pop from both head and tail.
	// /// </para>
	// /// </summary>
	// public sealed class Pool<T>
	// {
	// 	private readonly T[] items;
	// 	private int head;
	// 	private int tail;
	// 	private int size;
	// 	internal readonly int minSize;
	// 	private volatile int total; // total items: inUse + inPool
	//
	// 	/// <summary>
	// 	/// Construct stack pool.
	// 	/// </summary>
	// 	public Pool(int minSize, int maxSize)
	// 	{
	// 		this.minSize = minSize;
	// 		items = new T[maxSize];
	// 	}
	//
	// 	/// <summary>
	// 	/// Insert item at head of stack.
	// 	/// </summary>
	// 	public bool Enqueue(T item)
	// 	{
	// 		Monitor.Enter(this);
	// 		
	// 		try
	// 		{
	// 			if (size == items.Length)
	// 			{
	// 				return false;
	// 			}
	//
	// 			items[head] = item;
	//
	// 			if (++head == items.Length)
	// 			{
	// 				head = 0;
	// 			}
	// 			size++;
	// 			return true;
	// 		}
	// 		finally
	// 		{
	// 			Monitor.Exit(this);
	// 		}
	// 	}
	//
	// 	/// <summary>
	// 	/// Insert item at tail of stack.
	// 	/// </summary>
	// 	public bool EnqueueLast(T item)
	// 	{
	// 		Monitor.Enter(this);
	//
	// 		try
	// 		{
	// 			if (size == items.Length)
	// 			{
	// 				return false;
	// 			}
	//
	// 			if (tail == 0)
	// 			{
	// 				tail = items.Length - 1;
	// 			}
	// 			else
	// 			{
	// 				tail--;
	// 			}
	// 			items[tail] = item;
	// 			size++;
	// 			return true;
	// 		}
	// 		finally
	// 		{
	// 			Monitor.Exit(this);
	// 		}
	// 	}
	//
	// 	/// <summary>
	// 	/// Pop item from head of stack.
	// 	/// </summary>
	// 	public bool TryDequeue(out T item)
	// 	{
	// 		Monitor.Enter(this);
	//
	// 		try
	// 		{
	// 			if (size == 0)
	// 			{
	// 				item = default(T);
	// 				return false;
	// 			}
	//
	// 			if (head == 0)
	// 			{
	// 				head = items.Length - 1;
	// 			}
	// 			else
	// 			{
	// 				head--;
	// 			}
	// 			size--;
	//
	// 			item = items[head];
	// 			items[head] = default(T);
	// 			return true;
	// 		}
	// 		finally
	// 		{
	// 			Monitor.Exit(this);
	// 		}
	// 	}
	//
	// 	/// <summary>
	// 	/// Pop item from tail of stack.
	// 	/// </summary>
	// 	public bool TryDequeueLast(out T item)
	// 	{
	// 		Monitor.Enter(this);
	//
	// 		try
	// 		{
	// 			if (size == 0)
	// 			{
	// 				item = default(T);
	// 				return false;
	// 			}
	// 			item = items[tail];
	// 			items[tail] = default(T);
	//
	// 			if (++tail == items.Length)
	// 			{
	// 				tail = 0;
	// 			}
	// 			size--;
	// 			return true;
	// 		}
	// 		finally
	// 		{
	// 			Monitor.Exit(this);
	// 		}
	// 	}
	//
	// 	/// <summary>
	// 	/// Return item count.
	// 	/// </summary>
	// 	public int Count
	// 	{
	// 		get
	// 		{
	// 			Monitor.Enter(this);
	//
	// 			try
	// 			{
	// 				return size;
	// 			}
	// 			finally
	// 			{
	// 				Monitor.Exit(this);
	// 			}
	// 		}
	// 	}
	//
	// 	/// <summary>
	// 	/// Return pool capacity.
	// 	/// </summary>
	// 	public int Capacity
	// 	{
	// 		get { return items.Length; }
	// 	}
	//
	// 	/// <summary>
	// 	/// Return number of connections that might be closed.
	// 	/// </summary>
	// 	public int Excess()
	// 	{
	// 		return total - minSize;
	// 	}
	//
	// 	/// <summary>
	// 	/// Increment total connections.
	// 	/// </summary>
	// 	public int IncrTotal()
	// 	{
	// 		return Interlocked.Increment(ref total);
	// 	}
	//
	// 	/// <summary>
	// 	/// Decrement total connections.
	// 	/// </summary>
	// 	public int DecrTotal()
	// 	{
	// 		return Interlocked.Decrement(ref total);
	// 	}
	//
	// 	/// <summary>
	// 	/// Return total connections.
	// 	/// </summary>
	// 	public int Total
	// 	{
	// 		get { return total; }
	// 	}
	// }
}
