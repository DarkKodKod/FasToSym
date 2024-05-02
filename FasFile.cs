using System.Collections.ObjectModel;
using System.Text;

namespace FasToSym;

// Documentation of the FAS file format: https://fossies.org/linux/fasm/tools/fas.txt

public class FasFile
{
    private static readonly Encoding Encoding = Encoding.GetEncoding(65001/*UTF-8*/);
    private const int MayorVersion = 1;
    private const int MinorVersion = 73;

    private static class Utils
    {
        static public string CstrToString(byte[] data)
        {
            int inx = Array.FindIndex(data, 0, (x) => x == 0); //search for 0
            if (inx >= 0)
                return (Encoding.GetString(data, 0, inx));
            else
                return (Encoding.GetString(data));
        }
    }

    private struct Header
    {
        public byte mayorVersion;
        public byte minorVersion;
        public int offsetInputFileNameStringsTable;
        public int offsetOutputFileNameStringsTable;
        public int offsetStringsTable;
        public int lengthStringsTable;
        public int offsetSymbolsTable;
        public int lengthSymbolsTable;
        public int offsetPreprocessedSource;
        public int lengthPreprocessedSource;
        public int offsetAssemblyDump;
        public int lengthAssemblyDump;
        public int offsetSectionNamesTable;
        public int lengthSectionNamesTable;
        public int offsetSymbolReferencesDump;
        public int lengthSymbolReferencesDump;

        public const int Size = 64;
    }

    public class ExtendedSIB
    {
        public short registerCode;
        public short scale;
    }

    public struct SymbolTable
    {
        public long address;
        public short flags;
        public byte dataSize;
        public SymbolValueType addressType;
        public ExtendedSIB extendedSIB;
        public short numberOfPassDefined;
        public short numberOfPassUsed;
        public int relativeSection;
        public int offsetSymbolName;
        public int offsetInPreprocessedSource;

        public const int Size = 32;
    }

    public struct PreprocessedSourceLine
    {
        public int offset;
        public int lineNumber;
        public int position;
        public int offsetOfPreprocessedLine;
        public string line;
        public bool ignoredByAssembler;
    }

    public struct AssemblyDump
    {
        public int offsetOutputFile;
        public int offsetOfLineInPreprocessedSource;
        public long address;
        public ExtendedSIB extendedSIB;
        public int sectionOrExternalSymbol;
        public byte typeOfAddressValue;
        public byte typeOfCode;
        public byte assemblyWasTakingPlaceVirtualBlock;
        public byte higerBitsOfValueOfAddress;

        public const int Size = 28;
    }

    public struct SymbolReferencesDump
    {
        public int offsetSymbol;
        public int offsetStructure;

        public const int Size = 8;
    }

    private readonly string _inputFile = string.Empty;
    private Header _header;
    private readonly Collection<string> _stringTable = [];
    private readonly Collection<SymbolTable> _symbols = [];
    private readonly Collection<int> _sectionNames = [];
    private readonly Collection<PreprocessedSourceLine> _preprocessedSourceLines = [];
    private readonly Collection<AssemblyDump> _assemblyDumps = [];
    private readonly Collection<SymbolReferencesDump> _referencesDump = [];
    private int _offsetOfEndOfAssemblyInOutputFile = 0;

    public Collection<string> Errors { get; } = [];
    public string ParentDirectory { get; } = string.Empty;
    public string FileNameNoExtension { get; } = string.Empty;
    public Collection<string> StringTable { get => _stringTable; }
    public Collection<SymbolTable> SymbolsTable { get => _symbols; }
    public Collection<int> SectionNames { get => _sectionNames; }
    public Collection<PreprocessedSourceLine> PreprocessedSourceLines { get => _preprocessedSourceLines; }
    public Collection<AssemblyDump> AssemblyDumps { get => _assemblyDumps; }
    public Collection<SymbolReferencesDump> SymbolReferencesDumps { get => _referencesDump; }
    public int OffsetOfEndOfAssemblyInOutputFile { get => _offsetOfEndOfAssemblyInOutputFile; }
    public string InputFileName { get; private set; } = string.Empty;
    public string OutputFileName { get; private set; } = string.Empty;

