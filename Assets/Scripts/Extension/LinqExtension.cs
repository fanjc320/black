using System.Collections.Generic;

public static class LinqExtension
{
    public static IEnumerable<T> SkipLast<T>(this IEnumerable<T> source)
    {
        using (var e = source.GetEnumerator())
        {
            if (e.MoveNext())
            {
                for (var value = e.Current; e.MoveNext(); value = e.Current)
                {
                    yield return value;
                }
            }
        }
    }
}