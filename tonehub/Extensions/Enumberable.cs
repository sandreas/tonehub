namespace tonehub.Extensions;

public static class Enumberable
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> enumerable)
    {
        foreach(var item in enumerable)
        {
            yield return await Task.FromResult(item);
        }
    } 
}