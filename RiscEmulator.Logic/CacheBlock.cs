namespace RiscEmulator.Logic;

public class CacheBlock
{
    public bool Valid { get; set; }
    public bool Dirty { get; set; }
    public int Tag { get; set; }
    public int[] Data { get; }
    public int LastAccessTime { get; set; }
    public bool ReferenceBit { get; set; }

    public CacheBlock(int blockSize)
    {
        Data = new int[blockSize];
        Valid = false;
        Dirty = false;
        Tag = 0;
        LastAccessTime = 0;
        ReferenceBit = false;
    }
}
