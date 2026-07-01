namespace RiscEmulator.Logic;

public class LruApproximateReplacementPolicy : ICacheReplacementPolicy
{
    private int[] _clockPointers = Array.Empty<int>();

    public void RecordAccess(CacheBlock[] set, int setIndex, int wayIndex, int accessTime)
    {
        set[wayIndex].ReferenceBit = true;
    }

    public int SelectVictim(CacheBlock[] set, int setIndex)
    {
        int ptr = _clockPointers[setIndex];
        while (set[ptr].ReferenceBit)
        {
            set[ptr].ReferenceBit = false;
            ptr = (ptr + 1) % set.Length;
        }
        _clockPointers[setIndex] = (ptr + 1) % set.Length;
        return ptr;
    }

    public void Reset(int numSets)
    {
        _clockPointers = new int[numSets];
    }
}
