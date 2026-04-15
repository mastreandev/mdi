namespace MDI.Philips.M1350;

/// <summary>
/// Represents a 3-character Philips M1350 protocol revision token, for example <c>A20</c>.
/// </summary>
/// <param name="Generation">The revision generation letter.</param>
/// <param name="Minor">The first numeric revision component.</param>
/// <param name="Patch">The second numeric revision component.</param>
public readonly record struct ProtocolRevision(char Generation, byte Minor, byte Patch)
    : IComparable<ProtocolRevision>, IParsable<ProtocolRevision>, ISpanParsable<ProtocolRevision>
{
    public static bool operator <(ProtocolRevision left, ProtocolRevision right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(ProtocolRevision left, ProtocolRevision right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(ProtocolRevision left, ProtocolRevision right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(ProtocolRevision left, ProtocolRevision right)
    {
        return left.CompareTo(right) >= 0;
    }

    /// <summary>
    /// Attempts to parse a Philips M1350 3-character protocol revision token.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out ProtocolRevision revision)
    {
        return TryParse(s, provider: null, out revision);
    }

    /// <inheritdoc />
    public static ProtocolRevision Parse(string s, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(s);

        if (!TryParse(s.AsSpan(), provider, out ProtocolRevision revision))
        {
            throw new FormatException("Protocol revision must be a 3-character token such as A20.");
        }

        return revision;
    }

    /// <inheritdoc />
    public static ProtocolRevision Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out ProtocolRevision revision))
        {
            throw new FormatException("Protocol revision must be a 3-character token such as A20.");
        }

        return revision;
    }

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out ProtocolRevision result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }

        return TryParse(s.AsSpan(), provider, out result);
    }

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out ProtocolRevision result)
    {
        if (s.Length != 3
            || !char.IsAsciiLetterUpper(s[0])
            || !char.IsAsciiDigit(s[1])
            || !char.IsAsciiDigit(s[2]))
        {
            result = default;
            return false;
        }

        result = new(
            s[0],
            Minor: (byte)(s[1] - '0'),
            Patch: (byte)(s[2] - '0'));

        return true;
    }

    /// <inheritdoc />
    public int CompareTo(ProtocolRevision other)
    {
        int generationComparison = this.Generation.CompareTo(other.Generation);
        if (generationComparison != 0)
        {
            return generationComparison;
        }

        int minorComparison = this.Minor.CompareTo(other.Minor);
        if (minorComparison != 0)
        {
            return minorComparison;
        }

        return this.Patch.CompareTo(other.Patch);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return string.Create(
            3,
            this,
            static (span, value) =>
            {
                span[0] = value.Generation;
                span[1] = (char)('0' + value.Minor);
                span[2] = (char)('0' + value.Patch);
            });
    }
}
