using System;
using System.Collections.Generic;

namespace RiscEmulator.Logic;

public readonly struct WriteBufferEntry
{
    public int Address { get; }
    public int Value { get; }

    public WriteBufferEntry(int address, int value)
    {
        Address = address;
        Value = value;
    }
}

public class WriteBuffer
{
    private readonly LinkedList<WriteBufferEntry> _entries = new();
    private readonly Dictionary<int, LinkedListNode<WriteBufferEntry>> _index = new();
    private readonly int _capacity;

    public int Capacity => _capacity;
    public int Count => _entries.Count;
    public bool IsFull => _entries.Count >= _capacity;
    public int DrainedCount { get; private set; }

    public WriteBuffer(int capacity = 4)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public bool Enqueue(int address, int value)
    {
        if (_index.TryGetValue(address, out var existingNode))
        {
            _entries.Remove(existingNode);
            var merged = new LinkedListNode<WriteBufferEntry>(new WriteBufferEntry(address, value));
            _entries.AddLast(merged);
            _index[address] = merged;
            return true;
        }

        if (IsFull)
            return false;

        var node = new LinkedListNode<WriteBufferEntry>(new WriteBufferEntry(address, value));
        _entries.AddLast(node);
        _index[address] = node;
        return true;
    }

    public bool TryPeek(int address, out int value)
    {
        if (_index.TryGetValue(address, out var node))
        {
            value = node.Value.Value;
            return true;
        }

        value = 0;
        return false;
    }

    public bool DrainOne(Memory memory)
    {
        var first = _entries.First;
        if (first == null)
            return false;

        memory.Write(first.Value.Address, first.Value.Value);
        _entries.RemoveFirst();
        _index.Remove(first.Value.Address);
        DrainedCount++;
        return true;
    }

    public void DrainAll(Memory memory)
    {
        while (DrainOne(memory)) { }
    }

    public void Clear()
    {
        _entries.Clear();
        _index.Clear();
    }

    public void ResetStats()
    {
        DrainedCount = 0;
    }
}