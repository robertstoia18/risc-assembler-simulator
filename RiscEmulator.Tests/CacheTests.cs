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

    [Fact]
    public void Cache_Parameterizable_Configuration()
    {
        var cache1 = new Cache(numSets: 8, blockSize: 2, associativity: 1);
        Assert.Equal(8, cache1.NumSets);
        Assert.Equal(2, cache1.BlockSize);
   Assert.Equal(1, cache1.Associativity);

        var cache2 = new Cache(numSets: 32, blockSize: 8, associativity: 4);
   Assert.Equal(32, cache2.NumSets);
        Assert.Equal(8, cache2.BlockSize);
     Assert.Equal(4, cache2.Associativity);
    }

    [Fact]
    public void Cache_EightWay_SetAssociative_Works()
    {
 var memory = new Memory(2048);
        for (int i = 0; i < 2048; i++)
       memory.Write(i, i * 5);

        var cache = new Cache(numSets: 16, blockSize: 4, associativity: 8);

        for (int i = 0; i < 8; i++)
        {
        int addr = i * 64;
        cache.Read(addr, memory);
        }

        Assert.Equal(8, cache.Misses);
    Assert.Equal(0, cache.Hits);

        for (int i = 0; i < 8; i++)
        {
      int addr = i * 64;
   cache.Read(addr, memory);
    }

Assert.Equal(8, cache.Misses);
        Assert.Equal(8, cache.Hits);
    }

    [Fact]
    public void Cache_ReplacementPolicy_IsConfigurable()
    {
        var random = new Cache(numSets: 4, blockSize: 4, associativity: 2, policy: ReplacementPolicy.Random);
        var lruExact = new Cache(numSets: 4, blockSize: 4, associativity: 2, policy: ReplacementPolicy.LruExact);
        var lruApprox = new Cache(numSets: 4, blockSize: 4, associativity: 2, policy: ReplacementPolicy.LruApproximate);

        Assert.Equal(ReplacementPolicy.Random, random.Policy);
        Assert.Equal(ReplacementPolicy.LruExact, lruExact.Policy);
        Assert.Equal(ReplacementPolicy.LruApproximate, lruApprox.Policy);
    }

    [Fact]
    public void Cache_DefaultPolicy_IsRandom()
    {
        var cache = new Cache(numSets: 4, blockSize: 4, associativity: 2);
        Assert.Equal(ReplacementPolicy.Random, cache.Policy);
    }

    [Fact]
    public void LruExact_EvictsLeastRecentlyUsed()
    {
        var memory = new Memory(1024);
        for (int i = 0; i < 1024; i++)
            memory.Write(i, i);

        var cache = new Cache(numSets: 4, blockSize: 4, associativity: 2, policy: ReplacementPolicy.LruExact);

        cache.Read(0, memory);
        cache.Read(64, memory);
        cache.Read(0, memory);

        cache.Read(128, memory);

        int missesBefore = cache.Misses;
        cache.Read(0, memory);
        Assert.Equal(missesBefore, cache.Misses);

        cache.Read(64, memory);
        Assert.Equal(missesBefore + 1, cache.Misses);
    }

    [Fact]
    public void LruApproximate_SecondChance_GivesSecondChance()
    {
        var memory = new Memory(1024);
        for (int i = 0; i < 1024; i++)
            memory.Write(i, i);

        var cache = new Cache(numSets: 4, blockSize: 4, associativity: 2, policy: ReplacementPolicy.LruApproximate);

        cache.Read(0, memory);
        cache.Read(64, memory);

        cache.Read(128, memory);

        cache.Read(256, memory);

        int missesBefore = cache.Misses;
        cache.Read(128, memory);
        Assert.Equal(missesBefore, cache.Misses);

        cache.Read(64, memory);
        Assert.Equal(missesBefore + 1, cache.Misses);
    }

    [Fact]
    public void ProcessorState_Parameterizable_CacheConfiguration()
  {
    var state1 = new ProcessorState(
            iCacheNumSets: 8, iCacheBlockSize: 2, iCacheAssociativity: 1,
 dCacheNumSets: 16, dCacheBlockSize: 4, dCacheAssociativity: 2);

     Assert.Equal(8, state1.ICache.NumSets);
        Assert.Equal(2, state1.ICache.BlockSize);
   Assert.Equal(1, state1.ICache.Associativity);
  Assert.Equal(16, state1.DCache.NumSets);
 Assert.Equal(4, state1.DCache.BlockSize);
     Assert.Equal(2, state1.DCache.Associativity);

   var state2 = new ProcessorState(
    iCacheNumSets: 32, iCacheBlockSize: 8, iCacheAssociativity: 4,
            dCacheNumSets: 32, dCacheBlockSize: 8, dCacheAssociativity: 4);

      Assert.Equal(32, state2.ICache.NumSets);
        Assert.Equal(8, state2.ICache.BlockSize);
   Assert.Equal(4, state2.ICache.Associativity);
 }
}
