using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiscEmulator.Logic;

public class CacheBlock
{
    public bool Valid { get; set; }
    public int Tag { get; set; }
    public int[] Data { get; }
    public int LastAccessTime { get; set; }

    public CacheBlock(int blockSize)
    {
 Data = new int[blockSize];
    Valid = false;
        Tag = 0;
      LastAccessTime = 0;
    }
}