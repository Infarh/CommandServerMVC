using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CommandServerMVC.Infrastructure.Extensions;

internal static class FormFileEx
{
    public static async Task CopyToAsync(this IFormFile file, Stream dest_file, Action<byte[]> Process, CancellationToken Cancel = default, int BufferSize = 1024 * 1024)
    {
        byte[]? buffer = null;

        try
        {
            await using var src = file.OpenReadStream();

            buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            int readed;
            do
            {
                readed = await src.ReadAsync(buffer, 0, BufferSize, Cancel);
                await dest_file.WriteAsync(buffer, 0, readed, Cancel);
                Process(buffer);
            }
            while (readed == BufferSize);
        }
        finally
        {
            if (buffer is { })
                ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
