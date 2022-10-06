var builder = WebApplication.CreateBuilder(args);
//builder.Services.AddSingleton<IUpdateService, GeneralUpdateService>();
builder.Services.AddSignalR();
var app = builder.Build();

//app.MapHub<VersionHub>("/versionhub");

//app.Use(async (context, next) =>
//{
//    var hubContext = context.RequestServices.GetRequiredService<IHubContext<VersionHub>>();
//    await CommonHubContextMethod((IHubContext)hubContext);
//    if (next != null)
//    {
//        await next.Invoke();
//    }
//});

//async Task CommonHubContextMethod(IHubContext context)
//{
//    await context.Clients.All.SendAsync("clientMethod","");
//}

//app.MapGet("/versions/{clientType}/{clientVersion}/{clientAppKey}", async (int clientType, string clientVersion, string clientAppKey, IUpdateService updateService) =>
//{
//    //TODO: Link database query appSecretKey.
//    var appSecretKey = "41A54379-C7D6-4920-8768-21A3468572E5";
//    //return await updateService.UpdateVersionsTaskAsync(clientType, clientVersion, clientAppKey, appSecretKey, UpdateVersions);
//});

//app.MapGet("/validate/{clientType}/{clientVersion}/{clientAppKey}", async (int clientType, string clientVersion, string clientAppKey, IUpdateService updateService) =>
//{
//    //TODO: Link database query appSecretKey.
//    //var appSecretKey = "41A54379-C7D6-4920-8768-21A3468572E5";
//    //if (!appSecretKey.Equals(clientAppKey)) throw new Exception($"key {clientAppKey} is not found in the database, check whether you need to upload the new version information!");
//    //return await updateService.UpdateValidateTaskAsync(clientType, clientVersion, GetLastVersion(), clientAppKey, appSecretKey, true, GetValidateInfos);
//});

string GetLastVersion()
{
    //TODO:Link database query information.
    return "9.1.3.0";
}