    public FasFile(string fasFilePath)
	{
        if (string.IsNullOrEmpty(fasFilePath) ||
            !File.Exists(fasFilePath))
        {
            Errors.Add("Error: Input file is missing");
            return;
        }

        _inputFile = fasFilePath;

        string fileName = Path.GetFileNameWithoutExtension(fasFilePath);
        string? parentDirectory = Path.GetDirectoryName(fasFilePath);

        if (string.IsNullOrEmpty(parentDirectory))
        {
            Errors.Add("Error: Problem getting the parent directory from the input file.");
            return;
        }

        ParentDirectory = parentDirectory;
        FileNameNoExtension = fileName;

        ProcessFile();
    }

    public bool IsValid()
    {
        return Errors.Count == 0;
    }

    private void ProcessFile()
    {
        using FileStream fs = new (_inputFile, FileMode.Open, FileAccess.Read);
        using BinaryReader br = new(fs);
        
        br.BaseStream.Position = 0;

        if (!ReadHeader(br))
            return;

        if (!ReadStringTable(br))
            return;

        if (!ReadSymbolsTable(br))
            return;

        if (!ReadPreprocessedSource(br))
            return;

        if (!ReadAssemblyDump(br))
            return;

        if (!ReadSectionNamesTable(br))
            return;

        if (!ReadSymbolReferenceDump(br))
            return;

        if (br.BaseStream.Position != br.BaseStream.Length)
        {
            Errors.Add("Error: The end of the file does not match the current read poisition.");
        }   
    }

    // Table 1  Header
    //  /-------------------------------------------------------------------------\
    //  | Offset | Size    | Description                                          |
    //  |========|=========|======================================================|
    //  |   +0   |  dword  | Signature 1A736166h (little-endian).                 |
    //  |--------|---------|------------------------------------------------------|
    //  |   +4   |  byte   | Major version of flat assembler.                     |
    //  |--------|---------|------------------------------------------------------|
    //  |   +5   |  byte   | Minor version of flat assembler.                     |
    //  |--------|---------|------------------------------------------------------|
    //  |   +6   |  word   | Length of header.                                    |
    //  |--------|---------|------------------------------------------------------|
    //  |   +8   |  dword  | Offset of input file name in the strings table.      |
    //  |--------|---------|------------------------------------------------------|
    //  |  +12   |  dword  | Offset of output file name in the strings table.     |
    //  |--------|---------|------------------------------------------------------|
    //  |  +16   |  dword  | Offset of strings table.                             |
    //  |--------|---------|------------------------------------------------------|
    //  |  +20   |  dword  | Length of strings table.                             |
    //  |--------|---------|------------------------------------------------------|
    //  |  +24   |  dword  | Offset of symbols table.                             |
    //  |--------|---------|------------------------------------------------------|
    //  |  +28   |  dword  | Length of symbols table.                             |
    //  |--------|---------|------------------------------------------------------|
    //  |  +32   |  dword  | Offset of preprocessed source.                       |
    //  |--------|---------|------------------------------------------------------|
    //  |  +36   |  dword  | Length of preprocessed source.                       |
    //  |--------|---------|------------------------------------------------------|
    //  |  +40   |  dword  | Offset of assembly dump.                             |
    //  |--------|---------|------------------------------------------------------|
    //  |  +44   |  dword  | Length of assembly dump.                             |
    //  |--------|---------|------------------------------------------------------|
    //  |  +48   |  dword  | Offset of section names table.                       |
    //  |--------|---------|------------------------------------------------------|
    //  |  +52   |  dword  | Length of section names table.                       |
    //  |--------|---------|------------------------------------------------------|
    //  |  +56   |  dword  | Offset of symbol references dump.                    |
    //  |--------|---------|------------------------------------------------------|
    //  |  +60   |  dword  | Length of symbol references dump.                    |
    //  \-------------------------------------------------------------------------/
    private bool ReadHeader(BinaryReader br)
    {
        // Verify signature
        byte[] chunk = br.ReadBytes(4);

        if (chunk.Length < 4)
        {
            Errors.Add("Error: File is empty.");
            return false;
        }

        if (chunk[0] != 'f' ||
            chunk[1] != 'a' ||
            chunk[2] != 's' ||
            chunk[3] != 0x1a)
        {
            Errors.Add("Error: Error in the header.");
            return false;
        }

        _header.mayorVersion = br.ReadByte();
        _header.minorVersion = br.ReadByte();

        if (_header.mayorVersion != MayorVersion ||
            _header.minorVersion != MinorVersion)
        {
            Errors.Add("Error: Unexpected FAS version. " +
                $"The version from the input file is {_header.mayorVersion}.{_header.minorVersion} " +
                $"and the expected version is {MayorVersion}.{MinorVersion}");
            return false;
        }

        int lenghtOfHeader = br.ReadInt16();
        if (lenghtOfHeader != Header.Size)
        {
            Errors.Add("Error: Error in the header.");
            return false;
        }

        _header.offsetInputFileNameStringsTable = br.ReadInt32();
        _header.offsetOutputFileNameStringsTable = br.ReadInt32();
        _header.offsetStringsTable = br.ReadInt32();
        _header.lengthStringsTable = br.ReadInt32();
        _header.offsetSymbolsTable = br.ReadInt32();
        _header.lengthSymbolsTable = br.ReadInt32();
        _header.offsetPreprocessedSource = br.ReadInt32();
        _header.lengthPreprocessedSource = br.ReadInt32();
        _header.offsetAssemblyDump = br.ReadInt32();
        _header.lengthAssemblyDump = br.ReadInt32();
        _header.offsetSectionNamesTable = br.ReadInt32();
        _header.lengthSectionNamesTable = br.ReadInt32();
        _header.offsetSymbolReferencesDump = br.ReadInt32();
        _header.lengthSymbolReferencesDump = br.ReadInt32();

        return true;
    }

