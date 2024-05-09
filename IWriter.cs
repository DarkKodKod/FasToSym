using System.Collections.ObjectModel;

namespace FasToSym;

interface IWriter
{
    public Collection<string> Errors { get; }
    bool GenerateFrom(FasFile fasFile);
}
