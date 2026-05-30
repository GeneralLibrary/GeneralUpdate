using GeneralUpdate.Core.Configuration;

namespace CoreTest.Configuration;

/// <summary>
/// AAAT unit tests for <see cref="VersionRespDTO"/> and <see cref="BaseResponseDTO{TBody}"/>.
/// Covers: default construction, property set/get, generic type resolution, null body.
/// </summary>
public class VersionRespDTOTests
{
    [Fact]
    public void Ctor_Default_CodeIsZero()
    {
        var resp = new VersionRespDTO();
        Assert.Equal(0, resp.Code);
    }

    [Fact]
    public void Ctor_Default_BodyIsNull()
    {
        var resp = new VersionRespDTO();
        Assert.Null(resp.Body);
    }

    [Fact]
    public void Ctor_Default_MessageIsNull()
    {
        var resp = new VersionRespDTO();
        Assert.Null(resp.Message);
    }

    [Fact]
    public void FullAssignment_AllPropertiesSet()
    {
        var versions = new List<VersionEntry>
        {
            new() { Version = "2.0.0", Hash = "abc", Name = "update.zip" }
        };

        var resp = new VersionRespDTO
        {
            Code = 200,
            Body = versions,
            Message = "success"
        };

        Assert.Equal(200, resp.Code);
        Assert.Same(versions, resp.Body);
        Assert.Equal("success", resp.Message);
        Assert.Single(resp.Body);
        Assert.Equal("2.0.0", resp.Body[0].Version);
    }

    [Fact]
    public void BaseResponseDTO_WithStringBody_Works()
    {
        var resp = new BaseResponseDTO<string>
        {
            Code = 404,
            Body = "not found",
            Message = "error"
        };

        Assert.Equal(404, resp.Code);
        Assert.Equal("not found", resp.Body);
        Assert.Equal("error", resp.Message);
    }

    [Fact]
    public void BaseResponseDTO_WithIntBody_Works()
    {
        var resp = new BaseResponseDTO<int>
        {
            Code = 200,
            Body = 42,
            Message = "ok"
        };

        Assert.Equal(200, resp.Code);
        Assert.Equal(42, resp.Body);
        Assert.Equal("ok", resp.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(200)]
    [InlineData(400)]
    [InlineData(500)]
    public void Code_SetVariousValues_Works(int code)
    {
        var resp = new VersionRespDTO { Code = code };
        Assert.Equal(code, resp.Code);
    }

    [Fact]
    public void Code_NegativeValue_Works()
    {
        var resp = new VersionRespDTO { Code = -1 };
        Assert.Equal(-1, resp.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ok")]
    [InlineData("error message with special chars: !@#$%^&*()")]
    public void Message_SetVariousValues_Works(string message)
    {
        var resp = new VersionRespDTO { Message = message };
        Assert.Equal(message, resp.Message);
    }

    [Fact]
    public void Body_EmptyList_Works()
    {
        var resp = new VersionRespDTO
        {
            Code = 200,
            Body = new List<VersionEntry>(),
            Message = "no updates"
        };

        Assert.NotNull(resp.Body);
        Assert.Empty(resp.Body);
    }

    [Fact]
    public void Body_WithMultipleVersions_Works()
    {
        var versions = Enumerable.Range(1, 5).Select(i =>
            new VersionEntry { Version = $"{i}.0.0", Hash = $"hash{i}" }).ToList();

        var resp = new VersionRespDTO { Body = versions };

        Assert.Equal(5, resp.Body.Count);
        Assert.Equal("3.0.0", resp.Body[2].Version);
    }
}
