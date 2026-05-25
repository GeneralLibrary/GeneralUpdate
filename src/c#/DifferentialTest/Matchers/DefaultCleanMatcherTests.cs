using GeneralUpdate.Differential.Matchers;

namespace DifferentialTest.Matchers
{
    /// <summary>
    /// 分支覆盖点：
    ///   1. Match — oldFile匹配成功 (名称+相对路径匹配, 且文件存在)
    ///   2. Match — oldFile名称不匹配 → null
    ///   3. Match — oldFile相对路径不匹配 → null
    ///   4. Match — oldFile匹配但文件不存在(File.Exists=false) → null
    ///   5. Match — newFile匹配但文件不存在(File.Exists=false) → null
    ///   6. Match — leftNodes为空 → null
    ///   7. Match — leftNodes有多个匹配 → 取FirstOrDefault
    ///   8. Match — 大小写不敏感匹配
    ///
    /// 触发条件：各种 FileNode 组合
    /// 预期结果：正确匹配或返回null
    /// </summary>
    public class DefaultCleanMatcherTests : IDisposable
    {
        private readonly string _testDir;

        public DefaultCleanMatcherTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"CleanMatcherTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }

        [Fact(DisplayName = "Match_名称和相对路径均匹配且文件存在_返回oldFile")]
        public void Match_NameAndPathMatchFileExists_ReturnsOldFile()
        {
            var matcher = new DefaultCleanMatcher();

            var oldFile = CreateFileNode("app.dll", "sub");
            var newFile = CreateFileNode("app.dll", "sub");
            var leftNodes = new List<GeneralUpdate.Core.FileSystem.FileNode> { oldFile };

            var result = matcher.Match(newFile, leftNodes);

            Assert.NotNull(result);
            Assert.Equal(oldFile.FullName, result.FullName);
        }

        [Fact(DisplayName = "Match_名称匹配但相对路径不匹配_返回null")]
        public void Match_NameMatchButPathMismatch_ReturnsNull()
        {
            var matcher = new DefaultCleanMatcher();

            var oldFile = CreateFileNode("app.dll", "sub1");
            var newFile = CreateFileNode("app.dll", "sub2");
            var leftNodes = new List<GeneralUpdate.Core.FileSystem.FileNode> { oldFile };

            var result = matcher.Match(newFile, leftNodes);

            Assert.Null(result);
        }

        [Fact(DisplayName = "Match_相对路径匹配但名称不匹配_返回null")]
        public void Match_PathMatchButNameMismatch_ReturnsNull()
        {
            var matcher = new DefaultCleanMatcher();

            var oldFile = CreateFileNode("old.dll", "sub");
            var newFile = CreateFileNode("new.dll", "sub");
            var leftNodes = new List<GeneralUpdate.Core.FileSystem.FileNode> { oldFile };

            var result = matcher.Match(newFile, leftNodes);

            Assert.Null(result);
        }

        [Fact(DisplayName = "Match_匹配但oldFile文件不存在_返回null")]
        public void Match_FoundButOldFileNotExist_ReturnsNull()
        {
            var matcher = new DefaultCleanMatcher();
            var nonExistent = Path.Combine(_testDir, "nonexistent.dll");

            var oldFile = new GeneralUpdate.Core.FileSystem.FileNode
            {
                Name = "app.dll",
                RelativePath = "sub",
                FullName = nonExistent
            };
            var newFile = CreateFileNode("app.dll", "sub");
            var leftNodes = new List<GeneralUpdate.Core.FileSystem.FileNode> { oldFile };

            var result = matcher.Match(newFile, leftNodes);

            Assert.Null(result);
        }

        [Fact(DisplayName = "Match_匹配但newFile文件不存在_返回null")]
        public void Match_FoundButNewFileNotExist_ReturnsNull()
        {
            var matcher = new DefaultCleanMatcher();
            var nonExistent = Path.Combine(_testDir, "nonexistent2.dll");

            var oldFile = CreateFileNode("app.dll", "sub");
            var newFile = new GeneralUpdate.Core.FileSystem.FileNode
            {
                Name = "app.dll",
                RelativePath = "sub",
                FullName = nonExistent
            };
            var leftNodes = new List<GeneralUpdate.Core.FileSystem.FileNode> { oldFile };

            var result = matcher.Match(newFile, leftNodes);

            Assert.Null(result);
        }

        [Fact(DisplayName = "Match_leftNodes为空_返回null")]
        public void Match_EmptyLeftNodes_ReturnsNull()
        {
            var matcher = new DefaultCleanMatcher();
            var newFile = CreateFileNode("app.dll", "sub");
            var leftNodes = Enumerable.Empty<GeneralUpdate.Core.FileSystem.FileNode>();

            var result = matcher.Match(newFile, leftNodes);

            Assert.Null(result);
        }

        [Fact(DisplayName = "Match_名称大小写不同_仍然匹配(OrdinalIgnoreCase)")]
        public void Match_DifferentCaseName_StillMatches()
        {
            var matcher = new DefaultCleanMatcher();

            var oldFile = CreateFileNode("APP.DLL", "sub");
            var newFile = CreateFileNode("app.dll", "sub");
            var leftNodes = new List<GeneralUpdate.Core.FileSystem.FileNode> { oldFile };

            var result = matcher.Match(newFile, leftNodes);

            Assert.NotNull(result);
        }

        [Fact(DisplayName = "Match_相对路径大小写不同_仍然匹配")]
        public void Match_DifferentCasePath_StillMatches()
        {
            var matcher = new DefaultCleanMatcher();

            var oldFile = CreateFileNode("app.dll", "SUB");
            var newFile = CreateFileNode("app.dll", "sub");
            var leftNodes = new List<GeneralUpdate.Core.FileSystem.FileNode> { oldFile };

            var result = matcher.Match(newFile, leftNodes);

            Assert.NotNull(result);
        }

        [Fact(DisplayName = "Match_leftNodes有多个条目_匹配第一个")]
        public void Match_MultipleLeftNodes_MatchesFirst()
        {
            var matcher = new DefaultCleanMatcher();

            var oldFile1 = CreateFileNode("app.dll", "sub");
            var oldFile2 = CreateFileNode("app_v2.dll", "sub");
            var newFile = CreateFileNode("app.dll", "sub");
            var leftNodes = new List<GeneralUpdate.Core.FileSystem.FileNode> { oldFile1, oldFile2 };

            var result = matcher.Match(newFile, leftNodes);

            Assert.NotNull(result);
            Assert.Equal(oldFile1.FullName, result.FullName);
        }

        /// <summary>
        /// 创建真实存在的测试文件及其FileNode
        /// </summary>
        private GeneralUpdate.Core.FileSystem.FileNode CreateFileNode(string name, string relativeDir)
        {
            var dir = Path.Combine(_testDir, relativeDir);
            Directory.CreateDirectory(dir);
            var filePath = Path.Combine(dir, name);
            File.WriteAllText(filePath, "test content");

            return new GeneralUpdate.Core.FileSystem.FileNode
            {
                Name = name,
                RelativePath = relativeDir,
                FullName = filePath
            };
        }
    }
}
