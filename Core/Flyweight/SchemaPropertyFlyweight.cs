namespace JsonTool.Core.Flyweight;
public sealed class SchemaPropertyFlyweight : IEquatable<SchemaPropertyFlyweight>
{
    public string Type { get; }
    public string? Format { get; }
    public string? Pattern { get; }
    public string Key { get; }
    public DateTime CreatedAt { get; }
    private int _usageCount;
    public int UsageCount => _usageCount;
    internal SchemaPropertyFlyweight(string type, string? format, string? pattern)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Format = NormalizeValue(format);
        Pattern = NormalizeValue(pattern);
        Key = GenerateKey(type, Format, Pattern);
        CreatedAt = DateTime.Now;
        _usageCount = 0;
    }
    internal void IncrementUsage()
    {
        Interlocked.Increment(ref _usageCount);
    }
    public static string GenerateKey(string type, string? format, string? pattern)
    {
        var formatPart = string.IsNullOrEmpty(format) ? "_" : format;
        var patternPart = string.IsNullOrEmpty(pattern) ? "_" : ComputeHash(pattern);
        return $"{type}|{formatPart}|{patternPart}";
    }

    private static string? NormalizeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string ComputeHash(string input)
    {
        if (input.Length <= 20) return input;
        
        var hash = 0;
        foreach (var c in input)
        {
            hash = (hash * 31 + c) & 0x7FFFFFFF;
        }
        return $"h{hash:X8}";
    }
    public bool Matches(string type, string? format = null, string? pattern = null)
    {
        return Type == type &&
               Format == NormalizeValue(format) &&
               Pattern == NormalizeValue(pattern);
    }
    public string GetDisplayType()
    {
        if (!string.IsNullOrEmpty(Format))
        {
            return $"{Type} ({Format})";
        }
        return Type;
    }
    public int GetApproximateSize()
    {
        var size = 24; // Object header + references
        size += (Type?.Length ?? 0) * 2;
        size += (Format?.Length ?? 0) * 2;
        size += (Pattern?.Length ?? 0) * 2;
        size += (Key?.Length ?? 0) * 2;
        size += 8; // DateTime
        size += 4; // int
        return size;
    }

    #region Equality

    public bool Equals(SchemaPropertyFlyweight? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Key == other.Key;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as SchemaPropertyFlyweight);
    }

    public override int GetHashCode()
    {
        return Key.GetHashCode();
    }

    public static bool operator ==(SchemaPropertyFlyweight? left, SchemaPropertyFlyweight? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(SchemaPropertyFlyweight? left, SchemaPropertyFlyweight? right)
    {
        return !(left == right);
    }

    #endregion

    public override string ToString()
    {
        return $"Flyweight[{Key}] (used {UsageCount}x)";
    }
}