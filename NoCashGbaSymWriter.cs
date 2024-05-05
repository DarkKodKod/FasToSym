using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;

namespace FasToSym;

// Reference to No$Gba symbols: https://problemkaputt.de/gbatek-symbolic-debug-info.htm

internal partial class NoCashGbaSymWriter : IWriter
{
    private const string Extension = ".sym";
    private readonly byte[] Newline = Encoding.ASCII.GetBytes(Environment.NewLine);
    private const string ARM    = ".arm";    // following code is in 32bit/ARM format
    private const string THUMB  = ".thumb";  // following code is in 16bit/THUMB format
    private const string BYTE   = ".byt:";   // next NNNN bytes are 8bit data(dcb lines)
    private const string WORD   = ".wrd:";   // next NNNN bytes are 16bit data(dcw lines)
    private const string DOUBLE = ".dbl:";   // next NNNN bytes are 32bit data(dcd lines)
    private const string ASCII  = ".asc:";   // next NNNN bytes are ascii data(quoted dcb lines)
    private const string POOL   = ".pool";   // dummy label(indicates that following is literal pool)

    private struct SymbolLine(ulong address, string sourceLine)
    {
        public ulong address = address;
        public string sourceLine = sourceLine;
    }

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
        foreach (FasFile.AssemblyDump assemblyLine in fasFile.AssemblyDumps)
        {
            var sourceLines = fasFile.PreprocessedSourceLines.Where(sl => sl.offset == assemblyLine.offsetPreprocessedSourceLine).ToArray();

            if (sourceLines.Length > 0)
            {
                _symbolInformation.Add(new SymbolLine(assemblyLine.address, sourceLines[0].line));
            }
        }

        return _symbolInformation.Count > 0;
    }

    private void WriteToFile(FileStream fs)
    {
        foreach (SymbolLine symbol in _symbolInformation)
        {
            TestWriteOrg(fs, in symbol);
            TestWriteCode16(fs, in symbol);
            TestLabel(fs, in symbol);
            TestDataTypes(fs, in symbol);
        }
    }

    private void TestDataTypes(FileStream fs, in SymbolLine symbol)
    {
        string[] array = symbol.sourceLine.Split(' ');
        if (array.Length > 0)
        {
            // detect bytes
            if (array[0] == "db")
            {

            }
            // detect doubles
            else if (array[0] == "dd" ||
                array[0].StartsWith("dd("))
            {
                int size = 4;

                WriteLine(fs, symbol.address, DOUBLE + size.ToString("X4"));
            }
        }
    }

    private void TestLabel(FileStream fs, in SymbolLine symbol)
    {
        if (symbol.sourceLine.Contains(':'))
        {
            string[] array = symbol.sourceLine.Split(' ');

            WriteLine(fs, symbol.address, array[0][..^1]);
        }
    }

    private void TestWriteCode16(FileStream fs, in SymbolLine symbol)
    {
        if (symbol.sourceLine == "code16")
        {
            WriteLine(fs, symbol.address, THUMB);
        }
    }

    private void TestWriteOrg(FileStream fs, in SymbolLine symbol)
    {
        Regex regex = IsOrg();
        if (regex.IsMatch(symbol.sourceLine))
        {
            string value = symbol.sourceLine.Split('x')[1];
            int decValue = Convert.ToInt32(value, 16);
            WriteLine(fs, (ulong)decValue, ARM);
        }
    }

    private void WriteLine(FileStream fs, ulong address, string text)
    {
        byte[] addressBytes = new UTF8Encoding(true).GetBytes(address.ToString("X8"));
        fs.Write(addressBytes, 0, addressBytes.Length);
        byte[] labelBytes = new UTF8Encoding(true).GetBytes($" {text}");
        fs.Write(labelBytes, 0, labelBytes.Length);
        fs.Write(Newline, 0, Newline.Length);
    }

    [GeneratedRegex("org 0x[0-9]+")]
    private static partial Regex IsOrg();
}