    // The strings table contains just a sequence of ASCIIZ strings, which may
    // be referred to by other parts of the file. It contains the names of
    // main input file, the output file, and the names of the sections and
    // external symbols if there were any.
    private bool ReadStringTable(BinaryReader br)
    {
        if (_header.offsetStringsTable != br.BaseStream.Position)
        {
            Errors.Add("Error: String table is read at the wrong position");
            return false;
        }

        byte[] strTableArray = br.ReadBytes(_header.lengthStringsTable);

        if (strTableArray.Length < 1)
        {
            Errors.Add("Error: Error while processing string table.");
            return false;
        }

        int offset = 0;
        int index = 0;

        while (offset < strTableArray.Length)
        {
            byte[] tmp = new byte[strTableArray.Length - offset];
            Array.Copy(strTableArray, offset, tmp, 0, tmp.Length);

            string str = Utils.CstrToString(tmp);

            _stringTable.Add(str);

            if (offset == _header.offsetInputFileNameStringsTable)
                InputFileName = str;
            else if (offset == _header.offsetOutputFileNameStringsTable)
                OutputFileName = str;

            index++;

            if (!string.IsNullOrEmpty(str))
            {
                offset += str.Length + 1 /* plus null terminator */;
            }
        }

        return true;
    }

    // Table 2  Symbol structure
    //  /-------------------------------------------------------------------------\
    //  | Offset | Size  | Description                                            |
    //  |========|=======|========================================================|
    //  |   +0   | qword | Value of symbol.                                       |
    //  |--------|-------|--------------------------------------------------------|
    //  |   +8   | word  | Flags (table 2.1).                                     |
    //  |--------|-------|--------------------------------------------------------|
    //  |  +10   | byte  | Size of data labelled by this symbol (zero means plain |
    //  |        |       | label without size attached).                          |
    //  |--------|-------|--------------------------------------------------------|
    //  |  +11   | byte  | Type of value (table 2.2). Any value other than zero   |
    //  |        |       | means some kind of relocatable symbol.                 |
    //  |--------|-------|--------------------------------------------------------|
    //  |  +12   | dword | Extended SIB, the first two bytes are register codes   |
    //  |        |       | and the second two bytes are corresponding scales.     |
    //  |--------|-------|--------------------------------------------------------|
    //  |  +16   | word  | Number of pass in which symbol was defined last time.  |
    //  |--------|-------|--------------------------------------------------------|
    //  |  +18   | word  | Number of pass in which symbol was used last time.     |
    //  |--------|-------|--------------------------------------------------------|
    //  |  +20   | dword | If the symbol is relocatable, this field contains      |
    //  |        |       | information about section or external symbol, to which |
    //  |        |       | it is relative - otherwise this field has no meaning.  |
    //  |        |       | When the highest bit is cleared, the symbol is         |
    //  |        |       | relative to a section, and the bits 0-30 contain       |
    //  |        |       | the index (starting from 1) in the table of sections.  |
    //  |        |       | When the highest bit is set, the symbol is relative to |
    //  |        |       | an external symbol, and the bits 0-30 contain the      |
    //  |        |       | the offset of the name of this symbol in the strings   |
    //  |        |       | table.                                                 |
    //  |--------|-------|--------------------------------------------------------|
    //  |  +24   | dword | If the highest bit is cleared, the bits 0-30 contain   |
    //  |        |       | the offset of symbol name in the preprocessed source.  |
    //  |        |       | This name is a pascal-style string (byte length        |
    //  |        |       | followed by string data).                              |
    //  |        |       | Zero in this field means an anonymous symbol.          |
    //  |        |       | If the highest bit is set, the bits 0-30 contain the   |
    //  |        |       | offset of the symbol name in the strings table, and    |
    //  |        |       | this name is a zero-ended string in this case (as are  |
    //  |        |       | all the strings there).                                |
    //  |--------|-------|--------------------------------------------------------|
    //  |  +28   | dword | Offset in the preprocessed source of line that defined |
    //  |        |       | this symbol (see table 3).                             |
    //  \-------------------------------------------------------------------------/
    //
    // The symbols table is an array of 32-byte structures, each one in format
    // specified by table 2.
    private bool ReadSymbolsTable(BinaryReader br)
    {
        if (_header.offsetSymbolsTable != br.BaseStream.Position)
        {
            Errors.Add("Error: Symbols table is read at the wrong position");
            return false;
        }

        int numSymbols = _header.lengthSymbolsTable / SymbolTable.Size;

        for (int i = 0; i < numSymbols; i++)
        {
            SymbolTable symbolTable = new()
            {
                address = br.ReadInt64(),
                flags = br.ReadInt16(),
                dataSize = br.ReadByte(),
                addressType = (SymbolValueType)br.ReadByte(),
                extendedSIB = ReadExtendedSIB(br),
                numberOfPassDefined = br.ReadInt16(),
                numberOfPassUsed = br.ReadInt16(),
                relativeSection = br.ReadInt32(),
                offsetSymbolName = br.ReadInt32(),
                offsetInPreprocessedSource = br.ReadInt32()
            };

            _symbols.Add(symbolTable);
        }

        return true;
    }

