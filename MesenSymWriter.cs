using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;

namespace FasToSym;

// Reference to Mesen MLB file format: https://www.mesen.ca/docs/debugging/debuggerintegration.html#mesen-label-files-mlb

internal partial class MesenSymWriter : IWriter
{
    private record RomArea(string Label, string Address);

    private const string Extension = ".mlb";
    private static readonly RomArea MEMORY_PrgRom = new("GbaPrgRom", "08000000");
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
                if (!string.IsNullOrEmpty(arm))
                {
                    _symbolInformation.Add(new(assemblyLine.address, arm, MEMORY_PrgRom.Label));
                }
            }

            if (assemblyLine.address == 0)
                continue;

            if (CollectLabels(sourceLines[0].line, out string label))
            {
                _symbolInformation.Add(new(assemblyLine.address - prgRomStartAddress, label, MEMORY_PrgRom.Label));
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

            if (value == MEMORY_PrgRom.Address)
            {
                outLabel = ARM;
                startingAddress = (ulong)Convert.ToInt64(value, 16); ;
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
        label = label[..index];

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
