/// <summary>
/// 测试覆盖点：
/// - DownloadErrorType 枚举值验证 None=0, NetworkError=1, ClientError=2, ServerError=3, HashMismatch=4, IoError=5, Cancelled=6, Unknown=99
/// - DownloadResult.Ok() 返回 Success=true, ErrorType=None
/// - DownloadResult.Fail() 各类错误类型
/// </summary>
namespace GeneralUpdate.Extension.Common.Models.Tests;

public class DownloadResultTests
{
    [Fact]
    public void DownloadErrorType_None_值为0() => Assert.Equal(0, (int)DownloadErrorType.None);
    [Fact]
    public void NetworkError_值为1() => Assert.Equal(1, (int)DownloadErrorType.NetworkError);
    [Fact]
    public void ClientError_值为2() => Assert.Equal(2, (int)DownloadErrorType.ClientError);
    [Fact]
    public void ServerError_值为3() => Assert.Equal(3, (int)DownloadErrorType.ServerError);
    [Fact]
    public void HashMismatch_值为4() => Assert.Equal(4, (int)DownloadErrorType.HashMismatch);
    [Fact]
    public void IoError_值为5() => Assert.Equal(5, (int)DownloadErrorType.IoError);
    [Fact]
    public void Cancelled_值为6() => Assert.Equal(6, (int)DownloadErrorType.Cancelled);
    [Fact]
    public void Unknown_值为99() => Assert.Equal(99, (int)DownloadErrorType.Unknown);

    [Fact]
    public void Ok_返回Success为true_ErrorType为None()
    {
        var result = DownloadResult.Ok();
        Assert.True(result.Success);
        Assert.Equal(DownloadErrorType.None, result.ErrorType);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.HttpStatusCode);
    }

    [Theory]
    [InlineData(DownloadErrorType.NetworkError, "Connection refused", null)]
    [InlineData(DownloadErrorType.ClientError, "Not Found", 404)]
    [InlineData(DownloadErrorType.ServerError, "Internal Error", 500)]
    [InlineData(DownloadErrorType.Cancelled, "Download was cancelled.", null)]
    [InlineData(DownloadErrorType.IoError, "Disk full", null)]
    [InlineData(DownloadErrorType.Unknown, "Something went wrong", null)]
    [InlineData(DownloadErrorType.HashMismatch, "Hash verification failed", null)]
    public void Fail_各类错误类型返回正确结构(DownloadErrorType errorType, string message, int? httpStatus)
    {
        var result = DownloadResult.Fail(errorType, message, httpStatus);
        Assert.False(result.Success);
        Assert.Equal(errorType, result.ErrorType);
        Assert.Equal(message, result.ErrorMessage);
        Assert.Equal(httpStatus, result.HttpStatusCode);
    }

    [Fact]
    public void Fail_ErrorMessage为空字符串()
    {
        var result = DownloadResult.Fail(DownloadErrorType.Unknown, "");
        Assert.Empty(result.ErrorMessage);
    }

    [Fact]
    public void Fail_ErrorMessage为null()
    {
        var result = DownloadResult.Fail(DownloadErrorType.Unknown, null!);
        Assert.Null(result.ErrorMessage);
    }
}
