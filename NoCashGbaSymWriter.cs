using System.Collections.ObjectModel;
using System.Text;

namespace FasToSym;

// Reference to No$Gba symbols: https://problemkaputt.de/gbatek-symbolic-debug-info.htm

internal class NoCashGbaSymWriter : IWriter
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

        return WriteToFile(fs, fasFile);
    }

    private bool WriteToFile(FileStream fs, FasFile fasFile)
    {
        foreach (FasFile.AssemblyDump assemblyLine in fasFile.AssemblyDumps)
        {
            if (assemblyLine.address == 0)
                continue;

            byte[] addressBytes = new UTF8Encoding(true).GetBytes(assemblyLine.address.ToString("X8"));
            fs.Write(addressBytes, 0, addressBytes.Length);
            byte[] labelBytes = new UTF8Encoding(true).GetBytes(" TODO");
            fs.Write(labelBytes, 0, labelBytes.Length);
            fs.Write(Newline, 0, Newline.Length);
        }

        return true;
    }
}

//    public bool GenerateSymFile()
//    {
//        Collection<string> currentLabels = [];
//        bool typesFromLabel = false;
//
//        foreach (string[] arrayOfElements in _inputFileLine)
//        {
//            if (arrayOfElements.Length > 2 && arrayOfElements[2] == ".org")
//            {
//                StringBuilder sb = new();
//          
//                sb.Append(arrayOfElements[3].Split('x')[1]);
//                sb.Append(' ');
//                sb.Append(".arm");
//          
//                byte[] info = new UTF8Encoding(true).GetBytes(sb.ToString());
//                fs.Write(info, 0, info.Length);
//                fs.Write(_newline, 0, _newline.Length);
//                continue;
//            }
//
//            if (CacheLabelIfExist(arrayOfElements, ref currentLabels))
//            {
//                continue;
//            }
//
//            bool dataTypeExists = false;
//
//            if (arrayOfElements.Length > 3)
//            {
//                if (arrayOfElements[3] == ".byte")
//                    dataTypeExists = true;
//                if (arrayOfElements[3] == ".long")
//                    dataTypeExists = true;
//                if (arrayOfElements[3] == ".word")
//                    dataTypeExists = true;
//            }
//
//            if (!dataTypeExists)
//                typesFromLabel = false;
//
//            string address = arrayOfElements[0].Split(':')[1];
//
//            if (dataTypeExists && typesFromLabel)
//            {
//                StringBuilder sb = new();
//        
//                sb.Append(arrayOfElements[0].Split(':')[1]);
//                sb.Append(' ');
//        
//                int dataSize = 0;
//        
//                // TODO: Detect for ascii, short
//                if (arrayOfElements[3] == ".byte")
//                {
//                    sb.Append(".byt:");
//                    dataSize = 1;
//                }
//                else if (arrayOfElements[3] == ".long")
//                {
//                    sb.Append(".dbl:");
//                    dataSize = 4;
//                }
//                else if (arrayOfElements[3] == ".word")
//                {
//                    sb.Append(".wrd:");
//                    dataSize = 2;
//                }
//        
//                int countCommas = 1;
//                for (int i = 4; i < arrayOfElements.Length; i++)
//                {
//                    int count = arrayOfElements[i].Count(c => c == ',');
//        
//                    countCommas += count;
//                }
//        
//                sb.Append($"{(countCommas * dataSize):X4}");
//        
//                byte[] info = new UTF8Encoding(true).GetBytes(sb.ToString());
//                fs.Write(info, 0, info.Length);
//                fs.Write(_newline, 0, _newline.Length);
//            }
//        }
//
//        return true;
//    }