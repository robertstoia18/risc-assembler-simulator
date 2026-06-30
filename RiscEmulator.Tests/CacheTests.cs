using RiscEmulator.Logic;
using Xunit;

namespace RiscEmulator.Tests;

public class CacheTests
{
    [Fact]
    public void DirectMapped_Cache_Works()
    {
     var memory = new Memory(1024);
        for (int i = 0; i < 1024; i++)
            memory.Write(i, i * 10);

        var cache = new Cache(numSets: 16, blockSize: 4, associativity: 1);

        int val = cache.Read(0, memory);
  Assert.Equal(0, val);
        Assert.Equal(1, cache.Misses);
        Assert.Equal(0, cache.Hits);

        val = cache.Read(1, memory);
        Assert.Equal(10, val);
        Assert.Equal(1, cache.Misses);
        Assert.Equal(1, cache.Hits);
    }

 [Fact]
    public void TwoWay_SetAssociative_Cache_Works()
    {
        var memory = new Memory(1024);
        for (int i = 0; i < 1024; i++)
         memory.Write(i, i * 10);

        var cache = new Cache(numSets: 16, blockSize: 4, associativity: 2);

        int val1 = cache.Read(0, memory);
        Assert.Equal(0, val1);
        Assert.Equal(1, cache.Misses);

        int val2 = cache.Read(64, memory);
        Assert.Equal(640, val2);
        Assert.Equal(2, cache.Misses);

        int val3 = cache.Read(0, memory);
      Assert.Equal(0, val3);
        Assert.Equal(1, cache.Hits);

      int val4 = cache.Read(64, memory);
     Assert.Equal(640, val4);
        Assert.Equal(2, cache.Hits);
    }

    [Fact]
    public void FourWay_SetAssociative_Cache_Works()
    {
      var memory = new Memory(1024);
        for (int i = 0; i < 1024; i++)
       memory.Write(i, i + 100);

        var cache = new Cache(numSets: 8, blockSize: 4, associativity: 4);

      for (int i = 0; i < 4; i++)
     {
            int addr = i * 32;
         int val = cache.Read(addr, memory);
            Assert.Equal(addr + 100, val);
        }

        Assert.Equal(4, cache.Misses);
        Assert.Equal(0, cache.Hits);

        for (int i = 0; i < 4; i++)
        {
            int addr = i * 32;
            int val = cache.Read(addr, memory);
            Assert.Equal(addr + 100, val);
        }

   Assert.Equal(4, cache.Misses);
        Assert.Equal(4, cache.Hits);
    }

    [Fact]
    public void Cache_Random_Replacement_Works()
    {
        var memory = new Memory(1024);
  for (int i = 0; i < 1024; i++)
            memory.Write(i, i * 2);

        var cache = new Cache(numSets: 4, blockSize: 4, associativity: 2);

        for (int i = 0; i < 10; i++)
  {
       int addr = i * 16;
            cache.Read(addr, memory);
 }

        Assert.True(cache.Misses > 0);
      Assert.True(cache.TotalAccesses == 10);
    }

    [Fact]
    public void Cache_WriteThrough_Works()
  {
     var memory = new Memory(1024);
   var cache = new Cache(numSets: 16, blockSize: 4, associativity: 2);

        cache.Read(0, memory);
        Assert.Equal(0, memory.Read(0));

 cache.WriteThrough(0, 42, memory);
  Assert.Equal(42, memory.Read(0));

        int val = cache.Read(0, memory);
        Assert.Equal(42, val);
    }

    [Fact]
    public void Cache_Reset_Works()
    {
        var memory = new Memory(1024);
        for (int i = 0; i < 1024; i++)
      memory.Write(i, i);

        var cache = new Cache(numSets: 16, blockSize: 4, associativity: 2);

        cache.Read(0, memory);
        cache.Read(4, memory);
  Assert.True(cache.TotalAccesses > 0);

     cache.Reset();
        Assert.Equal(0, cache.Hits);
   Assert.Equal(0, cache.Misses);
        Assert.Equal(0, cache.TotalAccesses);
    }

    [Fact]
    public void Cache_GetSet_ReturnsCorrectBlocks()
    {
        var memory = new Memory(1024);
        var cache = new Cache(numSets: 8, blockSize: 4, associativity: 4);

 var set = cache.GetSet(0);
     Assert.NotNull(set);
        Assert.Equal(4, set.Length);

     for (int i = 0; i < 4; i++)
     {
      Assert.False(set[i].Valid);
        }
    }
}
