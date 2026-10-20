using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;

namespace FasToSym;

// Reference to Mesen MLB file format: https://www.mesen.ca/docs/debugging/debuggerintegration.html#mesen-label-files-mlb

internal partial class MesenSymWriter : IWriter
{
    enum MemoryAreaType
    {
        GbaPrgRom,
        GbaExtWorkRam,
        GbaIntWorkRam,
        GbaPaletteRam,
        GbaSpriteRam,
        GbaVideoRam
    }

    private record MemoryArea(MemoryAreaType Area, uint Address, uint EndAddress, string Padding);

    private const string Extension = ".mlb";
    private static readonly MemoryArea MEMORY_PrgRom = new(MemoryAreaType.GbaPrgRom, 0x08000000, 0, "X7");
    private static readonly MemoryArea MEMORY_ExtWorkRam = new(MemoryAreaType.GbaExtWorkRam, 0x02000000, 0x0203FFFF, "X6");
    private static readonly MemoryArea MEMORY_IntWorkRam = new(MemoryAreaType.GbaIntWorkRam, 0x03000000, 0x03007FFF, "X4");
    private static readonly MemoryArea MEMORY_PaletteRam = new(MemoryAreaType.GbaPaletteRam, 0x05000000, 0, "X4");
    private static readonly MemoryArea MEMORY_VideoRAM = new(MemoryAreaType.GbaVideoRam, 0x06000000, 0, "X5");
    private static readonly MemoryArea MEMORY_OAMRam = new(MemoryAreaType.GbaSpriteRam, 0x07000000, 0, "X4");
    private const string ARM = "_arm";
    private readonly byte[] Newline = Encoding.ASCII.GetBytes(Environment.NewLine);

    private record SymbolLine(uint Address, string SourceLine, MemoryArea MemoryArea);

    private readonly Collection<SymbolLine> _symbolInformation = [];

    public Collection<string> Errors { get; } = [];

    public bool GenerateFrom(FasFile fasFile)
    {
        if (!fasFile.IsValid())
        {
            return false;
        }

        string outputFilePath = Path.Combine(fasFile.ParentDirectory, fasFile.FileNameNoExtension + Extension);

        if (File.Exists(outputFilePath))
        {
            File.Delete(outputFilePath);
        }

        using FileStream fs = File.Create(outputFilePath);

        if (!CollectInformation(fasFile))
        {
            Errors.Add("Error: There are no symbols or the symbols could not find its preprocessed source.");
            return false;
        }

        WriteToFile(fs);

        return true;
    }

    private static bool IsIWRAM(uint address)
    {
        return address >= MEMORY_IntWorkRam.Address && address <= MEMORY_IntWorkRam.EndAddress;
    }

    private static bool IsEWRAM(uint address)
    {
        return address >= MEMORY_ExtWorkRam.Address && address <= MEMORY_ExtWorkRam.EndAddress;
    }

    private bool CollectInformation(FasFile fasFile)
    {
        MemoryArea memoryArea = MEMORY_PrgRom;

        foreach (FasFile.AssemblyDump assemblyLine in fasFile.AssemblyDumps)
        {
            FasFile.PreprocessedSourceLine[] sourceLines = [.. fasFile.PreprocessedSourceLines.Where(sl => sl.offset == assemblyLine.OffsetPreprocessedSourceLine)];

            if (sourceLines.Length == 0)
            {
                continue;
            }

            if (assemblyLine.IsOmittedFromBinary)
            {
                continue;
            }

            if (assemblyLine.IsVirtual && assemblyLine.OffsetOutputFile == 0)
            {
                if (!IsIWRAM(assemblyLine.Address32) &&
                    !IsEWRAM(assemblyLine.Address32))
                {
                    continue;
                }
            }

            if (CollectOrg(sourceLines[0].line, ref memoryArea))
            {
                if (memoryArea.Area == MemoryAreaType.GbaPrgRom)
                {
                    _symbolInformation.Add(new(assemblyLine.Address32, ARM, memoryArea));
                }
            }

            if (assemblyLine.Address32 == 0)
            {
                continue;
            }

            if (CollectLabels(sourceLines[0].line, out string label))
            {
                if (assemblyLine.IsVirtual)
                {
                    if (IsIWRAM(assemblyLine.Address32))
                    {
                        _symbolInformation.Add(new(assemblyLine.Address32 - MEMORY_IntWorkRam.Address, label, MEMORY_IntWorkRam));
                    }
                    else if (IsEWRAM(assemblyLine.Address32))
                    {
                        _symbolInformation.Add(new(assemblyLine.Address32 - MEMORY_ExtWorkRam.Address, label, MEMORY_ExtWorkRam));
                    }
                }
                else
                {
                    _symbolInformation.Add(new(assemblyLine.Address32 - memoryArea.Address, label, memoryArea));
                }
            }
        }

        return _symbolInformation.Count > 0;
    }

    private static bool CollectOrg(string sourceLine, ref MemoryArea memoryArea)
    {
        Regex regex = IsOrg();
        if (regex.IsMatch(sourceLine))
        {
            string value = sourceLine.Split('x')[1];

            ulong address = (ulong)Convert.ToInt64(value, 16);

            if (address == MEMORY_PrgRom.Address) { memoryArea = MEMORY_PrgRom; }
            else if (address == MEMORY_ExtWorkRam.Address) { memoryArea = MEMORY_ExtWorkRam; }
            else if (address == MEMORY_IntWorkRam.Address) { memoryArea = MEMORY_IntWorkRam; }
            else if (address == MEMORY_PaletteRam.Address) { memoryArea = MEMORY_PaletteRam; }
            else if (address == MEMORY_VideoRAM.Address) { memoryArea = MEMORY_VideoRAM; }
            else if (address == MEMORY_OAMRam.Address) { memoryArea = MEMORY_OAMRam; }

            return true;
        }

        return false;
    }

    private static bool CollectLabels(string sourceLine, out string outLabel)
    {
        outLabel = string.Empty;

        if (!sourceLine.Contains(':'))
            return false;

        string label = sourceLine;
        int index = label.IndexOf(':');
        label = label[..index];

        outLabel = label;

        return true;
    }

    private void WriteToFile(FileStream fs)
    {
        foreach (SymbolLine symbol in _symbolInformation)
        {
            byte[] memoryRegionBytes = new UTF8Encoding(true).GetBytes($"{Enum.GetName(typeof(MemoryAreaType), symbol.MemoryArea.Area)}:");
            fs.Write(memoryRegionBytes, 0, memoryRegionBytes.Length);
            byte[] addressBytes = new UTF8Encoding(true).GetBytes(symbol.Address.ToString(symbol.MemoryArea.Padding) + ":");
            fs.Write(addressBytes, 0, addressBytes.Length);
            byte[] labelBytes = new UTF8Encoding(true).GetBytes($"{symbol.SourceLine}");
            fs.Write(labelBytes, 0, labelBytes.Length);
            fs.Write(Newline, 0, Newline.Length);
        }
    }

    [GeneratedRegex("org 0x[0-9]+")]
    private static partial Regex IsOrg();
}
