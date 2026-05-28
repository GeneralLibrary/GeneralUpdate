using System;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>
/// Represents a file node within a file tree (binary search tree).
/// Also serves as the fundamental data unit for file snapshots, storing file metadata and tree structure references.
/// </summary>
/// <remarks>
/// <para>
/// FileNode has two core purposes in GeneralUpdate:
/// </para>
/// <list type="bullet">
///   <item><description><b>File metadata container</b>: Records the file name, full path, relative path, SHA-256 hash, and other metadata.</description></item>
///   <item><description><b>Binary search tree node</b>: Uses <see cref="Id"/> as the sort key to build a binary search tree,
///   supporting tree operations such as <see cref="Add"/>, <see cref="Search"/>, and <see cref="SearchParent"/>.</description></item>
/// </list>
/// <para>
/// The <see cref="Equals"/> method performs a case-insensitive comparison based on <see cref="Hash"/> and <see cref="Name"/>,
/// and is used in the <see cref="FileTree.Compare"/> algorithm to determine whether two nodes represent the same file.
/// </para>
/// </remarks>
public class FileNode
{
    #region Public Properties

    /// <summary>
    /// Gets or sets the unique identifier for the file node (the sort key for the binary search tree).
    /// </summary>
    /// <remarks>Assigned in a thread-safe auto-incrementing manner by the <see cref="StorageManager.GetId"/> method.</remarks>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the file name (without the path portion).
    /// </summary>
    /// <example><c>example.dll</c></example>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the full absolute path of the file.
    /// </summary>
    /// <example><c>C:\App\bin\example.dll</c></example>
    public string FullName { get; set; }

    /// <summary>
    /// Gets or sets the directory path where the file is located.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash value of the file (in hexadecimal string format).
    /// </summary>
    /// <remarks>
    /// Used for file content comparison and integrity verification. Computed by <see cref="Sha256HashAlgorithm"/>.
    /// </remarks>
    public string Hash { get; set; }

    /// <summary>
    /// Gets or sets the left child node reference in the binary search tree (stores nodes with Id less than the current node).
    /// </summary>
    public FileNode Left { get; set; }

    /// <summary>
    /// Gets or sets the right child node reference in the binary search tree (stores nodes with Id greater than or equal to the current node).
    /// </summary>
    public FileNode Right { get; set; }

    /// <summary>
    /// Gets or sets a type marker for the node in the left tree (reserved field for classification in differential analysis).
    /// </summary>
    public int LeftType { get; set; }

    /// <summary>
    /// Gets or sets a type marker for the node in the right tree (reserved field for classification in differential analysis).
    /// </summary>
    public int RightType { get; set; }

    /// <summary>
    /// Gets or sets the relative path of the file with respect to the root directory.
    /// </summary>
    /// <example><c>bin/example.dll</c></example>
    /// <remarks>
    /// Uses URI relative path format (forward slash <c>/</c> as directory separator) to ensure cross-platform compatibility.
    /// This property is computed via <c>Uri.MakeRelativeUri</c> in <see cref="StorageManager.ReadFileNode"/>.
    /// </remarks>
    public string RelativePath { get; set; }

    #endregion Public Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="FileNode"/> class with default property values.
    /// </summary>
    public FileNode()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileNode"/> class with the specified node ID.
    /// </summary>
    /// <param name="id">The unique identifier for the file node.</param>
    public FileNode(int id)
    {
        Id = id;
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Adds a new node to the binary search tree rooted at the current node.
    /// </summary>
    /// <param name="node">The <see cref="FileNode"/> instance to add.</param>
    /// <remarks>
    /// <para>
    /// Insertion rules:
    /// <list type="bullet">
    ///   <item><description>If the new node's <c>Id</c> is less than the current node's <c>Id</c>, recursively insert into the left subtree.</description></item>
    ///   <item><description>If the new node's <c>Id</c> is greater than or equal to the current node's <c>Id</c>, recursively insert into the right subtree.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// If <paramref name="node"/> is <c>null</c>, no operation is performed.
    /// </para>
    /// </remarks>
    public void Add(FileNode node)
    {
        if (node == null) return;

        if (node.Id < Id)
        {
            if (Left == null)
            {
                Left = node;
            }
            else
            {
                Left.Add(node);
            }
        }
        else
        {
            if (Right == null)
            {
                Right = node;
            }
            else
            {
                Right.Add(node);
            }
        }
    }

    /// <summary>
    /// Searches for a node with the specified ID in the binary search tree rooted at the current node.
    /// </summary>
    /// <param name="id">The node ID to search for.</param>
    /// <returns>
    /// The matching <see cref="FileNode"/> instance if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// Uses the binary search tree property for binary search with an average time complexity of O(log n):
    /// <list type="bullet">
    ///   <item><description>If <paramref name="id"/> equals the current node's ID, returns the current node.</description></item>
    ///   <item><description>If <paramref name="id"/> is less than the current node's ID, recursively searches the left subtree.</description></item>
    ///   <item><description>If <paramref name="id"/> is greater than the current node's ID, recursively searches the right subtree.</description></item>
    /// </list>
    /// </remarks>
    public FileNode Search(long id)
    {
        if (id == Id)
        {
            return this;
        }
        else if (id < Id)
        {
            if (Left == null) return null;
            return Left.Search(id);
        }
        else
        {
            if (Right == null) return null;
            return Right.Search(id);
        }
    }

    /// <summary>
    /// Finds the parent node of a node with the specified ID in the binary search tree (used for deletion operations).
    /// </summary>
    /// <param name="id">The ID of the target node.</param>
    /// <returns>
    /// The parent <see cref="FileNode"/> of the target node if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// This method is used in <see cref="FileTree.DelNode"/> to locate the parent of the node to be deleted,
    /// so that the parent's left/right child reference can be updated. The determination logic:
    /// <list type="bullet">
    ///   <item><description>If the current node's left or right child has an ID equal to the target ID, the current node is the parent.</description></item>
    ///   <item><description>Otherwise, recursively searches the left or right subtree based on ID comparison.</description></item>
    /// </list>
    /// </remarks>
    public FileNode SearchParent(long id)
    {
        if (Left != null && Left.Id == id || Right != null && Right.Id == id)
        {
            return this;
        }
        else
        {
            if (id < Id && Left != null)
            {
                return Left.SearchParent(id);
            }
            else if (id >= Id && Right != null)
            {
                return Right.SearchParent(id);
            }
            else
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Compare tree nodes equally by Hash and file names.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object obj)
    {
        if (obj == null) return false;
        var tempNode = obj as FileNode;
        if (tempNode == null) throw new ArgumentException(nameof(tempNode));
        return string.Equals(Hash, tempNode.Hash, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Name, tempNode.Name, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode() => (Name != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Name) : 0) ^ (Hash != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Hash) : 0);

    #endregion Public Methods
}
