using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;

namespace FasToSym;

// Reference to Mesen MLB file format: https://www.mesen.ca/docs/debugging/debuggerintegration.html#mesen-label-files-mlb

internal partial class MesenSymWriter : IWriter
{
    enum MemoryArea
    {
        GbaPrgRom = 0,
        GbaExtWorkRam = 1,
        GbaIntWorkRam = 2,
        GbaPaletteRam = 3
    }

    private record RomArea(MemoryArea Area, ulong Address);

    private const string Extension = ".mlb";
    private static readonly RomArea MEMORY_PrgRom = new(MemoryArea.GbaPrgRom, 0x08000000);
    private static readonly RomArea MEMORY_ExtWorkRam = new(MemoryArea.GbaExtWorkRam, 0x02000000);
    private static readonly RomArea MEMORY_IntWorkRam = new(MemoryArea.GbaIntWorkRam, 0x03000000);
    private static readonly RomArea MEMORY_PaletteRam = new(MemoryArea.GbaPaletteRam, 0x05000000);
    private const string ARM = "_arm";
    private readonly byte[] Newline = Encoding.ASCII.GetBytes(Environment.NewLine);

    private record SymbolLine(ulong Address, string SourceLine, RomArea MemoryArea);

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

    private bool CollectInformation(FasFile fasFile)
    {
        RomArea memoryArea = MEMORY_PrgRom;

        foreach (FasFile.AssemblyDump assemblyLine in fasFile.AssemblyDumps)
        {
            var sourceLines = fasFile.PreprocessedSourceLines.Where(sl => sl.offset == assemblyLine.offsetPreprocessedSourceLine).ToArray();

            if (sourceLines.Length == 0)
            {
                continue;
            }   

            if (CollectOrg(sourceLines[0].line, ref memoryArea))
            {
                if (memoryArea.Area == MemoryArea.GbaPrgRom)
                {
                    _symbolInformation.Add(new(assemblyLine.address, ARM, memoryArea));
                }
            }

            if (assemblyLine.address == 0)
            {
                continue;
            }   

            if (CollectLabels(sourceLines[0].line, out string label))
            {
                _symbolInformation.Add(new(assemblyLine.address - memoryArea.Address, label, memoryArea));
            }
        }

        return _symbolInformation.Count > 0;
    }

    private static bool CollectOrg(string sourceLine, ref RomArea memoryArea)
    {
        Regex regex = IsOrg();
        if (regex.IsMatch(sourceLine))
        {
            string value = sourceLine.Split('x')[1];

            ulong address = (ulong)Convert.ToInt64(value, 16);

            if (address == MEMORY_PrgRom.Address)           { memoryArea = MEMORY_PrgRom; }
            else if (address == MEMORY_ExtWorkRam.Address)  { memoryArea = MEMORY_ExtWorkRam; }
            else if (address == MEMORY_IntWorkRam.Address)  { memoryArea = MEMORY_IntWorkRam; }
            else if (address == MEMORY_PaletteRam.Address)  { memoryArea = MEMORY_PaletteRam; }

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
            byte[] memoryRegionBytes = new UTF8Encoding(true).GetBytes($"{Enum.GetName(typeof(MemoryArea), symbol.MemoryArea.Area)}:");
            fs.Write(memoryRegionBytes, 0, memoryRegionBytes.Length);

            string addressSise = symbol.MemoryArea.Area switch
            {
                MemoryArea.GbaPrgRom => "X7",
                MemoryArea.GbaExtWorkRam => "X6",
                MemoryArea.GbaIntWorkRam => "X4",
                MemoryArea.GbaPaletteRam => "X4",
                _ => throw new NotImplementedException()
            };

            byte[] addressBytes = new UTF8Encoding(true).GetBytes(symbol.Address.ToString(addressSise) + ":");
            fs.Write(addressBytes, 0, addressBytes.Length);
            byte[] labelBytes = new UTF8Encoding(true).GetBytes($"{symbol.SourceLine}");
            fs.Write(labelBytes, 0, labelBytes.Length);
            fs.Write(Newline, 0, Newline.Length);
        }
    }

    [GeneratedRegex("org 0x[0-9]+")]
    private static partial Regex IsOrg();
}
