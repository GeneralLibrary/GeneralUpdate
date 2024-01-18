using GeneralUpdate.AspNetCore.DTO;
using GeneralUpdate.AspNetCore.Services;
using GeneralUpdate.Core.Domain.DTO;
using GeneralUpdate.Core.Domain.Enum;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IUpdateService, GeneralUpdateService>();
builder.Services.AddSignalR();
var app = builder.Build();

/**
 * Push the latest version information in real time.
 */
//app.MapHub<VersionHub>("/versionhub");

//app.MapPost("/push", async Task<string> (HttpContext context) =>
//{
//    try
//    {
//        var hubContext = context.RequestServices.GetRequiredService<IHubContext<VersionHub>>();
//        await hubContext.SendMessage("TESTNAME", "123");
//    }
//    catch (Exception ex)
//    {
//        return ex.Message;
//    }
//    return "OK";
//});

/**
 * Check if an update is required.
 */
app.MapGet("/versions/{clientType}/{clientVersion}/{clientAppKey}", (int clientType, string clientVersion, string clientAppKey, IUpdateService updateService) =>
{
    var versions = new List<VersionDTO>();
    var hash = "28d10f1fc2a23dd1afe0af40d132b25c72ea56005963f653c27889f03d381c8d";//���ɺõĸ��°��ļ���MD5�룬��Ϊ���ظ��ͻ��˵�ʱ����Ҫͬ�������֤�Ƿ����
    var pubTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
    string version = null;
    if (clientType == AppType.ClientApp)
    {
        //client
        //version = "0.0.0.0";
        version = "9.9.9.9";//��������Ϊ9���ó�����Ϊ��Ҫ����
    }
    else if (clientType == AppType.UpgradeApp)
    {
        //upgrad
        version = "0.0.0.0";
        //version = "9.9.9.9"; //��������Ϊ9���ó�����Ϊ��Ҫ����
    }
    var url = $"http://192.168.1.7/WpfClient_1_24.1.5.1218.zip";//���°������ص�ַ
    var name = "update";
    versions.Add(new VersionDTO(hash, pubTime, version, url, name));
    return updateService.Update(clientType, clientVersion, version, clientAppKey, GetAppSecretKey(), false, versions);
});

/**
 * Upload update package.
 */
app.MapPost("/upload", async Task<string> (HttpContext context, HttpRequest request) =>
{
    var uploadReapDTO = new UploadReapDTO();
    try
    {
        var contextReq = context.Request;
        int.TryParse(contextReq.Form["clientType"], out int clientType);
        var version = contextReq.Form["clientType"].ToString();
        var clientAppKey = contextReq.Form["clientAppKey"].ToString();
        var hash = contextReq.Form["hash"].ToString();

        if (!request.HasFormContentType) throw new Exception("ContentType was not included in the request !");
        var form = await request.ReadFormAsync();

        var formFile = form.Files["file"];
        if (formFile is null || formFile.Length == 0) throw new ArgumentNullException("Uploaded update package file not found !");
        await using var stream = formFile.OpenReadStream();
        byte[] buffer = new byte[stream.Length];
        stream.Read(buffer, 0, buffer.Length);
        //TODO:save to file server.
        string localPath = $"E:\\{formFile.FileName}";
        await using var fileStream = new FileStream(localPath, FileMode.CreateNew, FileAccess.Write);
        fileStream.Write(buffer, 0, buffer.Length);

        //TODO: data persistence.To mysql , sqlserver....

        uploadReapDTO.Code = HttpStatus.OK;
        uploadReapDTO.Body = "Published successfully.";
        uploadReapDTO.Message = RespMessage.RequestSucceeded;
        return JsonConvert.SerializeObject(uploadReapDTO);
    }
    catch (Exception ex)
    {
        uploadReapDTO.Code = HttpStatus.BAD_REQUEST;
        uploadReapDTO.Body = $"Failed to publish ! Because : {ex.Message}";
        uploadReapDTO.Message = RespMessage.RequestFailed;
        return JsonConvert.SerializeObject(uploadReapDTO);
    }
});

app.Run();

string GetAppSecretKey()
{
    return "B8A7FADD-386C-46B0-B283-C9F963420C7C";
}