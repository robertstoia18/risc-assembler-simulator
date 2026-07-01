using RiscEmulator.Logic;
using Xunit;

namespace RiscEmulator.Tests;

public class WritePolicyTests
{
    [Fact]
    public void WriteThrough_OnHit_UpdatesBothCacheAndMemoryImmediately()
    {
        var memory = new Memory(64);
        var cache = new Cache(numSets: 4, blockSize: 4, associativity: 1, writePolicy: WritePolicy.WriteThrough);

        cache.Read(0, memory);
        cache.WriteThrough(0, 42, memory);

        Assert.Equal(42, memory.Read(0));
        Assert.Equal(42, cache.Read(0, memory));
    }

    [Fact]
    public void Write_DispatchesToWriteThrough_WhenPolicyIsWriteThrough()
    {
        var memory = new Memory(64);
        var cache = new Cache(numSets: 4, blockSize: 4, associativity: 1, writePolicy: WritePolicy.WriteThrough);

        cache.Write(0, 7, memory);

        Assert.Equal(7, memory.Read(0));
    }

    [Fact]
    public void WriteBack_OnHit_OnlyMarksDirty_DoesNotTouchMemoryImmediately()
    {
        var memory = new Memory(64);
        var cache = new Cache(numSets: 4, blockSize: 4, associativity: 1, writePolicy: WritePolicy.WriteBack);

        cache.Read(0, memory);
        cache.WriteBack(0, 99, memory);

        Assert.Equal(0, memory.Read(0));
        Assert.Equal(99, cache.Read(0, memory));
    }

    [Fact]
    public void WriteBack_MissUsesWriteAllocate()
    {
        var memory = new Memory(64);
        for (int i = 0; i < 64; i++) memory.Write(i, i);

        var cache = new Cache(numSets: 4, blockSize: 4, associativity: 2, writePolicy: WritePolicy.WriteBack);

        cache.WriteBack(5, 555, memory);
        Assert.Equal(1, cache.Misses);

        int val4 = cache.Read(4, memory);
        Assert.Equal(4, val4);

        int val5 = cache.Read(5, memory);
        Assert.Equal(555, val5);

        Assert.Equal(5, memory.Read(5));
    }

    [Fact]
    public void WriteBack_EvictingDirtyBlock_FlushesItToMemory()
    {
        var memory = new Memory(64);
        var cache = new Cache(numSets: 1, blockSize: 4, associativity: 1, writePolicy: WritePolicy.WriteBack);

        cache.Read(0, memory);
        cache.WriteBack(1, 77, memory);
        Assert.Equal(0, memory.Read(1));

        cache.Read(16, memory);

        Assert.Equal(77, memory.Read(1));
        Assert.Equal(1, cache.WriteBacks);
    }

    [Fact]
    public void FlushDirtyBlocks_WritesAllDirtyBlocksBackToMemory()
    {
        var memory = new Memory(64);
        var cache = new Cache(numSets: 1, blockSize: 4, associativity: 1, writePolicy: WritePolicy.WriteBack);

        cache.Read(0, memory);
        cache.WriteBack(2, 9, memory);
        Assert.Equal(0, memory.Read(2));

        cache.FlushDirtyBlocks(memory);

        Assert.Equal(9, memory.Read(2));
    }

    [Fact]
    public void WriteBuffer_Enqueue_CoalescesWritesToSameAddress()
    {
        var buffer = new WriteBuffer(capacity: 2);

        Assert.True(buffer.Enqueue(5, 10));
        Assert.True(buffer.Enqueue(5, 20));
        Assert.Equal(1, buffer.Count);

        Assert.True(buffer.Enqueue(6, 30));
        Assert.True(buffer.IsFull);

        Assert.False(buffer.Enqueue(7, 40));
    }

    [Fact]
    public void WriteBuffer_DrainOne_WritesOldestEntryToMemory()
    {
        var memory = new Memory(16);
        var buffer = new WriteBuffer(capacity: 2);
        buffer.Enqueue(5, 20);
        buffer.Enqueue(6, 30);

        bool drained = buffer.DrainOne(memory);

        Assert.True(drained);
        Assert.Equal(20, memory.Read(5));
        Assert.Equal(0, memory.Read(6));
        Assert.Equal(1, buffer.Count);
    }

    [Fact]
    public void Cache_WithWriteBuffer_DecouplesStoreFromMemoryWrite()
    {
        var memory = new Memory(16);
        var cache = new Cache(
            numSets: 4, blockSize: 4, associativity: 1,
            writePolicy: WritePolicy.WriteThrough,
            useWriteBuffer: true, writeBufferCapacity: 4);

        cache.WriteThrough(2, 123, memory);

        Assert.Equal(0, memory.Read(2));
        Assert.Equal(1, cache.WriteBufferCount);

        bool drained = cache.TickWriteBuffer(memory);

        Assert.True(drained);
        Assert.Equal(123, memory.Read(2));
        Assert.Equal(0, cache.WriteBufferCount);
    }

    [Fact]
    public void Cache_FlushWriteBuffer_DrainsAllEntriesAtOnce()
    {
        var memory = new Memory(16);
        var cache = new Cache(
            numSets: 4, blockSize: 4, associativity: 1,
            writePolicy: WritePolicy.WriteThrough,
            useWriteBuffer: true, writeBufferCapacity: 4);

        cache.WriteThrough(1, 11, memory);
        cache.WriteThrough(2, 22, memory);
        cache.WriteThrough(3, 33, memory);

        cache.FlushWriteBuffer(memory);

        Assert.Equal(11, memory.Read(1));
        Assert.Equal(22, memory.Read(2));
        Assert.Equal(33, memory.Read(3));
        Assert.Equal(0, cache.WriteBufferCount);
    }

    [Fact]
    public void Reset_ClearsDirtyBitsAndWriteBuffer()
    {
        var memory = new Memory(16);
        var cache = new Cache(
            numSets: 1, blockSize: 4, associativity: 1,
            writePolicy: WritePolicy.WriteBack,
            useWriteBuffer: true, writeBufferCapacity: 4);

        cache.Read(0, memory);
        cache.WriteBack(0, 5, memory);
        Assert.Equal(1, cache.Hits);

        cache.Reset();

        Assert.Equal(0, cache.Hits);
        Assert.Equal(0, cache.Misses);
        Assert.Equal(0, cache.WriteBacks);
        Assert.Equal(0, cache.WriteBufferCount);
        foreach (var block in cache.GetAllBlocks())
        {
            Assert.False(block.Valid);
            Assert.False(block.Dirty);
        }
    }
}