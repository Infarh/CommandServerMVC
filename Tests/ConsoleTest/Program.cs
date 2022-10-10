
using CommandClient;

using var http = new HttpClient
{
    BaseAddress = new("http://localhost:1423"),
};

var client = new FilesClient(http);

Console.WriteLine("Ожидание сервера");
Console.ReadLine();

await client.UploadAsync(@"c:\123\Diagram 2022-07-06 20-48-12.uxf");

Console.WriteLine("End!");
