namespace RiscEmulator.Logic;

public class RegisterFile
{
    private readonly int[] _values = new int[32];
    private readonly bool[] _valid = new bool[32];

    public RegisterFile()
    {
        Reset();
    }

    public void Reset()
    {
        for (int i = 0; i < 32; i++)
        {
            _values[i] = 0;
            _valid[i] = true;
        }
    }

    public int Read(int reg)
    {
        if (reg == 0) return 0;
        return _values[reg];
    }

    public void Write(int reg, int value)
    {
        if (reg == 0) return;
        _values[reg] = value;
        _valid[reg] = true;
    }

    public void Invalidate(int reg)
    {
        if (reg == 0) return;
        _valid[reg] = false;
    }

    public bool IsValid(int reg)
    {
        if (reg == 0) return true;
        return _valid[reg];
    }

    public int GetValue(int reg) => _values[reg];
    public bool GetValid(int reg) => _valid[reg];
}
