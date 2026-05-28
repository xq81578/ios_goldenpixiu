using System;

/// <summary>
/// 一個取代 UnityEngine.Vector2Int 的自訂整數二維向量結構體。
/// 實作了 IEquatable<T> 和相關方法，以確保在 HashSet 或 Dictionary 中能正確運作。
/// </summary>
public struct Coord : IEquatable<Coord>
{
    public int X { get; }
    public int Y { get; }

    public Coord(int x, int y)
    {
        X = x;
        Y = y;
    }

    // 實作 IEquatable<T> 介面，這是最高效的比較方式
    public bool Equals(Coord other)
    {
        return X == other.X && Y == other.Y;
    }

    // 覆寫 object.Equals，確保與其他物件比較時的正確性
    public override bool Equals(object obj)
    {
        return obj is Coord other && Equals(other);
    }

    // 覆寫 GetHashCode，這對於在 HashSet 和 Dictionary 中作為鍵值至關重要！
    // 若不覆寫，效能會很差且行為可能不正確。
    public override int GetHashCode()
    {
        // 一個簡單而高效的雜湊碼組合方式
        return HashCode.Combine(X, Y);
    }

    // 覆寫 == 和 != 運算子，讓語法更自然
    public static bool operator ==(Coord left, Coord right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Coord left, Coord right)
    {
        return !(left == right);
    }

    public override string ToString()
    {
        return $"({X}, {Y})";
    }
}
