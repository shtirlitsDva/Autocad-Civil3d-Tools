using System;
using System.Collections.Generic;

namespace DimensioneringV2.Services.Report;

/// <summary>
/// Compares NodeId strings numerically.
/// Supports both flat IDs ("1", "2", "10") and dotted IDs ("1.1", "1.2", "2.1").
/// Dotted IDs sort by prefix first, then by suffix.
/// </summary>
internal class NodeIdComparer : IComparer<string>
{
    public static readonly NodeIdComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (x == y) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        var xParts = x.Split('.');
        var yParts = y.Split('.');

        int len = Math.Max(xParts.Length, yParts.Length);
        for (int i = 0; i < len; i++)
        {
            if (i >= xParts.Length) return -1;
            if (i >= yParts.Length) return 1;

            bool xIsNum = int.TryParse(xParts[i], out int xNum);
            bool yIsNum = int.TryParse(yParts[i], out int yNum);

            int cmp;
            if (xIsNum && yIsNum)
                cmp = xNum.CompareTo(yNum);
            else
                cmp = string.Compare(xParts[i], yParts[i], StringComparison.Ordinal);

            if (cmp != 0) return cmp;
        }

        return 0;
    }
}
