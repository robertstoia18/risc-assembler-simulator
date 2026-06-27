namespace RiscEmulator.Logic;

public class AssemblerException : Exception
{
    public int Line { get; }
    public AssemblerException(int line, string message) : base($"Linia {line}: {message}")
    {
        Line = line;
    }
}

public class AssemblerResult
{
    public List<(int Address, int Word)> Words { get; } = new();
    public List<(int Address, Instruction Instr)> Instructions { get; } = new();
}

public class Assembler
{
    public AssemblerResult Assemble(string source, int startAddress = 0)
    {
        var lines = source.Split('\n')
            .Select(l => l.Trim())
            .ToArray();

        var labels = Pass1(lines, startAddress);
        return Pass2(lines, labels, startAddress);
    }

    private Dictionary<string, int> Pass1(string[] lines, int startAddress)
    {
        var labels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int address = startAddress;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = StripComment(lines[i]);
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.EndsWith(':'))
            {
                string label = line[..^1].Trim();
                if (!IsValidLabel(label))
                    throw new AssemblerException(i + 1, $"Etichetă invalidă: '{label}'");
                if (labels.ContainsKey(label))
                    throw new AssemblerException(i + 1, $"Etichetă duplicată: '{label}'");
                labels[label] = address;
                continue;
            }

            if (line.Contains(':'))
            {
                int colonIdx = line.IndexOf(':');
                string label = line[..colonIdx].Trim();
                if (IsValidLabel(label))
                {
                    if (labels.ContainsKey(label))
                        throw new AssemblerException(i + 1, $"Etichetă duplicată: '{label}'");
                    labels[label] = address;
                    line = line[(colonIdx + 1)..].Trim();
                    if (string.IsNullOrWhiteSpace(line)) continue;
                }
            }

