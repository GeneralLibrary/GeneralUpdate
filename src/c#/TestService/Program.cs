using GeneralUpdate.AspNetCore.Services;
using GeneralUpdate.Core.Domain.DTO;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<IUpdateService, GeneralUpdateService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

app.MapGet("/versions/{clientType}/{clientVersion}/{clientAppKey}", (int clientType, string clientVersion, string clientAppKey, IUpdateService updateService) =>
{
    return  updateService.Update(clientType, clientVersion, GetLastVersion(), clientAppKey, GetAppSecretKey(), false, GerVersions());
});

List<VersionDTO> GerVersions() 
{
    var versions = new List<VersionDTO>();
    return versions;
}

string GetLastVersion()
{
    return "9.1.3.0";
}

string GetAppSecretKey() 
{
    return "41A54379-C7D6-4920-8768-21A3468572E5";
}