    // Table 3  Preprocessed line
    //  /--------------------------------------------------------------------------\
    //  | Offset | Size  | Value                                                   |
    //  |========|=================================================================|
    //  |   +0   | dword | When the line was loaded from source, this field        |
    //  |        |       | contains either zero (if it is the line from the main   |
    //  |        |       | input file), or an offset inside the preprocessed       |
    //  |        |       | source to the name of file, from which this line was    |
    //  |        |       | loaded (the name of file is zero-ended string).         |
    //  |        |       | When the line was generated by macroinstruction, this   |
    //  |        |       | field contains offset inside the preprocessed source to |
    //  |        |       | the pascal-style string specifying the name of          |
    //  |        |       | macroinstruction, which generated this line.            |
    //  |--------|-------|---------------------------------------------------------|
    //  |   +4   | dword | Bits 0-30 contain the number of this line.              |
    //  |        |       | If the highest bit is zeroed, this line was loaded from |
    //  |        |       | source.                                                 |
    //  |        |       | If the highest bit is set, this line was generated by   |
    //  |        |       | macroinstruction.                                       |
    //  |--------|-------|---------------------------------------------------------|
    //  |   +8   | dword | If the line was loaded from source, this field contains |
    //  |        |       | the position of the line inside the source file, from   |
    //  |        |       | which it was loaded.                                    |
    //  |        |       | If line was generated by macroinstruction, this field   |
    //  |        |       | contains the offset of preprocessed line, which invoked |
    //  |        |       | the macroinstruction.                                   |
    //  |        |       | If line was generated by instantaneous macro, this      |
    //  |        |       | field is equal to the next one.                         |
    //  |--------|-------|---------------------------------------------------------|
    //  |  +12   | dword | If the line was generated by macroinstruction, this     |
    //  |        |       | field contains offset of the preprocessed line inside   |
    //  |        |       | the definition of macro, from which this one was        |
    //  |        |       | generated.                                              |
    //  |--------|-------|---------------------------------------------------------|
    //  |  +16   | ?     | The tokenized contents of line.                         |
    //  \--------------------------------------------------------------------------/
    //
    // The preprocessed source is a sequence of preprocessed lines, each one
    // in format as defined in table 3.
    private bool ReadPreprocessedSource(BinaryReader br)
    {
        if (_header.offsetPreprocessedSource != br.BaseStream.Position)
        {
            Errors.Add("Error: Preprocessed source is read at the wrong position");
            return false;
        }

        for (int i = 0; i < _header.lengthPreprocessedSource;)
        {
            PreprocessedSourceLine line = new()
            {
                offset = br.ReadInt32(),
                lineNumber = br.ReadInt32(),
                position = br.ReadInt32(),
                offsetOfPreprocessedLine = br.ReadInt32(),
                line = string.Empty,
                ignoredByAssembler = false
            };

            string actualLine = string.Empty;
            bool ignoredByAssembler = false;

            i += ProcessTokenLines(br, ref actualLine, ref ignoredByAssembler);
            i += (sizeof(int) * 4);

            line.line = actualLine;
            line.ignoredByAssembler = ignoredByAssembler;

            _preprocessedSourceLines.Add(line);
        }

        return true;
    }