            address += CountWords(line, i + 1);
        }

        return labels;
    }

    private AssemblerResult Pass2(string[] lines, Dictionary<string, int> labels, int startAddress)
    {
        var result = new AssemblerResult();
        int address = startAddress;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = StripComment(lines[i]);
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.EndsWith(':')) continue;

            if (line.Contains(':'))
            {
                int colonIdx = line.IndexOf(':');
                string possibleLabel = line[..colonIdx].Trim();
                if (IsValidLabel(possibleLabel))
                {
                    line = line[(colonIdx + 1)..].Trim();
                    if (string.IsNullOrWhiteSpace(line)) continue;
                }
            }

            var (instr, words) = EncodeLine(line, address, labels, i + 1);
            instr.OriginAddress = address;

            result.Instructions.Add((address, instr));
            foreach (var w in words)
            {
                result.Words.Add((address, w));
                address++;
            }
        }

        return result;
    }

    private (Instruction, List<int>) EncodeLine(string line, int currentAddress, Dictionary<string, int> labels, int lineNum)
    {
        string[] parts = SplitMnemonicAndOperands(line);
        string mnem = parts[0].ToUpper();
        string operands = parts.Length > 1 ? parts[1] : "";

        switch (mnem)
        {
            case "NOP":
                return (Instruction.MakeNop(), new List<int> { EncodeClass4(0b0001, 0) });

            case "HALT":
                {
                    var instr = new Instruction { Op = Opcode.HALT, Class = InstructionClass.Class4 };
                    return (instr, new List<int> { EncodeClass4(0b0010, 0) });
                }

            case "LD":
                return EncodeLD(operands, lineNum);

            case "ST":
                return EncodeST(operands, lineNum);

            case "ADD":
            case "SUB":
            case "AND":
            case "OR":
            case "XOR":
                return EncodeAlu(mnem, operands, lineNum);

            case "CLR":
            case "NEG":
            case "INC":
            case "DEC":
            case "ASL":
            case "ASR":
            case "LSR":
            case "ROL":
            case "ROR":
            case "RLC":
            case "RRC":
            case "PUSH":
            case "POP":
            case "JMP":
            case "JAL":
                return EncodeClass2(mnem, operands, labels, lineNum);

            case "BNE":
            case "BEQ":
            case "BL":
            case "BGE":
                return EncodeClass3(mnem, operands, currentAddress, labels, lineNum);

            default:
                throw new AssemblerException(lineNum, $"Mnemonică necunoscută: '{mnem}'");
        }
    }

    private (Instruction, List<int>) EncodeLD(string operands, int lineNum)
    {
        var ops = SplitOperands(operands);
        if (ops.Length < 2)
            throw new AssemblerException(lineNum, "LD necesită 2 operanzi: Rd, (Rs) sau Rd, offset(Rs)");

        string dst = ops[0].Trim();
        string src = ops[1].Trim();

        bool srcIsIndirect = src.StartsWith('(') && src.EndsWith(')');
        bool srcIsIndexed = src.Contains('(') && src.EndsWith(')') && !src.StartsWith('(');

        if (!srcIsIndirect && !srcIsIndexed)
            throw new AssemblerException(lineNum, "LD: sursa trebuie să fie indirectă: (Rs) sau offset(Rs)");

        var instr = new Instruction { Op = Opcode.LD, Class = InstructionClass.Class1 };
        instr.Rd = ParseRegister(dst, lineNum);
        instr.DestMode = AddressingMode.AD;

        var words = new List<int>();

        if (srcIsIndirect)
        {
            instr.SourceMode = AddressingMode.AI;
            instr.Rs1 = ParseRegister(src[1..^1], lineNum);
        }
        else
        {
            instr.SourceMode = AddressingMode.AX;
            ParseIndexed(src, out int reg, out int imm, lineNum);
            instr.Rs1 = reg;
            instr.Immediate = imm;
        }

        int word = (InstructionSet.GetClass1Opcode(Opcode.LD) << 12)
                 | ((int)instr.SourceMode << 10)
                 | (instr.Rs1 << 6)
                 | ((int)instr.DestMode << 4)
                 | (instr.Rd & 0xF);
        words.Add(word);

        if (instr.SourceMode == AddressingMode.AX)
            words.Add(instr.Immediate & 0xFFFF);

        return (instr, words);
    }

    private (Instruction, List<int>) EncodeST(string operands, int lineNum)
    {
        var ops = SplitOperands(operands);
        if (ops.Length < 2)
            throw new AssemblerException(lineNum, "ST necesită 2 operanzi: (Rd) sau offset(Rd), Rs");

        string dst = ops[0].Trim();
        string src = ops[1].Trim();

        bool dstIsIndirect = dst.StartsWith('(') && dst.EndsWith(')');
        bool dstIsIndexed = dst.Contains('(') && dst.EndsWith(')') && !dst.StartsWith('(');

        if (!dstIsIndirect && !dstIsIndexed)
            throw new AssemblerException(lineNum, "ST: destinația trebuie să fie indirectă: (Rd) sau offset(Rd)");

        var instr = new Instruction { Op = Opcode.ST, Class = InstructionClass.Class1 };
        instr.Rs1 = ParseRegister(src, lineNum);
        instr.SourceMode = AddressingMode.AD;

        var words = new List<int>();

        if (dstIsIndirect)
        {
            instr.DestMode = AddressingMode.AI;
            instr.Rd = ParseRegister(dst[1..^1], lineNum);
        }
        else
        {
            instr.DestMode = AddressingMode.AX;
            ParseIndexed(dst, out int reg, out int imm, lineNum);
            instr.Rd = reg;
            instr.Immediate = imm;
        }

        int word = (InstructionSet.GetClass1Opcode(Opcode.ST) << 12)
                 | ((int)instr.SourceMode << 10)
                 | (instr.Rs1 << 6)
                 | ((int)instr.DestMode << 4)
                 | (instr.Rd & 0xF);
        words.Add(word);

        if (instr.DestMode == AddressingMode.AX)
            words.Add(instr.Immediate & 0xFFFF);

        return (instr, words);
    }

    private (Instruction, List<int>) EncodeAlu(string mnem, string operands, int lineNum)
    {
        var op = Enum.Parse<Opcode>(mnem, true);
        var instr = new Instruction { Op = op, Class = InstructionClass.Class1 };
        var ops = SplitOperands(operands);

        if (ops.Length < 3)
            throw new AssemblerException(lineNum, $"{mnem} necesită 3 operanzi: Rd, Rs1, Rs2 sau Rd, Rs1, #imm");

        instr.Rd = ParseRegister(ops[0], lineNum);
        instr.Rs1 = ParseRegister(ops[1], lineNum);

        string third = ops[2].Trim();
        var words = new List<int>();

        if (third.StartsWith('#'))
        {
            if (!TryParseNumber(third[1..], out int imm))
                throw new AssemblerException(lineNum, $"Valoare imediată invalidă: '{third}'");
            instr.SourceMode = AddressingMode.AM;
            instr.DestMode = AddressingMode.AD;
            instr.Immediate = imm;
            instr.Rs2 = 0;

            int word = (InstructionSet.GetClass1Opcode(op) << 12)
                     | ((int)AddressingMode.AM << 10)
                     | (instr.Rs1 << 6)
                     | ((int)AddressingMode.AD << 4)
                     | (instr.Rd & 0xF);
            words.Add(word);
            words.Add(imm & 0xFFFF);
        }
        else
        {
            instr.Rs2 = ParseRegister(third, lineNum);
            instr.SourceMode = AddressingMode.AD;
            instr.DestMode = AddressingMode.AD;

            int word = (InstructionSet.GetClass1Opcode(op) << 12)
                     | ((int)AddressingMode.AD << 10)
                     | (instr.Rs1 << 6)
                     | ((int)AddressingMode.AD << 4)
                     | (instr.Rd & 0xF);
            words.Add(word);
        }

        return (instr, words);
    }


    private (Instruction, List<int>) EncodeClass2(string mnem, string operands, Dictionary<string, int> labels, int lineNum)
    {
        var op = Enum.Parse<Opcode>(mnem, true);
        var instr = new Instruction { Op = op, Class = InstructionClass.Class2 };

        var ops = SplitOperands(operands);

        AddressingMode mode = AddressingMode.AD;
        int regOrTarget = 0;
        int immediate = 0;
        var words = new List<int>();

        if (op == Opcode.JMP || op == Opcode.JAL)
        {
            if (op == Opcode.JAL)
                instr.Rd = 31;

            if (ops.Length < 1 || string.IsNullOrWhiteSpace(ops[0]))
                throw new AssemblerException(lineNum, $"{mnem} necesită un operand (adresă/etichetă)");

            string target = ops[0].Trim();

            if (target.StartsWith('(') && target.EndsWith(')'))
            {
                mode = AddressingMode.AI;
                regOrTarget = ParseRegister(target[1..^1], lineNum);
            }
            else if (target.Contains('(') && target.EndsWith(')') && !target.StartsWith('('))
            {
                mode = AddressingMode.AX;
                ParseIndexed(target, out regOrTarget, out immediate, lineNum);
            }
            else if (labels.TryGetValue(target, out int labelAddr))
            {
                mode = AddressingMode.AM;
                immediate = labelAddr;
            }
            else if (TryParseNumber(target, out int immVal))
            {
                mode = AddressingMode.AM;
                immediate = immVal;
            }
            else
            {
                throw new AssemblerException(lineNum, $"Operand invalid pentru {mnem}: '{target}' (etichetă negăsită sau număr invalid)");
            }

            instr.Rs1 = regOrTarget;
            instr.SourceMode = mode;
            instr.Immediate = immediate;

            int w = (InstructionSet.GetClass2Opcode(op) << 6)
                  | ((int)mode << 4)
                  | (regOrTarget & 0xF);
            words.Add(w);

            if (mode == AddressingMode.AM || mode == AddressingMode.AX)
                words.Add(immediate & 0xFFFF);
        }
        else
        {
            if (ops.Length < 1 || string.IsNullOrWhiteSpace(ops[0]))
                throw new AssemblerException(lineNum, $"{mnem} necesită un operand registru");

            string operand = ops[0].Trim();
            if (operand.StartsWith('(') && operand.EndsWith(')'))
            {
                mode = AddressingMode.AI;
                regOrTarget = ParseRegister(operand[1..^1], lineNum);
            }
            else
            {
                mode = AddressingMode.AD;
                regOrTarget = ParseRegister(operand, lineNum);
            }

            instr.Rs1 = regOrTarget;
            instr.SourceMode = mode;

            int w = (InstructionSet.GetClass2Opcode(op) << 6)
                  | ((int)mode << 4)
                  | (regOrTarget & 0xF);
            words.Add(w);
        }

        return (instr, words);
    }

    private (Instruction, List<int>) EncodeClass3(string mnem, string operands, int addr, Dictionary<string, int> labels, int lineNum)
    {
        var op = Enum.Parse<Opcode>(mnem, true);
        var instr = new Instruction { Op = op, Class = InstructionClass.Class3 };

        var ops = SplitOperands(operands);
        if (ops.Length < 3)
            throw new AssemblerException(lineNum, $"{mnem} necesită 3 operanzi: Rs1, Rs2, etichetă/offset");

        instr.Rs1 = ParseRegister(ops[0], lineNum);
        instr.Rs2 = ParseRegister(ops[1], lineNum);
        instr.SourceMode = AddressingMode.AD;

        string target = ops[2].Trim();
        int offset;
        int branchSize = 2;

        if (labels.TryGetValue(target, out int targetAddr))
        {
            offset = targetAddr - (addr + branchSize);
        }
        else if (TryParseNumber(target, out int immVal))
        {
            offset = immVal;
        }
        else
        {
            throw new AssemblerException(lineNum, $"Operand invalid pentru {mnem}: '{target}'");
        }

        instr.Offset = offset;

        int word1 = (InstructionSet.GetClass3Opcode(op) << 8)
                  | ((instr.Rs1 & 0xF) << 4)
                  | (instr.Rs2 & 0xF);
        int word2 = offset & 0xFFFF;

        return (instr, new List<int> { word1, word2 });
    }

    private int CountWords(string line, int lineNum)
    {
        string[] parts = SplitMnemonicAndOperands(line);
        string mnem = parts[0].ToUpper();
        string operands = parts.Length > 1 ? parts[1] : "";

        switch (mnem)
        {
            case "NOP":
            case "HALT":
            case "RET":
            case "RETI":
                return 1;

            case "BNE": case "BEQ": case "BL": case "BGE":
                return 2;

            case "ADD": case "SUB": case "AND": case "OR": case "XOR":
            {
                var ops = SplitOperands(operands);
                if (ops.Length >= 3 && ops[2].Trim().StartsWith('#'))
                    return 2;
                return 1;
            }

            case "LD":
            {
                var ops = SplitOperands(operands);
                if (ops.Length < 2) return 1;
                string src = ops[1].Trim();
                bool isIndexed = src.Contains('(') && src.EndsWith(')') && !src.StartsWith('(');
                return isIndexed ? 2 : 1;
            }

            case "ST":
            {
                var ops = SplitOperands(operands);
                if (ops.Length < 1) return 1;
                string dst = ops[0].Trim();
                bool isIndexed = dst.Contains('(') && dst.EndsWith(')') && !dst.StartsWith('(');
                return isIndexed ? 2 : 1;
            }

            case "JMP": case "JAL":
            {
                var ops = SplitOperands(operands);
                if (ops.Length < 1) return 1;
                string t = ops[0].Trim();
                if (t.Contains('(') && t.EndsWith(')') && !t.StartsWith('('))
                    return 2;
                if (!t.StartsWith('('))
                    return 2;
                return 1;
            }

            default:
                return 1;
        }
    }

    private int ParseRegister(string s, int lineNum)
    {
        s = s.Trim();
        if (s.StartsWith("R", StringComparison.OrdinalIgnoreCase) && int.TryParse(s[1..], out int idx))
        {
            if (idx >= 0 && idx <= 31) return idx;
        }
        throw new AssemblerException(lineNum, $"Registru invalid: '{s}' (valid R0-R31)");
    }

    private void ParseIndexed(string s, out int reg, out int imm, int lineNum)
    {
        int paren = s.IndexOf('(');
        string immStr = s[..paren].Trim();
        string regStr = s[(paren + 1)..^1].Trim();
        reg = ParseRegister(regStr, lineNum);
        if (!TryParseNumber(immStr, out imm))
            throw new AssemblerException(lineNum, $"Offset invalid în adresare indexată: '{immStr}'");
    }

    private bool TryParseImmediate(string s, Dictionary<string, int> labels, int currentAddr, out int value)
    {
        if (labels.TryGetValue(s, out value)) return true;
        if (TryParseNumber(s, out value)) return true;
        return false;
    }

    private bool TryParseNumber(string s, out int value)
    {
        s = s.Trim();
        if (s.EndsWith('h') || s.EndsWith('H'))
            return int.TryParse(s[..^1], System.Globalization.NumberStyles.HexNumber, null, out value);
        if (s.StartsWith("0x") || s.StartsWith("0X"))
            return int.TryParse(s[2..], System.Globalization.NumberStyles.HexNumber, null, out value);
        return int.TryParse(s, out value);
    }

    private int EncodeClass4(int subclass, int lowByte)
    {
        return (0b1110 << 12) | ((subclass & 0xF) << 8) | (lowByte & 0xFF);
    }

    private string StripComment(string line)
    {
        int idx = line.IndexOf(';');
        return idx >= 0 ? line[..idx].Trim() : line.Trim();
    }

    private string[] SplitMnemonicAndOperands(string line)
    {
        line = line.Trim();
        int spaceIdx = -1;
        for (int i = 0; i < line.Length; i++)
        {
            if (char.IsWhiteSpace(line[i]))
            {
                spaceIdx = i;
                break;
            }
        }
        if (spaceIdx < 0)
            return new[] { line };
        return new[] { line[..spaceIdx], line[(spaceIdx + 1)..] };
    }

    private string[] SplitOperands(string operands)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < operands.Length; i++)
        {
            if (operands[i] == '(') depth++;
            else if (operands[i] == ')') depth--;
            else if (operands[i] == ',' && depth == 0)
            {
                result.Add(operands[start..i].Trim());
                start = i + 1;
            }
        }
        result.Add(operands[start..].Trim());
        return result.ToArray();
    }

    private bool IsValidLabel(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        if (!char.IsLetter(s[0]) && s[0] != '_') return false;
        return s.All(c => char.IsLetterOrDigit(c) || c == '_');
    }
}
