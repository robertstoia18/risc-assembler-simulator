namespace RiscEmulator.Logic;

public class Cache
{
    private readonly CacheBlock[][] _sets;
    private readonly int _blockSize;
    private readonly int _numSets;
    private readonly int _associativity;
    private readonly ICacheReplacementPolicy _policy;
    private readonly WritePolicy _writePolicy;
    private readonly WriteBuffer? _writeBuffer;
    private int _accessCounter;

    public int Hits { get; private set; }
    public int Misses { get; private set; }
    public int TotalAccesses => Hits + Misses;
    public double HitRate => TotalAccesses == 0 ? 0.0 : (double)Hits / TotalAccesses;
    public int WriteBacks { get; private set; }

    public Cache(
        int numSets = 16,
        int blockSize = 4,
        int associativity = 1,
        ICacheReplacementPolicy? policy = null,
        WritePolicy writePolicy = WritePolicy.WriteThrough,
        bool useWriteBuffer = false,
        int writeBufferCapacity = 4)
    {
        _numSets = numSets;
        _blockSize = blockSize;
        _associativity = associativity;
        _policy = policy ?? new RandomReplacementPolicy();
        _policy.Reset(numSets);
        _writePolicy = writePolicy;
        _accessCounter = 0;
        _writeBuffer = useWriteBuffer ? new WriteBuffer(writeBufferCapacity) : null;

        _sets = new CacheBlock[numSets][];
        for (int i = 0; i < numSets; i++)
        {
            _sets[i] = new CacheBlock[associativity];
            for (int j = 0; j < associativity; j++)
                _sets[i][j] = new CacheBlock(blockSize);
        }
    }

    private int GetIndex(int wordAddress) => (wordAddress / _blockSize) % _numSets;
    private int GetTag(int wordAddress) => wordAddress / (_blockSize * _numSets);
    private int GetOffset(int wordAddress) => wordAddress % _blockSize;
    private int GetBlockStartAddress(int tag, int setIndex) => (tag * _numSets + setIndex) * _blockSize;

    public bool TryRead(int wordAddress, out int value)
    {
        int idx = GetIndex(wordAddress);
        int tag = GetTag(wordAddress);
        int offset = GetOffset(wordAddress);
        var set = _sets[idx];

        _accessCounter++;

        for (int i = 0; i < _associativity; i++)
        {
            var block = set[i];
            if (block.Valid && block.Tag == tag)
            {
                value = block.Data[offset];
                _policy.RecordAccess(set, idx, i, _accessCounter);
                Hits++;
                return true;
            }
        }

        value = 0;
        Misses++;
        return false;
    }

    public void LoadBlock(int wordAddress, Memory memory)
    {
        int idx = GetIndex(wordAddress);
        int tag = GetTag(wordAddress);
        int blockStart = wordAddress - GetOffset(wordAddress);
        var set = _sets[idx];

        _accessCounter++;

        int targetWayIndex = -1;
        for (int i = 0; i < _associativity; i++)
        {
            if (!set[i].Valid)
            {
                targetWayIndex = i;
                break;
            }
        }

        if (targetWayIndex < 0)
            targetWayIndex = _policy.SelectVictim(set, idx);

        var targetBlock = set[targetWayIndex];
        WriteBackIfDirty(targetBlock, idx, memory);

        targetBlock.Tag = tag;
        targetBlock.Valid = true;
        targetBlock.Dirty = false;
        _policy.RecordAccess(set, idx, targetWayIndex, _accessCounter);
        for (int i = 0; i < _blockSize; i++)
            targetBlock.Data[i] = memory.Read(blockStart + i);
    }

    private void WriteBackIfDirty(CacheBlock block, int setIndex, Memory memory)
    {
        if (_writePolicy != WritePolicy.WriteBack || !block.Valid || !block.Dirty)
            return;

        int evictedBlockStart = GetBlockStartAddress(block.Tag, setIndex);
        for (int i = 0; i < _blockSize; i++)
            CommitToMemory(evictedBlockStart + i, block.Data[i], memory);

        WriteBacks++;
        block.Dirty = false;
    }

    private void CommitToMemory(int wordAddress, int value, Memory memory)
    {
        if (_writeBuffer == null)
        {
            memory.Write(wordAddress, value);
            return;
        }

        if (!_writeBuffer.Enqueue(wordAddress, value))
            memory.Write(wordAddress, value);
    }

