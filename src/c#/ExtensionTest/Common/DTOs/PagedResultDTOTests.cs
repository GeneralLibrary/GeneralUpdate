/// <summary>
/// 测试覆盖点：
/// - 默认值：PageNumber=0, PageSize=0, TotalCount=0, TotalPages=0
/// - Items 默认值 Enumerable.Empty&lt;T&gt;()
/// - HasPrevious: PageNumber > 1 为 true，PageNumber <= 1 为 false
/// - HasNext: PageNumber < TotalPages 为 true，PageNumber >= TotalPages 为 false
/// - 边界条件：PageNumber=1/TotalPages=1 (无前页无后页)
/// - 边界条件：PageNumber=1/TotalPages=0 (无前页无后页)
/// - 边界条件：PageNumber=2/TotalPages=5 (有前页有后页)
/// - 泛型类型为 string/ExtensionDTO
/// </summary>
namespace GeneralUpdate.Extension.Common.DTOs.Tests;

public class PagedResultDTOTests
{
    [Fact]
    public void 默认构造_PageNumber和PageSize_为0()
    {
        var dto = new PagedResultDTO<string>();
        Assert.Equal(0, dto.PageNumber);
        Assert.Equal(0, dto.PageSize);
        Assert.Equal(0, dto.TotalCount);
        Assert.Equal(0, dto.TotalPages);
    }

    [Fact]
    public void 默认构造_Items_不为null且为空()
    {
        var dto = new PagedResultDTO<string>();
        Assert.NotNull(dto.Items);
        Assert.Empty(dto.Items);
    }

    [Fact]
    public void HasPrevious_PageNumber为1_返回false()
    {
        var dto = new PagedResultDTO<int> { PageNumber = 1, TotalPages = 10 };
        Assert.False(dto.HasPrevious);
    }

    [Fact]
    public void HasPrevious_PageNumber为2_返回true()
    {
        var dto = new PagedResultDTO<int> { PageNumber = 2, TotalPages = 10 };
        Assert.True(dto.HasPrevious);
    }

    [Fact]
    public void HasPrevious_PageNumber为0_返回false()
    {
        var dto = new PagedResultDTO<int> { PageNumber = 0, TotalPages = 10 };
        Assert.False(dto.HasPrevious);
    }

    [Fact]
    public void HasPrevious_PageNumber大于TotalPages_返回true()
    {
        var dto = new PagedResultDTO<int> { PageNumber = 5, TotalPages = 3 };
        Assert.True(dto.HasPrevious);
    }

    [Fact]
    public void HasNext_PageNumber小于TotalPages_返回true()
    {
        var dto = new PagedResultDTO<int> { PageNumber = 1, TotalPages = 10 };
        Assert.True(dto.HasNext);
    }

    [Fact]
    public void HasNext_PageNumber等于TotalPages_返回false()
    {
        var dto = new PagedResultDTO<int> { PageNumber = 10, TotalPages = 10 };
        Assert.False(dto.HasNext);
    }

    [Fact]
    public void HasNext_PageNumber大于TotalPages_返回false()
    {
        var dto = new PagedResultDTO<int> { PageNumber = 11, TotalPages = 10 };
        Assert.False(dto.HasNext);
    }

    [Fact]
    public void HasNext_TotalPages为0_返回false()
    {
        var dto = new PagedResultDTO<int> { PageNumber = 1, TotalPages = 0 };
        Assert.False(dto.HasNext);
    }

    [Fact]
    public void 单页场景_既无前页也无后页()
    {
        var dto = new PagedResultDTO<string>
        {
            PageNumber = 1,
            PageSize = 50,
            TotalCount = 30,
            TotalPages = 1
        };
        Assert.False(dto.HasPrevious);
        Assert.False(dto.HasNext);
    }

    [Fact]
    public void 中间页_有前页有后页()
    {
        var dto = new PagedResultDTO<string>
        {
            PageNumber = 3,
            PageSize = 10,
            TotalCount = 100,
            TotalPages = 10
        };
        Assert.True(dto.HasPrevious);
        Assert.True(dto.HasNext);
    }

    [Fact]
    public void Items可赋值为具体集合()
    {
        var items = new List<string> { "a", "b", "c" };
        var dto = new PagedResultDTO<string>
        {
            Items = items,
            TotalCount = 3,
            PageNumber = 1,
            PageSize = 10,
            TotalPages = 1
        };
        Assert.Equal(3, dto.Items.Count());
        Assert.Contains("a", dto.Items);
    }

    [Fact]
    public void 泛型为ExtensionDTO()
    {
        var items = new List<ExtensionDTO>
        {
            new() { Id = "e1", Name = "ext1" },
            new() { Id = "e2", Name = "ext2" }
        };
        var dto = new PagedResultDTO<ExtensionDTO>
        {
            Items = items,
            TotalCount = 2,
            PageNumber = 1,
            PageSize = 10,
            TotalPages = 1
        };
        Assert.Equal(2, dto.Items.Count());
        Assert.Equal("e1", dto.Items.First().Id);
    }

    [Fact]
    public void TotalCount为0_TotalPages也为0()
    {
        var dto = new PagedResultDTO<double>
        {
            TotalCount = 0,
            PageSize = 10,
            PageNumber = 1
        };
        Assert.Equal(0, dto.TotalCount);
        Assert.Equal(0, dto.TotalPages);
    }
}
