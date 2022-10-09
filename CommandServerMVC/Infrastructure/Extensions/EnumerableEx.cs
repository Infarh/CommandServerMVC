using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandServerMVC.Infrastructure.Extensions;

internal static class EnumerableEx
{
    //public static IEnumerable<Task<TResult>> SelectAsync<T, TResult>(this IEnumerable<Task<T>> items, Func<Task<T>, Task<TResult/>>)

    public static async IAsyncEnumerable<T> WhenAsync<T>(
        this IEnumerable<Task<T>> tasks, 
        Func<Task<T>, Task<bool>> Selector)
    {
        foreach (var item in tasks)
        {
            if (await Selector(item))
                yield return await item;
        }
    }
}
