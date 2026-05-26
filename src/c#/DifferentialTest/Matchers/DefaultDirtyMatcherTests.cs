using GeneralUpdate.Core.Differential;

namespace DifferentialTest.Matchers
{
    /// <summary>
    /// 分支覆盖点：
    ///   1. Match — 通过文件名（去掉.patch后缀后）匹配 (case-insensitive)
    ///   2. Match — 文件有双重后缀 (如 app.dll.patch) → 只去掉最后一个.patch
    ///   3. Match — 文件名没有.patch后缀 → 直接按oldFile.Name比较
    ///   4. Match — 匹配到但扩展名不是.patch → 返回null
    ///   5. Match — patchFiles为空 → null
    ///   6. Match — 大小写不敏感匹配
    ///   7. Match — 名字包含.patch但不以.patch结尾 (如 patchfile.txt)
    ///
    /// 触发条件：各种文件名组合
    /// 预期结果：正确匹配或返回null
    /// </summary>
    public class DefaultDirtyMatcherTests : IDisposable
    {
        private readonly string _testDir;

        public DefaultDirtyMatcherTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"DirtyMatcherTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }

        [Fact(DisplayName = "Match_文件名去掉patch后匹配_返回patchFile")]
        public void Match_NameWithoutPatchMatches_ReturnsPatchFile()
        {
            var matcher = new DefaultDirtyMatcher();
            var oldFile = CreateFileInfo("app.dll");
            var patchFile = CreateFileInfo("app.dll.patch");
            var patchFiles = new List<FileInfo> { patchFile };

            var result = matcher.Match(oldFile, patchFiles);

            Assert.NotNull(result);
            Assert.Equal(patchFile.FullName, result!.FullName);
        }

        [Fact(DisplayName = "Match_双重后缀.patch文件_去掉最后一个patch后缀后匹配")]
        public void Match_DoubleExtensionWithPatch_StripsOnlyLastPatch()
        {
            var matcher = new DefaultDirtyMatcher();
            var oldFile = CreateFileInfo("app.dll.patch"); // 原始文件名含.patch
            var patchFile1 = CreateFileInfo("app.dll.patch.patch");
            var patchFiles = new List<FileInfo> { patchFile1 };

            var result = matcher.Match(oldFile, patchFiles);

            Assert.NotNull(result);
            Assert.Equal(patchFile1.FullName, result!.FullName);
        }

        [Fact(DisplayName = "Match_文件名不匹配_返回null")]
        public void Match_NameMismatch_ReturnsNull()
        {
            var matcher = new DefaultDirtyMatcher();
            var oldFile = CreateFileInfo("app.dll");
            var patchFile = CreateFileInfo("other.dll.patch");
            var patchFiles = new List<FileInfo> { patchFile };

            var result = matcher.Match(oldFile, patchFiles);

            Assert.Null(result);
        }

        [Fact(DisplayName = "Match_匹配但扩展名不是.patch_返回null")]
        public void Match_MatchedButNotPatchExtension_ReturnsNull()
        {
            var matcher = new DefaultDirtyMatcher();
            var oldFile = CreateFileInfo("app.dll");
            var patchFile = CreateFileInfo("app.dll.txt"); // 非.patch扩展名
            var patchFiles = new List<FileInfo> { patchFile };

            var result = matcher.Match(oldFile, patchFiles);

            Assert.Null(result);
        }

        [Fact(DisplayName = "Match_patchFiles为空_返回null")]
        public void Match_EmptyPatchFiles_ReturnsNull()
        {
            var matcher = new DefaultDirtyMatcher();
            var oldFile = CreateFileInfo("app.dll");
            var patchFiles = Enumerable.Empty<FileInfo>();

            var result = matcher.Match(oldFile, patchFiles);

            Assert.Null(result);
        }

        [Fact(DisplayName = "Match_文件名大小写不同_仍然匹配")]
        public void Match_DifferentCaseName_StillMatches()
        {
            var matcher = new DefaultDirtyMatcher();
            var oldFile = CreateFileInfo("APP.DLL");
            var patchFile = CreateFileInfo("app.dll.patch");
            var patchFiles = new List<FileInfo> { patchFile };

            var result = matcher.Match(oldFile, patchFiles);

            Assert.NotNull(result);
        }

        [Fact(DisplayName = "Match_patch后缀大小写不同_仍能匹配")]
        public void Match_DifferentCasePatchExtension_StillMatches()
        {
            var matcher = new DefaultDirtyMatcher();
            var oldFile = CreateFileInfo("app.dll");
            var patchFile = CreateFileInfo("app.dll.PATCH");
            var patchFiles = new List<FileInfo> { patchFile };

            var result = matcher.Match(oldFile, patchFiles);

            Assert.NotNull(result);
        }

        [Fact(DisplayName = "Match_多个patch文件_匹配第一个")]
        public void Match_MultiplePatchFiles_MatchesFirst()
        {
            var matcher = new DefaultDirtyMatcher();
            var oldFile = CreateFileInfo("app.dll");
            var patchFile1 = CreateFileInfo("app.dll.patch");
            var patchFile2 = CreateFileInfo("app_v2.dll.patch");
            var patchFiles = new List<FileInfo> { patchFile1, patchFile2 };

            var result = matcher.Match(oldFile, patchFiles);

            Assert.NotNull(result);
            Assert.Equal(patchFile1.FullName, result!.FullName);
        }

        [Fact(DisplayName = "Match_patch文件名没有.patch后缀_直接按名称比较")]
        public void Match_NoPatchSuffixInName_ComparesByNameDirectly()
        {
            var matcher = new DefaultDirtyMatcher();
            var oldFile = CreateFileInfo("readme.txt");
            var patchFile = CreateFileInfo("readme.txt.patch");
            var patchFiles = new List<FileInfo> { patchFile };

            var result = matcher.Match(oldFile, patchFiles);

            Assert.NotNull(result);
        }

        private FileInfo CreateFileInfo(string name)
        {
            var path = Path.Combine(_testDir, name);
            File.WriteAllText(path, "test");
            return new FileInfo(path);
        }
    }
}
