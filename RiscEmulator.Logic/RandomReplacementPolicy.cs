namespace RiscEmulator.Logic;

public class RandomReplacementPolicy : ICacheReplacementPolicy
{
    private readonly Random _random = new();

    public void RecordAccess(CacheBlock[] set, int setIndex, int wayIndex, int accessTime) { }

    public int SelectVictim(CacheBlock[] set, int setIndex) => _random.Next(set.Length);

    public void Reset(int numSets) { }
}
