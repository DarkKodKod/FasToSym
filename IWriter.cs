using System.Collections.ObjectModel;

namespace FasToSym;

public interface IWriter
{
    public Collection<string> Errors { get; }
    bool GenerateFrom(FasFile fasFile);
}
