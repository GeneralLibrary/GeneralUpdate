using Xunit;
using GeneralUpdate.Extension.Common.DTOs;

namespace GeneralUpdate.Extension.Common.DTOs.Tests;

public class HttpResponseDTOTests
{
    [Fact]
    public void 默认构造_Message_Body_Code_为null()
    {
        var dto = new HttpResponseDTO<string>();
        Assert.Null(dto.Message);
        Assert.Null(dto.Body);
        Assert.Null(dto.Code);
    }

    [Fact]
    public void 设置Message_Body_Code_可正确读取()
    {
        var dto = new HttpResponseDTO<int> { Message = "Success", Body = 42, Code = "OK" };
        Assert.Equal("Success", dto.Message);
        Assert.Equal(42, dto.Body);
        Assert.Equal("OK", dto.Code);
    }

    [Fact]
    public void Body为null时仍可正确赋值()
    {
        var dto = new HttpResponseDTO<string> { Message = "Error", Code = "ERR_001" };
        Assert.Null(dto.Body);
        Assert.Equal("Error", dto.Message);
    }

    [Fact]
    public void 泛型为ExtensionDTO时可正常使用()
    {
        var dto = new HttpResponseDTO<ExtensionDTO> { Body = new ExtensionDTO { Id = "ext-1" } };
        Assert.NotNull(dto.Body);
        Assert.Equal("ext-1", dto.Body.Id);
    }
}