    // Returns the number of bytes read 
    private static int ProcessTokenLines(BinaryReader br, ref string actualLine, ref bool ignoredByAssembler)
    {
        StringBuilder sb = new();
        int bytesCounter = 0;
        bool tokenUsed = false;

        byte[] ReadBytes(int numberOfBytes)
        {
            bytesCounter += numberOfBytes;
            return br.ReadBytes(numberOfBytes);
        }
        byte ReadByte()
        {
            return ReadBytes(1)[0];
        }

        byte token = ReadByte();

        while (token != 0x00)
        {
            switch (token)
            {
                case 0x1A:  // normal token, followed by size and then the characters
                case 0x3B:  // ignore line
                    {
                        if (token == 0x3B)
                            ignoredByAssembler = true;

                        if (tokenUsed)
                        {
                            sb.Append(' ');
                        }

                        int numberOfCharacter = ReadByte();

                        byte[] characters = ReadBytes(numberOfCharacter);
                        sb.Append(Encoding.GetString(characters));

                        tokenUsed = true;
                    }
                    break;
                case 0x22:  // quote token, followed by a double word representing number of characters
                    {
                        sb.Append(" \'");

                        uint numberOfCharacters = BitConverter.ToUInt32(ReadBytes(4));

                        byte[] characters = ReadBytes(Convert.ToInt32(numberOfCharacters));
                        sb.Append(Encoding.GetString(characters));

                        sb.Append('\'');

                        tokenUsed = true;
                    }
                    break;
                default:    // Non token character
                    {
                        sb.Append(Encoding.GetString([token]));

                        tokenUsed = false;
                    }
                    break;
            }

            token = ReadByte();
        }

        actualLine = sb.ToString();

        return bytesCounter;
    }

