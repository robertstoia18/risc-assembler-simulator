using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiscEmulator.Logic;

public class Cache
{
    private readonly CacheBlock[][] _sets;
    private readonly int _blockSize;
    private readonly int _numSets;
    private readonly int _associativity;
    private readonly Random _random;
    private int _accessCounter;

    public int Hits { get; private set; }
    public int Misses { get; private set; }
    public int TotalAccesses => Hits + Misses;
    public double HitRate => TotalAccesses == 0 ? 0.0 : (double)Hits / TotalAccesses;

    public Cache(int numSets = 16, int blockSize = 4, int associativity = 1)
    {
        _numSets = numSets;
   _blockSize = blockSize;
        _associativity = associativity;
        _random = new Random();
   _accessCounter = 0;

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
            block.LastAccessTime = _accessCounter;
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

        CacheBlock? targetBlock = null;
        for (int i = 0; i < _associativity; i++)
        {
if (!set[i].Valid)
    {
         targetBlock = set[i];
  break;
         }
        }

        if (targetBlock == null)
        {
            int victimIndex = _random.Next(_associativity);
   targetBlock = set[victimIndex];
     }

        targetBlock.Tag = tag;
        targetBlock.Valid = true;
        targetBlock.LastAccessTime = _accessCounter;
        for (int i = 0; i < _blockSize; i++)
            targetBlock.Data[i] = memory.Read(blockStart + i);
    }

    public void WriteThrough(int wordAddress, int value, Memory memory)
  {
        memory.Write(wordAddress, value);

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
        block.LastAccessTime = _accessCounter;
break;
          }
        }
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

    public void Reset()
    {
    Hits = 0;
    Misses = 0;
      _accessCounter = 0;
        for (int i = 0; i < _numSets; i++)
        {
 for (int j = 0; j < _associativity; j++)
            {
            var block = _sets[i][j];
         block.Valid = false;
     block.Tag = 0;
  block.LastAccessTime = 0;
       Array.Clear(block.Data, 0, block.Data.Length);
            }
        }
    }

  public IEnumerable<CacheBlock> GetAllBlocks()
    {
     for (int i = 0; i < _numSets; i++)
        {
 for (int j = 0; j < _associativity; j++)
    {
                yield return _sets[i][j];
            }
        }
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
}