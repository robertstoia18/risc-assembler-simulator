using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiscEmulator.Logic;

public class Cache
{
    private readonly CacheBlock[] _blocks;
    private readonly int _blockSize;
    private readonly int _numSets;

    public int Hits { get; private set; }
    public int Misses { get; private set; }
    public int TotalAccesses => Hits + Misses;
    public double HitRate => TotalAccesses == 0 ? 0.0 : (double)Hits / TotalAccesses;

    public Cache(int numSets = 16, int blockSize = 4)
    {
        _numSets = numSets;
        _blockSize = blockSize;
        _blocks = new CacheBlock[numSets];
        for (int i = 0; i < numSets; i++)
            _blocks[i] = new CacheBlock(blockSize);
    }

    private int GetIndex(int wordAddress) => (wordAddress / _blockSize) % _numSets;
    private int GetTag(int wordAddress) => wordAddress / (_blockSize * _numSets);
    private int GetOffset(int wordAddress) => wordAddress % _blockSize;

    public bool TryRead(int wordAddress, out int value)
    {
        int idx = GetIndex(wordAddress);
        int tag = GetTag(wordAddress);
        int offset = GetOffset(wordAddress);
        var block = _blocks[idx];

        if (block.Valid && block.Tag == tag)
        {
            value = block.Data[offset];
            Hits++;
            return true;
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
        var block = _blocks[idx];

        block.Tag = tag;
        block.Valid = true;
        for (int i = 0; i < _blockSize; i++)
            block.Data[i] = memory.Read(blockStart + i);
    }

    public void WriteThrough(int wordAddress, int value, Memory memory)
    {
        memory.Write(wordAddress, value);

        int idx = GetIndex(wordAddress);
        int tag = GetTag(wordAddress);
        int offset = GetOffset(wordAddress);
        var block = _blocks[idx];

        if (block.Valid && block.Tag == tag)
            block.Data[offset] = value;
    }

    public int Read(int wordAddress, Memory memory)
    {
        if (TryRead(wordAddress, out int value))
            return value;

        LoadBlock(wordAddress, memory);
        value = _blocks[GetIndex(wordAddress)].Data[GetOffset(wordAddress)];
        return value;
    }

    public void Reset()
    {
        Hits = 0;
        Misses = 0;
        foreach (var block in _blocks)
        {
            block.Valid = false;
            block.Tag = 0;
            Array.Clear(block.Data, 0, block.Data.Length);
        }
    }

    public IReadOnlyList<CacheBlock> Blocks => _blocks;
    public int BlockSize => _blockSize;
    public int NumSets => _numSets;
}