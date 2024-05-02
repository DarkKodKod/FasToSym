namespace FasToSym;

static internal class HelpContent
{
    static public void Print()
    {
        Console.WriteLine("The symbols generated from fasmarm.exe should use the command line arguments:");
        Console.WriteLine("     -s [filename].fas");
        Console.WriteLine("     -t [outputType]  ");
        
        Console.Write("             valid values: ");
        for (int i = 1; i < (int)OutputSymFormats.Max; i++)
        {
            Console.Write(((OutputSymFormats)i).ToString().ToLower());

            if (i < (int)OutputSymFormats.Max - 1)
                Console.WriteLine(", ");
        }
        Console.WriteLine(Environment.NewLine);
    }
}
