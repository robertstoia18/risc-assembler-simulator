# Set-Associative Cache Implementation with Random Replacement

## Overview
This implementation adds a parameterizable set-associative cache with random replacement policy to the RISC Emulator project.

## Changes Made

### 1. Modified Files

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
- Updated ICache and DCache instantiation to use 2-way set-associative configuration:
  ```csharp
  public Cache ICache { get; } = new Cache(numSets: 16, blockSize: 4, associativity: 2);
  public Cache DCache { get; } = new Cache(numSets: 16, blockSize: 4, associativity: 2);
  ```

#### `RiscEmulator.UI/ViewModels/MainViewModel.cs`
- Updated cache block initialization to handle variable number of blocks
- Modified cache display logic to iterate through sets and ways
- Now correctly displays all blocks from all sets in the UI

### 2. New Files

#### `RiscEmulator.Tests/CacheTests.cs`
Comprehensive test suite including:
- `DirectMapped_Cache_Works`: Tests 1-way associativity (direct-mapped)
- `TwoWay_SetAssociative_Cache_Works`: Tests 2-way set-associative
- `FourWay_SetAssociative_Cache_Works`: Tests 4-way set-associative
- `Cache_Random_Replacement_Works`: Verifies random replacement policy
- `Cache_WriteThrough_Works`: Tests write-through behavior
- `Cache_Reset_Works`: Tests cache reset functionality
- `Cache_GetSet_ReturnsCorrectBlocks`: Tests GetSet method

All tests pass successfully (7/7).

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
✅ All tests passing (7/7)
✅ No warnings or errors
