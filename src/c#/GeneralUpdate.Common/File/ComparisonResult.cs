using System.Collections.Generic;

namespace GeneralUpdate.Common;

public class ComparisonResult
{
    private readonly List<string> _uniqueToA = new List<string>();
    private readonly List<string> _uniqueToB = new List<string>();
    private readonly List<string> _differentFiles = new List<string>();

    public IReadOnlyList<string> UniqueToA => _uniqueToA.AsReadOnly();
    public IReadOnlyList<string> UniqueToB => _uniqueToB.AsReadOnly();
    public IReadOnlyList<string> DifferentFiles => _differentFiles.AsReadOnly();

    public void AddUniqueToA(IEnumerable<string> files) => _uniqueToA.AddRange(files);

    public void AddUniqueToB(IEnumerable<string> files) => _uniqueToB.AddRange(files);

    public void AddDifferentFiles(IEnumerable<string> files) => _differentFiles.AddRange(files);
}