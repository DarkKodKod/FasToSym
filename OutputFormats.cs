namespace FasToSym;

public enum OutputSymFormats
{
    None = 0,
    NoCashGba,
    Mesen,
    Max
}

public static class FormatUtils
{
    public static OutputSymFormats ConvertToValidFormat(string input)
    {
        if (input.Equals(OutputSymFormats.NoCashGba.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return OutputSymFormats.NoCashGba;
        }
        else if (input.Equals(OutputSymFormats.Mesen.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return OutputSymFormats.Mesen;
        }

        return OutputSymFormats.None;
    }
}
