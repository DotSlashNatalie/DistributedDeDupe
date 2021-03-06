using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

// Copied from https://stackoverflow.com/a/22323356
public static class OrderingExtension
{
    public static IEnumerable<T> OrderByNatural<T>(this IEnumerable<T> items, Func<T, string> selector,
        StringComparer stringComparer = null)
    {
        var regex = new Regex(@"\d+", RegexOptions.Compiled);

        int maxDigits = items
            .SelectMany(i =>
                regex.Matches(selector(i)).Cast<Match>().Select(digitChunk => (int?) digitChunk.Value.Length))
            .Max() ?? 0;

        return items.OrderBy(i => regex.Replace(selector(i), match => match.Value.PadLeft(maxDigits, '0')),
            stringComparer ?? StringComparer.CurrentCulture);
    }
}