    // Table 4  Row of the assembly dump
    //  /-------------------------------------------------------------------------\
    //  | Offset | Size  | Description                                            |
    //  |========|=======|========================================================|
    //  |   +0   | dword | Offset in output file.                                 |
    //  |--------|-------|--------------------------------------------------------|
    //  |   +4   | dword | Offset of line in preprocessed source.                 |
    //  |--------|-------|--------------------------------------------------------|
    //  |   +8   | qword | Value of $ address.                                    |
    //  |--------|-------|--------------------------------------------------------|
    //  |  +16   | dword | Extended SIB for the $ address, the first two bytes    |
    //  |        |       | are register codes and the second two bytes are        |
    //  |        |       | corresponding scales.                                  |
    //  |--------|-------|--------------------------------------------------------|
    //  |  +20   | dword | If the $ address is relocatable, this field contains   |
    //  |        |       | information about section or external symbol, to which |
    //  |        |       | it is relative - otherwise this field is zero.         |
    //  |        |       | When the highest bit is cleared, the address is        |
    //  |        |       | relative to a section, and the bits 0-30 contain       |
    //  |        |       | the index (starting from 1) in the table of sections.  |
    //  |        |       | When the highest bit is set, the address is relative   |
    //  |        |       | to an external symbol, and the bits 0-30 contain the   |
    //  |        |       | the offset of the name of this symbol in the strings   |
    //  |        |       | table.                                                 |
    //  |--------|-------|--------------------------------------------------------|
    //  |  +24   | byte  | Type of $ address value (as in table 2.2).             |
    //  |--------|-------|--------------------------------------------------------|
    //  |  +25   | byte  | Type of code - possible values are 16, 32, and 64.     |
    //  |--------|-------|--------------------------------------------------------|
    //  |  +26   | byte  | If the bit 0 is set, then at this point the assembly   |
    //  |        |       | was taking place inside the virtual block, and the     |
    //  |        |       | offset in output file has no meaning here.             |
    //  |        |       | If the bit 1 is set, the line was assembled at the     |
    //  |        |       | point, which was not included in the output file for   |
    //  |        |       | some other reasons (like inside the reserved data at   |
    //  |        |       | the end of section).                                   |
    //  |--------|-------|--------------------------------------------------------|
    //  |  +27   | byte  | The higher bits of value of $ address.                 |     
    //  \-------------------------------------------------------------------------/
    //
    // The assembly dump contains an array of 28-byte structures, each one in
    // format specified by table 4, and at the end of this array an additional
    // double word containing the offset in output file at which the assembly
    // was ended.
    private bool ReadAssemblyDump(BinaryReader br)
    {
        if (_header.offsetAssemblyDump != br.BaseStream.Position)
        {
            Errors.Add("Error: Assembly dump is read at the wrong position");
            return false;
        }

        int length = _header.lengthAssemblyDump / AssemblyDump.Size;

        for (int i = 0; i < length; i++)
        {
            AssemblyDump assemblyDump = new()
            {
                offsetOutputFile = br.ReadInt32(),
                offsetOfLineInPreprocessedSource = br.ReadInt32(),
                address = br.ReadInt64(),
                extendedSIB = ReadExtendedSIB(br),
                sectionOrExternalSymbol = br.ReadInt32(),
                typeOfAddressValue = br.ReadByte(),
                typeOfCode = br.ReadByte(),
                assemblyWasTakingPlaceVirtualBlock = br.ReadByte(),
                higerBitsOfValueOfAddress = br.ReadByte()
            };

            _assemblyDumps.Add(assemblyDump);
        }

        _offsetOfEndOfAssemblyInOutputFile = br.ReadInt32();

        return true;
    }

    // The section names table exists only when the output format was an object
    // file (ELF or COFF), and it is an array of 4-byte entries, each being an
    // offset of the name of the section in the strings table.
    // The index of section in this table is the same, as the index of section
    // in the generated object file.
    private bool ReadSectionNamesTable(BinaryReader br)
    {
        if (_header.offsetSectionNamesTable != br.BaseStream.Position)
        {
            Errors.Add("Error: Section names is read at the wrong position");
            return false;
        }

        int numSections = _header.lengthSectionNamesTable / sizeof(int);

        for (int i = 0; i < numSections; i++)
        {
            _sectionNames.Add(br.ReadInt32());
        }

        return true;
    }

