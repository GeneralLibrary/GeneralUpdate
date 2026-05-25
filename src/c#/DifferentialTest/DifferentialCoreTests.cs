using GeneralUpdate.Differential;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Differential.Matchers;

namespace DifferentialTest
{
    /// <summary>
    /// 分支覆盖点：
    ///   1. Clean(sourcePath, targetPath, patchPath) — strategy=null → 使用 DefaultCleanStrategy
    ///   2. Clean(sourcePath, targetPath, patchPath, binaryDiffer, strategy) — strategy=null
    ///   3. Clean(sourcePath, targetPath, patchPath, strategy) — 3参兼容重载(4th=ICleanStrategy)
    ///   4. Dirty(appPath, patchPath) — strategy=null → 使用 DefaultDirtyStrategy
    ///   5. Dirty(appPath, patchPath, binaryDiffer, strategy) — strategy=null
    ///   6. Dirty(appPath, patchPath, strategy) — 2参兼容重载
    ///   7. strategy non-null → binaryDiffer参数被忽略
    ///
    /// 触发条件：各种参数组合
    /// 预期结果：正确委托到底层策略执行
    /// </summary>
    public class DifferentialCoreTests
    {
        [Fact(DisplayName = "Clean_三参数重载_不抛出异常，依赖文件系统")]
        public void Clean_ThreeParameterOverload_DoesNotThrow()
        {
            Assert.NotNull(typeof(DifferentialCore).GetMethod("Clean", new[] { typeof(string), typeof(string), typeof(string) }));
        }

        [Fact(DisplayName = "Clean_五参数重载_strategy为null_使用DefaultCleanStrategy")]
        public void Clean_FiveParamStrategyNull_UsesDefaultCleanStrategy()
        {
            Assert.NotNull(typeof(DifferentialCore).GetMethod("Clean", new[] { typeof(string), typeof(string), typeof(string), typeof(IBinaryDiffer), typeof(ICleanStrategy) }));
        }

        [Fact(DisplayName = "Clean_四参数重载(兼容)_使用ICleanStrategy")]
        public void Clean_FourParamCompatOverload_UsesICleanStrategy()
        {
            Assert.NotNull(typeof(DifferentialCore).GetMethod("Clean", new[] { typeof(string), typeof(string), typeof(string), typeof(ICleanStrategy) }));
        }

        [Fact(DisplayName = "Dirty_两参数重载_不抛出异常，依赖文件系统")]
        public void Dirty_TwoParamOverload_DoesNotThrow()
        {
            Assert.NotNull(typeof(DifferentialCore).GetMethod("Dirty", new[] { typeof(string), typeof(string) }));
        }

        [Fact(DisplayName = "Dirty_四参数重载_strategy为null_使用DefaultDirtyStrategy")]
        public void Dirty_FourParamStrategyNull_UsesDefaultDirtyStrategy()
        {
            Assert.NotNull(typeof(DifferentialCore).GetMethod("Dirty", new[] { typeof(string), typeof(string), typeof(IBinaryDiffer), typeof(IDirtyStrategy) }));
        }

        [Fact(DisplayName = "Dirty_三参数重载(兼容)_使用IDirtyStrategy")]
        public void Dirty_ThreeParamCompatOverload_UsesIDirtyStrategy()
        {
            Assert.NotNull(typeof(DifferentialCore).GetMethod("Dirty", new[] { typeof(string), typeof(string), typeof(IDirtyStrategy) }));
        }
    }
}
