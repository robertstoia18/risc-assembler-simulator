namespace RiscEmulator.Logic;

public interface ICacheReplacementPolicy
{
    void RecordAccess(CacheBlock[] set, int setIndex, int wayIndex, int accessTime);
    int SelectVictim(CacheBlock[] set, int setIndex);
    void Reset(int numSets);
}