    // Table 2.1  Symbol flags
    //  /-----------------------------------------------------------------\
    //  | Bit | Value | Description                                       |
    //  |=====|=======|===================================================|
    //  |  0  |     1 | Symbol was defined.                               |
    //  |-----|-------|---------------------------------------------------|
    //  |  1  |     2 | Symbol is an assembly-time variable.              |
    //  |-----|-------|---------------------------------------------------|
    //  |  2  |     4 | Symbol cannot be forward-referenced.              |
    //  |-----|-------|---------------------------------------------------|
    //  |  3  |     8 | Symbol was used.                                  |
    //  |-----|-------|---------------------------------------------------|
    //  |  4  |   10h | The prediction was needed when checking           |
    //  |     |       | whether the symbol was used.                      |
    //  |-----|-------|---------------------------------------------------|
    //  |  5  |   20h | Result of last predicted check for being used.    |
    //  |-----|-------|---------------------------------------------------|
    //  |  6  |   40h | The prediction was needed when checking           |
    //  |     |       | whether the symbol was defined.                   |
    //  |-----|-------|---------------------------------------------------|
    //  |  7  |   80h | Result of last predicted check for being defined. |
    //  |-----|-------|---------------------------------------------------|
    //  |  8  |  100h | The optimization adjustment is applied to         |
    //  |     |       | the value of this symbol.                         |
    //  |-----|-------|---------------------------------------------------|
    //  |  9  |  200h | The value of symbol is negative number encoded    |
    //  |     |       | as two's complement.                              |
    //  |-----|-------|---------------------------------------------------|
    //  | 10  |  400h | Symbol is a special marker and has no value.      |
    //  \-----------------------------------------------------------------/
    [Flags]
    public enum SymbolFlags
    {
        SymbolWasDefined                = 0x000,
        SymbolInAssemblyTimeVariable    = 0x002,
        SymbolCannotBeForwardReferenced = 0x004,
        SymbolWasUsed                   = 0x008,
        PredictionWasNeededWasUsed      = 0x010,
        ResultLastPredictedUsed         = 0x020,
        PredictionWasNeededWasDefined   = 0x040,               
        ResultLastPredictionDefined     = 0x080,
        OptimizationAppliedToValue      = 0x100,
        ValueIsNegativeNumberEncoded    = 0x200,
        SymbolIsSpecialMarker           = 0x400
    }

    // Table 2.2  Symbol value types
    //  /-------------------------------------------------------------------\
    //  | Value | Description                                               |
    //  |=======|===========================================================|
    //  |   0   | Absolute value.                                           |
    //  |-------|-----------------------------------------------------------|
    //  |   1   | Relocatable segment address (only with MZ output).        |
    //  |-------|-----------------------------------------------------------|
    //  |   2   | Relocatable 32-bit address.                               |
    //  |-------|-----------------------------------------------------------|
    //  |   3   | Relocatable relative 32-bit address (value valid only for |
    //  |       | symbol used in the same place where it was calculated,    |
    //  |       | it should not occur in the symbol structure).             |
    //  |-------|-----------------------------------------------------------|
    //  |   4   | Relocatable 64-bit address.                               |
    //  |-------|-----------------------------------------------------------|
    //  |   5   | [ELF only] GOT-relative 32-bit address.                   |
    //  |-------|-----------------------------------------------------------|
    //  |   6   | [ELF only] 32-bit address of PLT entry.                   |
    //  |-------|-----------------------------------------------------------|
    //  |   7   | [ELF only] Relative 32-bit address of PLT entry (value    |
    //  |       | valid only for symbol used in the same place where it     |
    //  |       | was calculated, it should not occur in the symbol         |
    //  |       | structure).                                               |
    //  \-------------------------------------------------------------------/     
    public enum SymbolValueType
    {
        AbsoluteValue                   = 0,
        RelocatableSegmentAddress       = 1,
        Relocatable32bitAddress         = 2,
        RelocatableRelative32bitAddress = 3,
        Relocatable64bitAddress         = 4,
        GOTRelative32bitAddress         = 5,
        AddressPLTEntry                 = 6,
        AddressPLTEntryRelative         = 7
    }

