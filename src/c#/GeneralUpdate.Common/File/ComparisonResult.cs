using System.Collections.Generic;

namespace GeneralUpdate.Common;

/// <summary>
/// Result of a comparison between two directories.
/// </summary>
public class ComparisonResult
{
    private readonly List<string> _uniqueToA = new List<string>();
    private readonly List<string> _uniqueToB = new List<string>();
    private readonly List<string> _differentFiles = new List<string>();

    /// <summary>
    /// List of files that are unique to A.
    /// </summary>
    public IReadOnlyList<string> UniqueToA => _uniqueToA.AsReadOnly();
    
    /// <summary>
    /// List of files that are unique to B.
    /// </summary>
    public IReadOnlyList<string> UniqueToB => _uniqueToB.AsReadOnly();
    
    /// <summary>
    /// List of files that are different between A and B.
    /// </summary>
    public IReadOnlyList<string> DifferentFiles => _differentFiles.AsReadOnly();

    public void AddUniqueToA(IEnumerable<string> files) => _uniqueToA.AddRange(files);

    public void AddUniqueToB(IEnumerable<string> files) => _uniqueToB.AddRange(files);

    public void AddDifferentFiles(IEnumerable<string> files) => _differentFiles.AddRange(files);
}