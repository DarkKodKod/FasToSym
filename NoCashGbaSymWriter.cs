using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;

namespace FasToSym;

// Reference to No$Gba symbols: https://problemkaputt.de/gbatek-symbolic-debug-info.htm

internal partial class NoCashGbaSymWriter : IWriter
{
    private const string Extension = ".sym";
    private readonly byte[] Newline = Encoding.ASCII.GetBytes(Environment.NewLine);
    private const string ARM = ".arm";    // following code is in 32bit/ARM format
    private const string THUMB = ".thumb";  // following code is in 16bit/THUMB format
    private const string BYTE = ".byt:";   // next NNNN bytes are 8bit data(dcb lines)
    private const string WORD = ".wrd:";   // next NNNN bytes are 16bit data(dcw lines)
    private const string DOUBLE = ".dbl:";   // next NNNN bytes are 32bit data(dcd lines)
    private const string ASCII = ".asc:";   // next NNNN bytes are ascii data(quoted dcb lines)
    private const string POOL = ".pool";   // dummy label(indicates that following is literal pool)

    private record SymbolLine(ulong Address, string SourceLine);

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
                _symbolInformation.Add(new(assemblyLine.address, sourceLines[0].line));
            }
        }

        return _symbolInformation.Count > 0;
    }

    private void WriteToFile(FileStream fs)
    {
        bool fromLabel = false;
        bool firstOrg = true;

        foreach (SymbolLine symbol in _symbolInformation)
        {
            if (firstOrg == true &&
                TestWriteOrg(fs, in symbol))
            {
                firstOrg = false;
                continue;
            }
            else if (TestWriteCode16(fs, in symbol))
            {
                continue;
            }
            else if (TestLabel(fs, in symbol))
            {
                fromLabel = true;
                continue;
            }
            else if (TestVariables(fs, in symbol))
            {
                fromLabel = false;
                continue;
            }
            else if (fromLabel == true)
            {
                string[] array = symbol.SourceLine.Split(' ');

                if (CheckIfIsDataTypes(array, out string line))
                {
                    WriteLine(fs, symbol.Address, line);
                    continue;
                }
            }

            // normal code.
            fromLabel = false;
        }
    }

    private bool TestVariables(FileStream fs, in SymbolLine symbol)
    {
        string[] array = symbol.SourceLine.Split(' ');

        if (array.Length > 1)
        {
            if (CheckIfIsDataTypes(array[1..], out string line))
            {
                WriteLine(fs, symbol.Address, array[0]);
                WriteLine(fs, symbol.Address, line);

                return true;
            }
        }

        return false;
    }

    private bool TestLabel(FileStream fs, in SymbolLine symbol)
    {
        if (symbol.SourceLine.Contains(':'))
        {
            string[] array = symbol.SourceLine.Split(' ');

            WriteLine(fs, symbol.Address, array[0][..^1]);

            if (array.Length > 1)
            {
                if (CheckIfIsDataTypes(array[1..], out string line))
                {
                    WriteLine(fs, symbol.Address, line);
                }
            }

            return true;
        }

        return false;
    }

    private bool TestWriteCode16(FileStream fs, in SymbolLine symbol)
    {
        if (symbol.SourceLine == "code16" ||
            symbol.SourceLine == "thumb")
        {
            WriteLine(fs, symbol.Address, THUMB);

            return true;
        }

        return false;
    }

    private bool TestWriteOrg(FileStream fs, in SymbolLine symbol)
    {
        Regex regex = IsOrg();
        if (regex.IsMatch(symbol.SourceLine))
        {
            string value = symbol.SourceLine.Split('x')[1];
            int decValue = Convert.ToInt32(value, 16);
            WriteLine(fs, (ulong)decValue, ARM);

            return true;
        }

        return false;
    }

    private void WriteLine(FileStream fs, ulong address, string text)
    {
        byte[] addressBytes = new UTF8Encoding(true).GetBytes(address.ToString("X8"));
        fs.Write(addressBytes, 0, addressBytes.Length);
        byte[] labelBytes = new UTF8Encoding(true).GetBytes($" {text}");
        fs.Write(labelBytes, 0, labelBytes.Length);
        fs.Write(Newline, 0, Newline.Length);
    }

    private bool CheckIfIsDataTypes(in string[] array, out string line)
    {
        line = string.Empty;

        if (array.Length > 0)
        {
            // detect bytes
            if (array[0] == "db" ||
                array[0].StartsWith("db("))
            {
                int size = 1;

                string[] countElements = array[1].Split(',');
                if (countElements.Length > 1)
                {
                    size *= countElements.Length;
                }

                line = BYTE + size.ToString("X4");
            }
            // detect words
            else if (array[0] == "dw" ||
                array[0].StartsWith("dw("))
            {
                int size = 2;

                string[] countElements = array[1].Split(',');
                if (countElements.Length > 1)
                {
                    size *= countElements.Length;
                }

                line = WORD + size.ToString("X4");
            }
            // detect doubles
            else if (array[0] == "dd" ||
                array[0].StartsWith("dd("))
            {
                int size = 4;

                string[] countElements = array[1].Split(',');
                if (countElements.Length > 1)
                {
                    size *= countElements.Length;
                }

                line = DOUBLE + size.ToString("X4");
            }
        }

        return !string.IsNullOrEmpty(line);
    }

    [GeneratedRegex("org 0x[0-9]+")]
    private static partial Regex IsOrg();
}