    // Table 2.3  Register codes for extended SIB
    //  /------------------\
    //  | Value | Register |
    //  |=======|==========|
    //  |  23h  | BX       |
    //  |-------|----------|
    //  |  25h  | BP       |
    //  |-------|----------|
    //  |  26h  | SI       |
    //  |-------|----------|
    //  |  27h  | DI       |
    //  |-------|----------|
    //  |  40h  | EAX      |
    //  |-------|----------|
    //  |  41h  | ECX      |
    //  |-------|----------|
    //  |  42h  | EDX      |
    //  |-------|----------|
    //  |  43h  | EBX      |
    //  |-------|----------|
    //  |  44h  | ESP      |
    //  |-------|----------|
    //  |  45h  | EBP      |
    //  |-------|----------|
    //  |  46h  | ESI      |
    //  |-------|----------|
    //  |  47h  | EDI      |
    //  |-------|----------|
    //  |  48h  | R8D      |
    //  |-------|----------|
    //  |  49h  | R9D      |
    //  |-------|----------|
    //  |  4Ah  | R10D     |
    //  |-------|----------|
    //  |  4Bh  | R11D     |
    //  |-------|----------|
    //  |  4Ch  | R12D     |
    //  |-------|----------|
    //  |  4Dh  | R13D     |
    //  |-------|----------|
    //  |  4Eh  | R14D     |
    //  |-------|----------|
    //  |  4Fh  | R15D     |
    //  |-------|----------|
    //  |  80h  | RAX      |
    //  |-------|----------|
    //  |  81h  | RCX      |
    //  |-------|----------|
    //  |  82h  | RDX      |
    //  |-------|----------|
    //  |  83h  | RBX      |
    //  |-------|----------|
    //  |  84h  | RSP      |
    //  |-------|----------|
    //  |  85h  | RBP      |
    //  |-------|----------|
    //  |  86h  | RSI      |
    //  |-------|----------|
    //  |  87h  | RDI      |
    //  |-------|----------|
    //  |  88h  | R8       |
    //  |-------|----------|
    //  |  89h  | R9       |
    //  |-------|----------|
    //  |  8Ah  | R10      |
    //  |-------|----------|
    //  |  8Bh  | R11      |
    //  |-------|----------|
    //  |  8Ch  | R12      |
    //  |-------|----------|
    //  |  8Dh  | R13      |
    //  |-------|----------|
    //  |  8Eh  | R14      |
    //  |-------|----------|
    //  |  8Fh  | R15      |
    //  |-------|----------|
    //  |  94h  | EIP      |
    //  |-------|----------|
    //  |  98h  | RIP      |
    //  \------------------/
    public enum RegisterCode
    {
        None = 0x00,
        BX   = 0x23,
        BP   = 0x25,
        SI   = 0x26,
        DI   = 0x27,
        EAX  = 0x40,
        ECX  = 0x41,
        EDX  = 0x42,
        EBX  = 0x43,
        ESP  = 0x44,
        EBP  = 0x45,
        ESI  = 0x46,
        EDI  = 0x47,
        R8D  = 0x48,
        R9D  = 0x49,
        R10D = 0x4A,
        R11D = 0x4B,
        R12D = 0x4C,
        R13D = 0x4D,
        R14D = 0x4E,
        R15D = 0x4F,
        RAX  = 0x80,
        RCX  = 0x81,
        RDX  = 0x82,
        RBX  = 0x83,
        RSP  = 0x84,
        RBP  = 0x85,
        RSI  = 0x86,
        RDI  = 0x87,
        R8   = 0x88,
        R9   = 0x89,
        R10  = 0x8A,
        R11  = 0x8B,
        R12  = 0x8C,
        R13  = 0x8D,
        R14  = 0x8E,
        R15  = 0x8F,
        EIP  = 0x94,
        RIP  = 0x98
    }

    // The symbol references dump contains an array of 8-byte structures, each
    // one describes an event of some symbol being used.The first double word
    // of such structure contains an offset of symbol in the symbols table,
    // and the second double word is an offset of structure in assembly dump,
    // which specifies at what moment the symbol was referenced.
    private bool ReadSymbolReferenceDump(BinaryReader br)
    {
        if (_header.offsetSymbolReferencesDump != br.BaseStream.Position)
        {
            Errors.Add("Error: Symbol references is read at the wrong position");
            return false;
        }

        int length = _header.lengthSymbolReferencesDump / SymbolReferencesDump.Size;

        for (int i = 0; i < length; i++)
        {
            SymbolReferencesDump referenceDump = new()
            {
                offsetSymbol = br.ReadInt32(),
                offsetStructure = br.ReadInt32()
            };

            _referencesDump.Add(referenceDump);
        }

        return true;
    }

    private static ExtendedSIB ReadExtendedSIB(BinaryReader br)
    {
        ExtendedSIB extendedSIB = new()
        {
            registerCode = br.ReadInt16(),
            scale = br.ReadInt16()
        };

        return extendedSIB;
    }
}
