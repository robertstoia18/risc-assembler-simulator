namespace RiscEmulator.Logic;

public class Memory
{
    private readonly int[] _words;

    public int Size => _words.Length;

    public Memory(int wordCount = 4096)
    {
        _words = new int[wordCount];
    }

    public int Read(int wordAddress)
    {
        if (wordAddress < 0 || wordAddress >= _words.Length)
            return 0;
        return _words[wordAddress];
    }

    public void Write(int wordAddress, int value)
    {
        if (wordAddress < 0 || wordAddress >= _words.Length)
            return;
        _words[wordAddress] = value;
    }

    public void LoadProgram(List<(int address, int word)> words)
    {
        foreach (var (addr, word) in words)
            Write(addr, word);
    }

    public void Reset()
    {
        Array.Clear(_words, 0, _words.Length);
    }

    public int[] GetWindow(int startAddress, int count)
    {
        int[] window = new int[count];
        for (int i = 0; i < count; i++)
            window[i] = Read(startAddress + i);
        return window;
    }
}
