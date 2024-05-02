using System.Collections.ObjectModel;
using static System.Net.Mime.MediaTypeNames;
using System.Text;

namespace FasToSym;

// Reference to No$Gba symbols: https://problemkaputt.de/gbatek-symbolic-debug-info.htm

internal class NoCashGbaSymWriter : IWriter
{
    private const string Extension = ".sym";

    // .arm          ;following code is in 32bit/ARM format
    // .thumb        ; following code is in 16bit/THUMB format
    // .byt:NNNN     ; next NNNN bytes are 8bit data(dcb lines)
    // .wrd:NNNN     ;next NNNN bytes are 16bit data(dcw lines)
    // .dbl:NNNN     ;next NNNN bytes are 32bit data(dcd lines)
    // .asc:NNNN     ;next NNNN bytes are ascii data(quoted dcb lines)
    // .pool         ;dummy label(indicates that following is literal pool)

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
        }

        return true;
    }
}

//    private readonly Collection<string[]> _inputFileLine = [];
//
//    private readonly byte[] _newline = Encoding.ASCII.GetBytes(Environment.NewLine);
//
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
//            if (currentLabels.Count > 0)
//            {
//                WriteLabels(fs, currentLabels, address);
//
//                typesFromLabel = true;
//            }
//
//            if (dataTypeExists && typesFromLabel)
//            {
//                WriteDataType(arrayOfElements, fs);
//            }
//        }
//
//        return true;
//    }
//
//    private void WriteLabels(FileStream fs, Collection<string> currentLabels, string address)
//    {
//        // write labels
//        foreach (string label in currentLabels)
//        {
//            byte[] addressBytes = new UTF8Encoding(true).GetBytes(address.ToString() + " ");
//            fs.Write(addressBytes, 0, addressBytes.Length);
//            byte[] labelBytes = new UTF8Encoding(true).GetBytes(label.ToString());
//            fs.Write(labelBytes, 0, labelBytes.Length);
//            fs.Write(_newline, 0, _newline.Length);
//        }
//
//        currentLabels.Clear();
//    }
//
//    private void WriteDataType(string[] arrayOfElements, FileStream fs)
//    {
//        StringBuilder sb = new();
//
//        sb.Append(arrayOfElements[0].Split(':')[1]);
//        sb.Append(' ');
//
//        int dataSize = 0;
//
//        // TODO: Detect for ascii, short
//        if (arrayOfElements[3] == ".byte")
//        {
//            sb.Append(".byt:");
//            dataSize = 1;
//        }
//        else if (arrayOfElements[3] == ".long")
//        {
//            sb.Append(".dbl:");
//            dataSize = 4;
//        }
//        else if (arrayOfElements[3] == ".word")
//        {
//            sb.Append(".wrd:");
//            dataSize = 2;
//        }
//
//        int countCommas = 1;
//        for (int i = 4; i < arrayOfElements.Length; i++)
//        {
//            int count = arrayOfElements[i].Count(c => c == ',');
//
//            countCommas += count;
//        }
//
//        sb.Append($"{(countCommas * dataSize):X4}");
//
//        byte[] info = new UTF8Encoding(true).GetBytes(sb.ToString());
//        fs.Write(info, 0, info.Length);
//        fs.Write(_newline, 0, _newline.Length);
//    }