    public void WriteThrough(int wordAddress, int value, Memory memory)
    {
        CommitToMemory(wordAddress, value, memory);

        int idx = GetIndex(wordAddress);
        int tag = GetTag(wordAddress);
        int offset = GetOffset(wordAddress);
        var set = _sets[idx];

        _accessCounter++;

        for (int i = 0; i < _associativity; i++)
        {
            var block = set[i];
            if (block.Valid && block.Tag == tag)
            {
                block.Data[offset] = value;
                _policy.RecordAccess(set, idx, i, _accessCounter);
                break;
            }
        }
    }

    public void WriteBack(int wordAddress, int value, Memory memory)
    {
        int idx = GetIndex(wordAddress);
        int tag = GetTag(wordAddress);
        int offset = GetOffset(wordAddress);
        var set = _sets[idx];

        _accessCounter++;

        for (int i = 0; i < _associativity; i++)
        {
            var block = set[i];
            if (block.Valid && block.Tag == tag)
            {
                block.Data[offset] = value;
                block.Dirty = true;
                _policy.RecordAccess(set, idx, i, _accessCounter);
                Hits++;
                return;
            }
        }

        Misses++;

        LoadBlock(wordAddress, memory);

        for (int i = 0; i < _associativity; i++)
        {
            var block = set[i];
            if (block.Valid && block.Tag == tag)
            {
                block.Data[offset] = value;
                block.Dirty = true;
                return;
            }
        }
    }

    public void Write(int wordAddress, int value, Memory memory)
    {
        if (_writePolicy == WritePolicy.WriteBack)
            WriteBack(wordAddress, value, memory);
        else
            WriteThrough(wordAddress, value, memory);
    }

    public int Read(int wordAddress, Memory memory)
    {
        if (TryRead(wordAddress, out int value))
            return value;

        LoadBlock(wordAddress, memory);

        int idx = GetIndex(wordAddress);
        int tag = GetTag(wordAddress);
        int offset = GetOffset(wordAddress);
        var set = _sets[idx];

        for (int i = 0; i < _associativity; i++)
        {
            if (set[i].Valid && set[i].Tag == tag)
            {
                value = set[i].Data[offset];
                return value;
            }
        }

        return 0;
    }

    public bool TickWriteBuffer(Memory memory)
    {
        return _writeBuffer?.DrainOne(memory) ?? false;
    }

    public void FlushWriteBuffer(Memory memory)
    {
        _writeBuffer?.DrainAll(memory);
    }

    public void FlushDirtyBlocks(Memory memory)
    {
        if (_writePolicy == WritePolicy.WriteBack)
        {
            for (int i = 0; i < _numSets; i++)
                for (int j = 0; j < _associativity; j++)
                    WriteBackIfDirty(_sets[i][j], i, memory);
        }

        FlushWriteBuffer(memory);
    }

    public void Reset()
    {
        Hits = 0;
        Misses = 0;
        WriteBacks = 0;
        _accessCounter = 0;
        _writeBuffer?.Clear();
        _policy.Reset(_numSets);
        for (int i = 0; i < _numSets; i++)
        {
            for (int j = 0; j < _associativity; j++)
            {
                var block = _sets[i][j];
                block.Valid = false;
                block.Dirty = false;
                block.Tag = 0;
                block.LastAccessTime = 0;
                block.ReferenceBit = false;
                Array.Clear(block.Data, 0, block.Data.Length);
            }
        }
    }

    public IEnumerable<CacheBlock> GetAllBlocks()
    {
        for (int i = 0; i < _numSets; i++)
            for (int j = 0; j < _associativity; j++)
                yield return _sets[i][j];
    }

    public CacheBlock[] GetSet(int setIndex)
    {
        if (setIndex < 0 || setIndex >= _numSets)
            throw new ArgumentOutOfRangeException(nameof(setIndex));
        return _sets[setIndex];
    }

    public int BlockSize => _blockSize;
    public int NumSets => _numSets;
    public int Associativity => _associativity;
    public ICacheReplacementPolicy Policy => _policy;
    public WritePolicy WritePolicyType => _writePolicy;
    public bool HasWriteBuffer => _writeBuffer != null;
    public int WriteBufferCount => _writeBuffer?.Count ?? 0;
    public int WriteBufferCapacity => _writeBuffer?.Capacity ?? 0;
}
