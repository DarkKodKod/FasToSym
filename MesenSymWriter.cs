using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;

namespace FasToSym;

// Reference to Mesen MLB file format: https://www.mesen.ca/docs/debugging/debuggerintegration.html#mesen-label-files-mlb

internal partial class MesenSymWriter : IWriter
{
    private const string Extension = ".mlb";
    private const string MEMORY = "GbaMemory";
    private const string MEMORY_PrgRom = "GbaPrgRom";
    private const string MEMORY_BootRom = "GbaBootRom";
    private const string MEMORY_SaveRam = "GbaSaveRam";
    private const string MEMORY_IntWorkRam = "GbaIntWorkRam";
    private const string MEMORY_ExtWorkRam = "GbaExtWorkRam";
    private const string MEMORY_VideoRam = "GbaVideoRam";
    private const string MEMORY_SpriteRam = "GbaSpriteRam";
    private const string MEMORY_PaletteRam = "GbaPaletteRam";
    private const string ARM = "_arm";
    private readonly byte[] Newline = Encoding.ASCII.GetBytes(Environment.NewLine);

    private record SymbolLine(ulong Address, string SourceLine, string Region);

    private readonly Collection<SymbolLine> _symbolInformation = [];

    public Collection<string> Errors { get; } = [];

    public bool GenerateFrom(FasFile fasFile)
    {
        if (!fasFile.IsValid())
            return false;

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
        ulong prgRomStartAddress = 0;

        foreach (FasFile.AssemblyDump assemblyLine in fasFile.AssemblyDumps)
        {
            var sourceLines = fasFile.PreprocessedSourceLines.Where(sl => sl.offset == assemblyLine.offsetPreprocessedSourceLine).ToArray();

            if (sourceLines.Length == 0)
                continue;

            if (CollectOrg(sourceLines[0].line, out string arm, ref prgRomStartAddress))
            {
                _symbolInformation.Add(new(assemblyLine.address, arm, MEMORY_PrgRom));
            }

            if (assemblyLine.address == 0)
                continue;

            if (CollectLabels(sourceLines[0].line, out string label))
            {
                _symbolInformation.Add(new(assemblyLine.address - prgRomStartAddress, label, MEMORY_PrgRom));
            }
        }

        return _symbolInformation.Count > 0;
    }

    private static bool CollectOrg(string sourceLine, out string outLabel, ref ulong startingAddress)
    {
        outLabel = string.Empty;

        Regex regex = IsOrg();
        if (regex.IsMatch(sourceLine))
        {
            string value = sourceLine.Split('x')[1];
            ulong decValue = (ulong)Convert.ToInt64(value, 16);
            outLabel = ARM;

            if (startingAddress == 0)
            {
                startingAddress = decValue;
            }

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
        label = label.Remove(index);

        outLabel = label;

        return true;
    }

    private void WriteToFile(FileStream fs)
    {
        foreach (SymbolLine symbol in _symbolInformation)
        {
            byte[] memoryRegionBytes = new UTF8Encoding(true).GetBytes($"{symbol.Region}:");
            fs.Write(memoryRegionBytes, 0, memoryRegionBytes.Length);
            byte[] addressBytes = new UTF8Encoding(true).GetBytes(symbol.Address.ToString("X7") + ":");
            fs.Write(addressBytes, 0, addressBytes.Length);
            byte[] labelBytes = new UTF8Encoding(true).GetBytes($"{symbol.SourceLine}");
            fs.Write(labelBytes, 0, labelBytes.Length);
            fs.Write(Newline, 0, Newline.Length);
        }
    }

    [GeneratedRegex("org 0x[0-9]+")]
    private static partial Regex IsOrg();
}
