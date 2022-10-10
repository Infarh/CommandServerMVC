using System.Buffers;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

using CommandServerMVC.Infrastructure.Extensions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace CommandServerMVC.Controllers.Api;

[ApiController, Route("api/file")]
public class FilesApiController : ControllerBase
{
    private readonly ILogger<FilesApiController> _Logger;
    private readonly string _FilesDirectoryPath;

    public FilesApiController(IConfiguration Configuration, ILogger<FilesApiController> Logger)
    {
        _Logger = Logger;
        _FilesDirectoryPath = Configuration.GetValue("FilesDir", "files");
    }

    private static async Task<T> DeserializeAsync<T>(string FilePath, T _, CancellationToken Cancel)
    {
        await using var file_stream = System.IO.File.OpenRead(FilePath);
        return await DeserializeAsync<T>(file_stream, Cancel);
    }

    private static async Task<T> DeserializeAsync<T>(Stream stream, CancellationToken Cancel) =>
        (await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: Cancel).ConfigureAwait(false))!;

    [HttpGet]
    public async Task<IActionResult> GetFiles(CancellationToken Cancel)
    {
        if (!Directory.Exists(_FilesDirectoryPath))
            return NoContent();

        var files_tasks = Directory.EnumerateFiles(_FilesDirectoryPath, "*.info.json")
           .Select(async file => await DeserializeAsync(file, new
           {
               FileName = "",
               Length = 0L,
               MD5 = ""
           }, Cancel));

        var files = await Task.WhenAll(files_tasks);

        return Ok(files);
    }

    [HttpGet("info/{file}")]
    public async Task<IActionResult> GetFileInfo(string file, CancellationToken Cancel)
    {
        var files_dir = _FilesDirectoryPath;
        var file_path = Path.Combine(files_dir, file);
        var json_path = file_path + ".info.json";
        if (System.IO.File.Exists(file_path) && System.IO.File.Exists(file_path))
        {
            var files_info = await DeserializeAsync(json_path, new
            {
                FileName = "",
                Length = 0L,
                MD5 = ""
            }, Cancel);
            return Ok(files_info);
        }

        var files_tasks = Directory.EnumerateFiles(_FilesDirectoryPath, "*.info.json")
           .Select(async f => await DeserializeAsync(f, new
           {
               FileName = "",
               Length = 0L,
               MD5 = ""
           }, Cancel))
           .WhenAsync(async f => (await f).FileName == file);

        var file_info = await files_tasks.FirstOrDefaultAsync(cancellationToken: Cancel);
        if (file_info is null)
            return NotFound();

        return Ok(file_info);
    }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken Cancel)
    {
        _Logger.LogInformation("Загрузка файла {0} (размер {1}Б)", file.FileName, file.Length);

        var files_dir = _FilesDirectoryPath;
        Directory.CreateDirectory(files_dir);
        var file_name = Path.GetRandomFileName();
        var file_path = Path.Combine(files_dir, file_name);

        byte[]? buffer = null;
        string md5_str;
        try
        {
            using var       md5       = MD5.Create();
            await using var dest_file = System.IO.File.Create(file_path);
            await using var src_file  = file.OpenReadStream();

            const int buffer_size = 1024 * 1024;
            buffer = ArrayPool<byte>.Shared.Rent(buffer_size);

            int readed;
            do
            {
                readed = await src_file.ReadAsync(buffer, 0, buffer_size, Cancel);
                await dest_file.WriteAsync(buffer, 0, readed, Cancel);
                md5.ComputeHash(buffer, 0, readed);
            }
            while(readed == buffer_size);

            md5_str = md5.Hash!.Aggregate(new StringBuilder(), (S, b) => S.Append(b.ToString("x2")), S => S.ToString());

        }
        finally
        {
            if(buffer is { })
                ArrayPool<byte>.Shared.Return(buffer);
        }

        System.IO.File.Move(file_path, file_path = Path.Combine(files_dir, md5_str));

        var file_info = new
        {
            file.FileName,
            file.Length,
            MD5 = md5_str
        };

        await using (var meta_file = System.IO.File.Create($"{file_path}.info.json"))
            await JsonSerializer.SerializeAsync(meta_file, file_info, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.Cyrillic, UnicodeRanges.BasicLatin)
            }, Cancel);

        _Logger.LogInformation("Файл загружен {0}", file_info);

        return CreatedAtAction(nameof(GetFileInfo), new { file = md5_str }, file_info);
    }

    [HttpGet("{file}")]
    public async Task<IActionResult> Download(string file, CancellationToken Cancel)
    {
        var files_dir = _FilesDirectoryPath;
        var file_path = Path.Combine(files_dir, file);
        var json_path = file_path + ".info.json";
        if (System.IO.File.Exists(file_path) && System.IO.File.Exists(file_path))
        {
            var files_info = await DeserializeAsync(json_path, new
            {
                FileName = "",
                Length   = 0L,
                MD5      = ""
            }, Cancel);

            var file_type = new FileExtensionContentTypeProvider().TryGetContentType(file_path, out var content_type)
                ? content_type
                : MediaTypeNames.Application.Octet;

            Response.Headers.Add("md5", files_info.MD5);
            return File(System.IO.File.OpenRead(file_path), file_type, files_info.FileName);
        }

        var files_tasks = Directory.EnumerateFiles(_FilesDirectoryPath, "*.info.json")
           .Select(async f => (File: f, Info: await DeserializeAsync(f, new
            {
                FileName = "",
                Length   = 0L,
                MD5      = ""
            }, Cancel)))
           .WhenAsync(async f => (await f).Info.FileName == file);

        (json_path, var find_file_info) = await files_tasks.FirstOrDefaultAsync(cancellationToken: Cancel);
        if (json_path is not { Length: > 0 })
            return NotFound();

        file_path = Path.Combine(files_dir, find_file_info.MD5);

        var find_file_type = new FileExtensionContentTypeProvider().TryGetContentType(file_path, out var find_content_type)
            ? find_content_type
            : MediaTypeNames.Application.Octet;
        Response.Headers.Add("md5", find_file_info.MD5);
        return File(System.IO.File.OpenRead(file_path), find_file_type, find_file_info.FileName);
    }

    [HttpDelete("{file}")]
    public async Task<IActionResult> DeleteFile(string file, CancellationToken Cancel)
    {
        _Logger.LogInformation("Попытка удаления файла {0}", file);

        var files_dir = _FilesDirectoryPath;
        var file_path = Path.Combine(files_dir, file);
        var json_path = file_path + ".info.json";
        if (System.IO.File.Exists(file_path) && System.IO.File.Exists(file_path))
        {
            var files_info = await DeserializeAsync(json_path, new
            {
                FileName = "",
                Length = 0L,
                MD5 = ""
            }, Cancel);

            System.IO.File.Delete(file_path);
            System.IO.File.Delete(json_path);

            _Logger.LogInformation("Файл {0} удалён", files_info);
            return Ok(files_info);
        }

        var files_tasks = Directory.EnumerateFiles(_FilesDirectoryPath, "*.info.json")
           .Select(async f => (File: f, Info: await DeserializeAsync(f, new
           {
               FileName = "",
               Length = 0L,
               MD5 = ""
           }, Cancel)))
           .WhenAsync(async f => (await f).Info.FileName == file);

        (json_path, var find_file_info) = await files_tasks.FirstOrDefaultAsync(cancellationToken: Cancel);
        if (json_path is not { Length: > 0 })
        {
            _Logger.LogInformation("При попытке удаления файла {0} файл не найден", file);
            return NotFound();
        }

        file_path = Path.Combine(files_dir, find_file_info.MD5);

        System.IO.File.Delete(file_path);
        System.IO.File.Delete(json_path);

        if(!Directory.EnumerateFileSystemEntries(files_dir, "*.*", SearchOption.AllDirectories).Any())
            Directory.Delete(files_dir);

        _Logger.LogInformation("Файл {0} удалён", find_file_info);
        return Ok(find_file_info);
    }
}
