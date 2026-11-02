using Avalonia;

namespace PhotoX.App.Services;

/// <summary>
/// ↑ and ↓ in a wrapping thumbnail grid (prompt_10 §7): the card in the row above or below whose centre is nearest
/// horizontally, from the cards' positions as laid out, so it follows the columns after resizing.
/// </summary>
public static class GridNavigation
{
    /// <summary>The index of the card <paramref name="rows"/> rows away (−1 up, +1 down), or −1 when there is no such row.</summary>
    public static int Neighbour(IReadOnlyList<Rect> cards, int from, int rows)
    {
        if (from < 0 || from >= cards.Count || rows == 0) return -1;
        var here = cards[from];
        var centre = here.Center.X;
        // Rows are the distinct tops of the cards; the target row is the nearest one past the current card's row.
        var tops = cards.Select(c => c.Top).Distinct().Order().ToList();
        var row = tops.FindIndex(t => Math.Abs(t - here.Top) < 1);
        var target = row + rows;
        if (row < 0 || target < 0 || target >= tops.Count) return -1;
        var best = -1;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < cards.Count; i++)
        {
            if (Math.Abs(cards[i].Top - tops[target]) >= 1) continue;
            var distance = Math.Abs(cards[i].Center.X - centre);
            if (distance < bestDistance) (best, bestDistance) = (i, distance);
        }

        return best;
    }
}
