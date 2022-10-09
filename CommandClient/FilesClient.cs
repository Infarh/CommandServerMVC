using System.Net.Http.Json;

namespace CommandClient;

public class FilesClient
{
    public HttpClient Http { get; }

    public FilesClient(HttpClient Http) => this.Http = Http;

    public async Task<ServerFileInfo> UploadAsync(string FilePath, CancellationToken Cancel = default)
    {
        var file_info = new FileInfo(FilePath);

        using var content = new MultipartFormDataContent();

        await using var file_stream    = File.OpenRead(FilePath);
        var             stream_content = new StreamContent(file_stream);
        content.Add(stream_content, "file", file_info.Name);

        var response = await Http.PostAsync("api/file", content, Cancel);
        var result = await response
               .EnsureSuccessStatusCode()
               .Content
               .ReadFromJsonAsync<ServerFileInfo>(cancellationToken: Cancel)
                ?? throw new InvalidOperationException("Не получен ответ от сервера");

        return result;
    }
}

public class ServerFileInfo
{
    public string FileName { get; init; } = null!
    public long Length { get; init; }
    public string MD5 { get; init; } = null!;
}
