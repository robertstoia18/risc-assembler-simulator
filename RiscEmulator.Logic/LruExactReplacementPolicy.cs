namespace RiscEmulator.Logic;

public class LruExactReplacementPolicy : ICacheReplacementPolicy
{
    public void RecordAccess(CacheBlock[] set, int setIndex, int wayIndex, int accessTime)
    {
        set[wayIndex].LastAccessTime = accessTime;
    }

    public int SelectVictim(CacheBlock[] set, int setIndex)
    {
        int lru = 0;
        for (int i = 1; i < set.Length; i++)
            if (set[i].LastAccessTime < set[lru].LastAccessTime)
                lru = i;
        return lru;
    }

    public void Reset(int numSets) { }
}
