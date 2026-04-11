namespace Jaina.Core.Extensions;

public static class CollectionExtensions
{
    public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source)
            action(item);
    }

    public static async Task ForEachAsync<T>(this IEnumerable<T> source, Func<T, Task> action)
    {
        foreach (var item in source)
            await action(item).ConfigureAwait(false);
    }

    public static (IEnumerable<T> Matched, IEnumerable<T> Unmatched) Partition<T>(
        this IEnumerable<T> source, Func<T, bool> predicate)
    {
        var matched = new List<T>();
        var unmatched = new List<T>();

        foreach (var item in source)
        {
            if (predicate(item))
                matched.Add(item);
            else
                unmatched.Add(item);
        }

        return (matched, unmatched);
    }


}
