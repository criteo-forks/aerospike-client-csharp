using Aerospike.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aerospike.Test;

[TestClass]
public class TestPool
{
    [TestMethod]
    public void RespectsFifoOrder()
    {
        var pool = new Pool<string>(0, 10);

        pool.Enqueue("1");
        pool.Enqueue("2");
        pool.Enqueue("3");
        pool.TryDequeue(out var first);
        pool.TryDequeue(out var second);
        pool.TryDequeue(out var third);
        
        Assert.AreEqual("1", first);
        Assert.AreEqual("2", second);
        Assert.AreEqual("3", third);
    }
    
    [TestMethod]
    public void CanDequeueAfterEnqueueLast()
    {
        var pool = new Pool<string>(0, 10);

        pool.EnqueueLast("1");
        Assert.IsTrue(pool.TryDequeue(out var first));
        
        Assert.AreEqual("1", first);
    }
}