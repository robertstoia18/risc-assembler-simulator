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

            case "ADD":
            case "SUB":
            case "AND":
            case "OR":
            case "XOR":
            case "CMP":
            case "MOV":
                return EncodeClass1(mnem, operands, currentAddress, labels, lineNum);

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
            case "CALL":
                return EncodeClass2(mnem, operands, currentAddress, labels, lineNum);

            case "BR":
            case "BNE":
            case "BEQ":
            case "BPL":
            case "BMI":
            case "BCS":
            case "BCC":
            case "BVS":
            case "BVC":
                return EncodeClass3(mnem, operands, currentAddress, labels, lineNum);

            default:
                throw new AssemblerException(lineNum, $"Mnemonică necunoscută: '{mnem}'");
        }
    }

    private (Instruction, List<int>) EncodeClass1(string mnem, string operands, int addr, Dictionary<string, int> labels, int lineNum)
    {
        var op = Enum.Parse<Opcode>(mnem, true);
        var instr = new Instruction { Op = op, Class = InstructionClass.Class1 };

        var ops = SplitOperands(operands);

        if (op == Opcode.MOV)
            return EncodeMov(ops, instr, lineNum);

        if (op == Opcode.CMP)
        {
            if (ops.Length < 2)
                throw new AssemblerException(lineNum, "CMP necesită 2 operanzi: Rs1, Rs2");
            instr.Rs1 = ParseRegister(ops[0], lineNum);
            instr.Rs2 = ParseRegister(ops[1], lineNum);
            instr.Rd = 0;
            instr.SourceMode = AddressingMode.AD;
            instr.DestMode = AddressingMode.AD;
            int cmpWord = (InstructionSet.GetClass1Opcode(Opcode.CMP) << 12)
                        | ((int)AddressingMode.AD << 10)
                        | (instr.Rs1 << 6)
                        | ((int)AddressingMode.AD << 4)
                        | instr.Rs2;
            return (instr, new List<int> { cmpWord });
        }

        if (ops.Length < 3)
            throw new AssemblerException(lineNum, $"{mnem} necesită 3 operanzi: Rd, Rs1, Rs2");

        instr.Rd = ParseRegister(ops[0], lineNum);
        instr.Rs1 = ParseRegister(ops[1], lineNum);
        instr.Rs2 = ParseRegister(ops[2], lineNum);
        instr.SourceMode = AddressingMode.AD;
        instr.DestMode = AddressingMode.AD;

        int word = (InstructionSet.GetClass1Opcode(op) << 12)
                 | ((int)instr.SourceMode << 10)
                 | (instr.Rs1 << 6)
                 | ((int)instr.DestMode << 4)
                 | instr.Rd;

        return (instr, new List<int> { word });
    }

    private (Instruction, List<int>) EncodeMov(string[] ops, Instruction instr, int lineNum)
    {
        if (ops.Length < 2)
            throw new AssemblerException(lineNum, "MOV necesită 2 operanzi");

        string dst = ops[0].Trim();
        string src = ops[1].Trim();

        var words = new List<int>();

        bool dstIsIndirect = dst.StartsWith('(') && dst.EndsWith(')');
        bool dstIsIndexed = dst.Contains('(') && dst.EndsWith(')') && !dst.StartsWith('(');
        bool srcIsIndirect = src.StartsWith('(') && src.EndsWith(')');
        bool srcIsIndexed = src.Contains('(') && src.EndsWith(')') && !src.StartsWith('(');

        if (srcIsIndirect)
        {
            instr.SourceMode = AddressingMode.AI;
            instr.DestMode = AddressingMode.AD;
            instr.Rs1 = ParseRegister(src[1..^1], lineNum);
            instr.Rd = ParseRegister(dst, lineNum);
        }
        else if (srcIsIndexed)
        {
            instr.SourceMode = AddressingMode.AX;
            instr.DestMode = AddressingMode.AD;
            ParseIndexed(src, out int regIdx, out int imm, lineNum);
            instr.Rs1 = regIdx;
            instr.Rd = ParseRegister(dst, lineNum);
            instr.Immediate = imm;
        }
        else if (dstIsIndirect)
        {
            instr.SourceMode = AddressingMode.AD;
            instr.DestMode = AddressingMode.AI;
            instr.Rs1 = ParseRegister(src, lineNum);
            instr.Rd = ParseRegister(dst[1..^1], lineNum);
        }
        else if (dstIsIndexed)
        {
            instr.SourceMode = AddressingMode.AD;
            instr.DestMode = AddressingMode.AX;
            ParseIndexed(dst, out int regIdx, out int imm, lineNum);
            instr.Rd = regIdx;
            instr.Rs1 = ParseRegister(src, lineNum);
            instr.Immediate = imm;
        }
        else
        {
            instr.SourceMode = AddressingMode.AD;
            instr.DestMode = AddressingMode.AD;
            instr.Rs1 = ParseRegister(src, lineNum);
            instr.Rd = ParseRegister(dst, lineNum);
        }

        int word = (InstructionSet.GetClass1Opcode(Opcode.MOV) << 12)
                 | ((int)instr.SourceMode << 10)
                 | (instr.Rs1 << 6)
                 | ((int)instr.DestMode << 4)
                 | instr.Rd;
        words.Add(word);

        if (instr.SourceMode == AddressingMode.AX || instr.DestMode == AddressingMode.AX)
            words.Add(instr.Immediate & 0xFFFF);

        return (instr, words);
    }

    private (Instruction, List<int>) EncodeClass2(string mnem, string operands, int addr, Dictionary<string, int> labels, int lineNum)
    {
        var op = Enum.Parse<Opcode>(mnem, true);
        var instr = new Instruction { Op = op, Class = InstructionClass.Class2 };

        var ops = SplitOperands(operands);

        AddressingMode mode = AddressingMode.AD;
        int regOrTarget = 0;
        int immediate = 0;
        var words = new List<int>();

        if (op == Opcode.JMP || op == Opcode.CALL)
        {
            if (ops.Length < 1 || string.IsNullOrWhiteSpace(ops[0]))
                throw new AssemblerException(lineNum, $"{mnem} necesită un operand (adresă/etichetă)");

            string target = ops[0].Trim();

            if (target.StartsWith('(') && target.EndsWith(')'))
            {
                mode = AddressingMode.AI;
                regOrTarget = ParseRegister(target[1..^1], lineNum);
            }
            else if (target.Contains('(') && target.EndsWith(')'))
            {
                mode = AddressingMode.AX;
                ParseIndexed(target, out regOrTarget, out immediate, lineNum);
            }
            else if (TryParseImmediate(target, labels, addr, out int immVal))
            {
                mode = AddressingMode.AM;
                immediate = immVal;
            }
            else
            {
                throw new AssemblerException(lineNum, $"Operand invalid pentru {mnem}: '{target}'");
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
        if (ops.Length < 1 || string.IsNullOrWhiteSpace(ops[0]))
            throw new AssemblerException(lineNum, $"{mnem} necesită un operand (offset sau etichetă)");

        string target = ops[0].Trim();
        int offset;

        if (labels.TryGetValue(target, out int targetAddr))
        {
            offset = targetAddr - (addr + 1);
        }
        else if (TryParseImmediate(target, labels, addr, out int immVal))
        {
            offset = immVal;
        }
        else
        {
            throw new AssemblerException(lineNum, $"Operand invalid pentru {mnem}: '{target}'");
        }

        if (offset < -128 || offset > 127)
            throw new AssemblerException(lineNum,
                $"Offset {offset} depășește intervalul [-128,127] pentru branch scurt. Folosiți JMP sau mutați eticheta mai aproape.");

        instr.Offset = offset;

        int word = (InstructionSet.GetClass3Opcode(op) << 8) | (offset & 0xFF);
        return (instr, new List<int> { word });
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
            case "CLC": case "CLV": case "CLZ": case "CLS": case "CCC":
            case "SEC": case "SEV": case "SEZ": case "SES": case "SCC":
                return 1;

            case "BR": case "BNE": case "BEQ": case "BPL": case "BMI":
            case "BCS": case "BCC": case "BVS": case "BVC":
                return 1;

            case "ADD": case "SUB": case "AND": case "OR": case "XOR": case "CMP":
                return 1;

            case "MOV":
            {
                var ops = SplitOperands(operands);
                if (ops.Length < 2) return 1;
                string dst = ops[0].Trim();
                string src = ops[1].Trim();
                bool hasExtra = (src.Contains('(') && src.EndsWith(')') && !src.StartsWith('('))
                             || (dst.Contains('(') && dst.EndsWith(')') && !dst.StartsWith('('));
                return hasExtra ? 2 : 1;
            }

            case "JMP": case "CALL":
            {
                var ops = SplitOperands(operands);
                if (ops.Length < 1) return 1;
                string t = ops[0].Trim();
                bool hasExtra = !t.StartsWith('(') || !t.EndsWith(')');
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
            if (idx >= 0 && idx <= 15) return idx;
        }
        throw new AssemblerException(lineNum, $"Registru invalid: '{s}'");
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
