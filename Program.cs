using FasToSym;

if (args.Length == 0)
{
    Console.WriteLine("No arguments");
    HelpContent.Print();
    return 1;
}

FasFile? fasFile = null;
IWriter? writer = null;
OutputSymFormats outputFormat = OutputSymFormats.None;

// Gather the arguments

for (int i = 0; i < args.Length; i++)
{
    string arg = args[i];

    if (arg == "-i")
    {
        i++;

        string filePath = args[i];

        fasFile = new(filePath);
    }
    else if (arg == "-t")
    {
        i++;

        string outputType = args[i];

        if (outputType.Equals(OutputSymFormats.NoCashGba.ToString(), StringComparison.CurrentCultureIgnoreCase))
            outputFormat = OutputSymFormats.NoCashGba;
    }
}

if (fasFile == null || !fasFile.IsValid())
{
    if (fasFile != null)
    {
        foreach (string error in fasFile.Errors)
        {
            Console.WriteLine(error);
        }
    }

    HelpContent.Print();
    return 1;
}

switch (outputFormat)
{
    case OutputSymFormats.NoCashGba:
        writer = new NoCashGbaSymWriter();
        break;
    case OutputSymFormats.None:
    default:
        Console.WriteLine("No output type format found");
        HelpContent.Print();
        break;
}

if (writer != null)
{
    if (!writer.GenerateFrom(fasFile))
    {
        foreach (string error in writer.Errors)
        {
            Console.WriteLine(error);
        }
        HelpContent.Print();
        return 1;
    }
}

return 0;