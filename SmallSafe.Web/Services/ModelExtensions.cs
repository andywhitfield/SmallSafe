namespace SmallSafe.Web.Services;

public static class ModelExtensions
{
    public static IEnumerable<T> Move<T, K>(this IEnumerable<T> items, T itemToMove, T? prevItem, Func<T, IEquatable<K>> keyFunc)
    {
        var moved = false;
        foreach (var g in items.Where(g => !keyFunc(g).Equals(keyFunc(itemToMove))))
        {
            if (moved)
            {
                yield return g;
                continue;
            }

            if (prevItem is null)
            {
                moved = true;
                yield return itemToMove;
                yield return g;
                continue;
            }

            if (keyFunc(g).Equals(keyFunc(prevItem)))
            {
                moved = true;
                yield return g;
                yield return itemToMove;
                continue;
            }

            yield return g;
        }
        
        if (!moved)
            yield return itemToMove;
    }
}