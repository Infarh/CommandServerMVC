using System.Net.Http.Json;

namespace CommandClient;

public class FilesClient
{
    public HttpClient Http { get; }

    public FilesClient(HttpClient Http) => this.Http = Http;

    public record ServerFileInfo(string FileName, long Length, string MD5);

    public async Task<ServerFileInfo> UploadAsync(string FilePath, CancellationToken Cancel = default)
    {
        var file_info = new FileInfo(FilePath);
        if (!file_info.Exists)
            throw new FileNotFoundException("Файл не найден", file_info.FullName);

        await using var file_stream = file_info.OpenRead();
        var stream_content = new StreamContent(file_stream);

        using var content = new MultipartFormDataContent
        {
            { stream_content, "file", file_info.Name }
        };

        var response = await Http.PostAsync("api/file", content, Cancel);
        var result = await response
               .EnsureSuccessStatusCode()
               .Content
               .ReadFromJsonAsync<ServerFileInfo>(cancellationToken: Cancel)
                ?? throw new InvalidOperationException("Не получен ответ от сервера");

        return result;
    }
}

