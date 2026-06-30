# Cache Set-Asociativ Parametrizabil cu Random Replacement

## Ce am făcut
Implementare cache set-asociativ complet parametrizabil (fără hardcoding) cu algoritm de înlocuire random.

## Fișiere Modificate

#### `RiscEmulator.Logic/CacheBlock.cs`
- Added `LastAccessTime` property to track when blocks were last accessed
- Updated constructor to initialize all properties

#### `RiscEmulator.Logic/Cache.cs`
**Major refactoring to support set-associative cache:**

- **Configuration Parameters:**
  - `numSets`: Number of cache sets (default: 16)
  - `blockSize`: Number of words per block (default: 4)
  - `associativity`: Number of ways/blocks per set (default: 1 for direct-mapped)

- **Data Structure Changes:**
  - Changed from single array `_blocks[]` to 2D array `_sets[][]`
  - Each set contains multiple blocks (based on associativity)

- **Random Replacement Policy:**
  - Uses `Random` class to select victim block when set is full
  - Searches for invalid blocks first before replacing
  - If all blocks valid, randomly selects one to evict

- **New Methods:**
  - `GetAllBlocks()`: Enumerates all cache blocks across all sets
  - `GetSet(int setIndex)`: Returns all blocks in a specific set
  - `Associativity` property: Exposes the associativity parameter

- **Updated Methods:**
  - `TryRead()`: Searches all blocks in the set for tag match
  - `LoadBlock()`: Implements random replacement when set is full
  - `WriteThrough()`: Updates the correct block in set-associative structure
- `Read()`: Updated to work with set-associative structure
  - `Reset()`: Resets all blocks in all sets

#### `RiscEmulator.Logic/ProcessorState.cs`
- **REFACTORED**: Constructor now accepts cache configuration parameters (FULLY PARAMETERIZABLE):
  ```csharp
  public ProcessorState(
      int iCacheNumSets = 16,
 int iCacheBlockSize = 4,
      int iCacheAssociativity = 2,
      int dCacheNumSets = 16,
 int dCacheBlockSize = 4,
      int dCacheAssociativity = 2)
  {
  ICache = new Cache(iCacheNumSets, iCacheBlockSize, iCacheAssociativity);
      DCache = new Cache(dCacheNumSets, dCacheBlockSize, dCacheAssociativity);
      // ...
 }
  ```
- **NO MORE HARDCODED VALUES!** ✅ Fully flexible cache configuration

#### `RiscEmulator.UI/ViewModels/MainViewModel.cs`
- **NEW**: Cache configuration properties exposed to UI:
  - `ICacheNumSets`, `ICacheBlockSize`, `ICacheAssociativity`
  - `DCacheNumSets`, `DCacheBlockSize`, `DCacheAssociativity`
- **NEW**: `ReconfigureCacheCommand` - allows runtime cache reconfiguration
- **NEW**: `ICacheConfig` and `DCacheConfig` - display current configuration
- Updated cache block initialization to handle variable number of blocks
- Modified cache display logic to iterate through sets and ways
- Dynamic reinitialization when cache parameters change

### 2. New Files

#### `RiscEmulator.Tests/CacheTests.cs`
Comprehensive test suite including:
- `DirectMapped_Cache_Works`: Tests 1-way associativity (direct-mapped)
- `TwoWay_SetAssociative_Cache_Works`: Tests 2-way set-associative
- `FourWay_SetAssociative_Cache_Works`: Tests 4-way set-associative
- `Cache_EightWay_SetAssociative_Works`: Tests 8-way set-associative (NEW)
- `Cache_Random_Replacement_Works`: Verifies randomreplacement policy
- `Cache_WriteThrough_Works`: Tests write-through behavior
- `Cache_Reset_Works`: Tests cache reset functionality
- `Cache_GetSet_ReturnsCorrectBlocks`: Tests GetSet method
- `Cache_Parameterizable_Configuration`: Tests cache parameter flexibility (NEW)
- `ProcessorState_Parameterizable_CacheConfiguration`: Tests ProcessorState parameterization (NEW)

All tests pass successfully (10/10). ✅

## Features

### Parameterizable Configuration
The cache can be configured with any combination of:
- Number of sets (power of 2 recommended)
- Block size (words per block)
- Associativity (1 = direct-mapped, 2 = 2-way, 4 = 4-way, etc.)

### Random Replacement Policy
- When a set is full and a new block needs to be loaded:
  1. First checks for any invalid (unused) blocks
  2. If all blocks are valid, randomly selects a victim
- Simple but effective policy that avoids worst-case scenarios

### Address Mapping
Maintains the standard cache addressing:
- **Offset**: `wordAddress % blockSize`
- **Index**: `(wordAddress / blockSize) % numSets`
- **Tag**: `wordAddress / (blockSize * numSets)`

## Usage Examples

```csharp
// Direct-mapped (1-way)
var directCache = new Cache(numSets: 16, blockSize: 4, associativity: 1);

// 2-way set-associative
var twoWayCache = new Cache(numSets: 16, blockSize: 4, associativity: 2);

// 4-way set-associative
var fourWayCache = new Cache(numSets: 16, blockSize: 4, associativity: 4);

// Fully associative (1 set with N ways)
var fullyAssoc = new Cache(numSets: 1, blockSize: 4, associativity: 64);
```

## Performance Considerations

- **Associativity = 1**: Direct-mapped, fastest access, most conflicts
- **Associativity = 2-4**: Good balance of speed and hit rate
- **Higher associativity**: Better hit rate, slower search time

## Testing

All tests pass successfully. Run tests with:
```
dotnet test --filter "FullyQualifiedName~RiscEmulator.Tests.CacheTests"
```

## Branch Information

Branch: `feature/set-associative-cache`
Base: `feature/set-associative-cache` (created from current state)

## Build Status

✅ Build successful
✅ All tests passing (10/10) 🎉
✅ No warnings or errors
✅ **FULLY PARAMETERIZABLE** - No hardcoded values!
