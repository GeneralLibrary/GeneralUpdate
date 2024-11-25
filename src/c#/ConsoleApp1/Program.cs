// See https://aka.ms/new-console-template for more information

using Microsoft.AspNetCore.SignalR.Client;

HubConnection _connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5000/UpgradeHub")
    .Build();

_connection.On<string, string>("11", (user, message) =>
{
    Console.WriteLine($"{user}: {message}");
});
await _connection.StartAsync();

while (true)
{
    var content = Console.ReadLine();
    if (content == "exit") break;
}