using GeneralUpdate.Core.HashAlgorithms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeneralUpdate.Core.ContentProvider
{
    public enum FileOperations
    {
        Query,
        Delete,
        Update,
        Add,
        Copy
    }

    public enum SetOperations
    {
        Intersection,
        Union,
        Difference
    }

    public partial class FileProvider 
    {
        /// <summary>
        /// 根据参数内容筛选出sourceDir、targetDir两个文件路径中符合要求的文件信息
        /// </summary>
        /// <param name="sourceDir">源目录</param>
        /// <param name="targetDir">目标目录</param>
        /// <param name="resultDir">筛选结果保存目录</param>
        /// <param name="condition">筛选条件，可以是文件名，可以是文件后缀名</param>
        /// <param name="fileOption">文件可执行的操作：增、删、查、改、拷贝</param>
        /// <param name="setOperations">集合运算：交集、并集、差集</param>
        /// <param name="recursion">整个方法里的操作是否递归执行</param>
        /// <param name="integrality">是否保持目录结构的完整性</param>
        /// <returns></returns>
        public List<FileNode> Handle(string sourceDir, string targetDir, string resultDir, List<string> condition, FileOperations fileOption, SetOperations setOperations, bool recursion, bool integrality)
        {
            IEnumerable<FileNode> sourceFiles = Directory.EnumerateFiles(sourceDir, "*.*", recursion ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .Select(fileInfo => new FileNode
                {
                    Id = fileInfo.GetHashCode(),
                    Name = fileInfo.Name,
                    FullName = fileInfo.FullName,
                    Path = fileInfo.DirectoryName,
                    Hash = CalculateFileHash(fileInfo)
                });
            IEnumerable<FileNode> targetFiles = Directory.EnumerateFiles(targetDir, "*.*", recursion ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .Select(fileInfo => new FileNode
                {
                    Id = fileInfo.GetHashCode(),
                    Name = fileInfo.Name,
                    FullName = fileInfo.FullName,
                    Path = fileInfo.DirectoryName,
                    Hash = CalculateFileHash(fileInfo)
                });

            // 然后根据条件筛选满足要求的文件
            IEnumerable<FileNode> filteredSourceFiles = sourceFiles.Where(file => condition.Any(c => file.Name.Contains(c) || file.Name.EndsWith(c)));
            IEnumerable<FileNode> filteredTargetFiles = targetFiles.Where(file => condition.Any(c => file.Name.Contains(c) || file.Name.EndsWith(c)));

            IEnumerable<FileNode> resultFiles;
            switch (setOperations)
            {
                case SetOperations.Intersection:
                    resultFiles = filteredSourceFiles.Intersect(filteredTargetFiles);
                    break;
                case SetOperations.Union:
                    resultFiles = filteredSourceFiles.Union(filteredTargetFiles);
                    break;
                case SetOperations.Difference:
                    resultFiles = filteredSourceFiles.Except(filteredTargetFiles);
                    break;
                default:
                    throw new ArgumentException("Invalid operation.", nameof(setOperations));
            }

            foreach (var file in resultFiles)
            {
                ExecuteFileOperation(file, fileOption, resultDir, integrality);
            }

            return resultFiles.ToList();
        }

        private string CalculateFileHash(FileInfo file)
        {
            var hashAlgorithm = new Sha256HashAlgorithm();
            return hashAlgorithm.ComputeHash(file.FullName);
        }

        /// <summary>
        /// 执行文件操作.
        /// </summary>
        /// <param name="file">文件对象.</param>
        /// <param name="operation">要执行的操作.</param>
        /// <param name="resultDir">结果目录.</param>
        /// <param name="retainStructure">是否保留结构.</param>
        private void ExecuteFileOperation(FileNode file, FileOperations operation, string resultDir, bool retainStructure)
        {
            string destinationPath = Path.Combine(resultDir, retainStructure ? file.RelativePath : file.Name);

            switch (operation)
            {
                case FileOperations.Add:
                    // 如果文件已存在，此操作将覆盖现有文件
                    File.Copy(file.Path, destinationPath, true);
                    break;
                case FileOperations.Delete:
                    if (File.Exists(destinationPath))
                    {
                        File.Delete(destinationPath);
                    }
                    break;
                case FileOperations.Update:
                    // 在这里，我们假定更新操作意味着复制源文件到目标位置，覆盖现有文件
                    File.Copy(file.Path, destinationPath, true);
                    break;
                case FileOperations.Copy:
                    // 确保目标目录存在
                    string directoryName = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(directoryName))
                    {
                        Directory.CreateDirectory(directoryName);
                    }
                    // 拷贝文件到新的位置
                    File.Copy(file.Path, destinationPath, true);
                    break;
                case FileOperations.Query:
                    // 对于“查询”操作，我们不执行任何文件操作，只在控制台中打印出相关信息
                    Console.WriteLine($"Found file: {file.FullName}");
                    break;
                default:
                    throw new ArgumentException("Invalid operation", nameof(operation));
            }
        }

    }
}
