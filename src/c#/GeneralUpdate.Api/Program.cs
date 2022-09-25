using GeneralUpdate.AspNetCore.Services;
using GeneralUpdate.Core.Domain.DTO;
using GeneralUpdate.Core.Domain.Enum;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<IUpdateService, GeneralUpdateService>();
var app = builder.Build();
app.MapGet("/versions/{clientType}/{clientVersion}/{clientAppKey}", (int clientType, string clientVersion, string clientAppKey, IUpdateService updateService) =>
{
    var versions = new List<VersionDTO>();
    var md5 = "dd776e3a4f2028a5f61187e23089ddbd";
    var pubTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
    string version = null;
    if (clientType == AppType.ClientApp)
    {
        //client
        //version = "0.0.0.0";
        version = "9.9.9.9";
    }
    else if (clientType == AppType.UpgradeApp)
    {
        //upgrad
        //version = "0.0.0.0";
        version = "9.9.9.9";
    }
    var url = $"http://127.0.0.1/1664083126.zip";
    var name = "1664081315";
    versions.Add(new VersionDTO(md5, pubTime, version, url, name));
    return updateService.Update(clientType, clientVersion, version, clientAppKey, GetAppSecretKey(), false, versions);
});

app.MapPost("/upload", async Task<IResult> (int clientType,  string version,  string clientAppKey, HttpRequest request) =>
{
    try
    {
        if (!request.HasFormContentType) return Results.BadRequest();
        var form = await request.ReadFormAsync();
        var formFile = form.Files["file"];
        if (formFile is null || formFile.Length == 0) return Results.BadRequest();
        await using var stream = formFile.OpenReadStream();
        byte[] buffer = new byte[stream.Length];
        stream.Read(buffer, 0, buffer.Length);
        string localPath = $"E:\\{formFile.FileName}";
        await using var fileStream = new FileStream(localPath, FileMode.CreateNew, FileAccess.Write);
        fileStream.Write(buffer, 0, buffer.Length);
        return Results.Ok("ok");
    }
    catch (Exception)
    {
        return Results.BadRequest();
    }
});

app.Run();

string GetAppSecretKey()
{
    return "B8A7FADD-386C-46B0-B283-C9F963420C7C";
}