using System.Collections.Generic;

namespace GeneralUpdate.Common;

/// <summary>
/// Result of a comparison between two directories.
/// </summary>
public class ComparisonResult
{
    private List<FileNode> _leftNodes;
    private List<FileNode> _rightNodes;
    private List<FileNode> _differentNodes;

    public ComparisonResult()
    {
        _leftNodes = new List<FileNode>();
        _rightNodes = new List<FileNode>();
        _differentNodes = new List<FileNode>();
    }

    /// <summary>
    /// List of files that are unique to A.
    /// </summary>
    public IReadOnlyList<FileNode> LeftNodes => _leftNodes.AsReadOnly();
    
    /// <summary>
    /// List of files that are unique to B.
    /// </summary>
    public IReadOnlyList<FileNode> RightNodes => _rightNodes.AsReadOnly();
    
    /// <summary>
    /// List of files that are different between A and B.
    /// </summary>
    public IReadOnlyList<FileNode> DifferentNodes => _differentNodes.AsReadOnly();

    public void AddToLeft(IEnumerable<FileNode> files) => _leftNodes.AddRange(files);

    public void AddToRight(IEnumerable<FileNode> files) => _rightNodes.AddRange(files);

    public void AddDifferent(IEnumerable<FileNode> files) => _differentNodes.AddRange(files);
}