using System.Globalization;
using ES.FX.Primitives.Extensions;

namespace ES.FX.Tests.Primitives;

public class StringExtensionsTests
{
    // ---- Truncate ----

    [Fact]
    public void Truncate_Null_ReturnsNull()
    {
        string? value = null;
        Assert.Null(value.Truncate(5));
    }

    [Fact]
    public void Truncate_ShorterThanLength_ReturnsOriginal()
    {
        Assert.Equal("abc", "abc".Truncate(10));
    }

    [Fact]
    public void Truncate_EqualToLength_ReturnsOriginal()
    {
        Assert.Equal("abc", "abc".Truncate(3));
    }

    [Fact]
    public void Truncate_LongerThanLength_ReturnsPrefix()
    {
        Assert.Equal("abc", "abcdef".Truncate(3));
    }

    [Fact]
    public void Truncate_ZeroLength_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, "abc".Truncate(0));
    }

    [Fact]
    public void Truncate_NegativeLength_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => "abc".Truncate(-1));
    }

    // ---- TruncateOrDefault ----

    [Fact]
    public void TruncateOrDefault_Null_ReturnsDefault()
    {
        string? value = null;
        Assert.Equal("fallback", value.TruncateOrDefault(5, "fallback"));
    }

    [Fact]
    public void TruncateOrDefault_Empty_ReturnsDefault()
    {
        Assert.Equal("fallback", string.Empty.TruncateOrDefault(5, "fallback"));
    }

    [Fact]
    public void TruncateOrDefault_NonEmpty_ReturnsTruncated()
    {
        Assert.Equal("abc", "abcdef".TruncateOrDefault(3, "fallback"));
    }

    [Fact]
    public void TruncateOrDefault_NegativeLength_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => "abc".TruncateOrDefault(-1, "fallback"));
    }

    // ---- SplitIntoChunks ----

    [Fact]
    public void SplitIntoChunks_Null_ReturnsEmptyArray()
    {
        string? value = null;
        Assert.Empty(value.SplitIntoChunks(3));
    }

    [Fact]
    public void SplitIntoChunks_EvenlyDivisible_ReturnsChunks()
    {
        Assert.Equal(new[] { "abc", "def" }, "abcdef".SplitIntoChunks(3));
    }

    [Fact]
    public void SplitIntoChunks_Remainder_LastChunkShorter()
    {
        Assert.Equal(new[] { "abc", "de" }, "abcde".SplitIntoChunks(3));
    }

    [Fact]
    public void SplitIntoChunks_Empty_ReturnsEmptyArray()
    {
        Assert.Empty(string.Empty.SplitIntoChunks(3));
    }

    [Fact]
    public void SplitIntoChunks_LengthZero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => "abc".SplitIntoChunks(0));
    }

    [Fact]
    public void SplitIntoChunks_NegativeLength_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => "abc".SplitIntoChunks(-1));
    }

    [Fact]
    public void SplitIntoChunks_NullWithBadLength_ReturnsEmpty_NoThrow()
    {
        // null short-circuits before the length check.
        string? value = null;
        Assert.Empty(value.SplitIntoChunks(0));
    }

    // ---- ToTitleCase ----

    [Fact]
    public void ToTitleCase_Null_ReturnsNull()
    {
        string? value = null;
        Assert.Null(value.ToTitleCase());
    }

    [Fact]
    public void ToTitleCase_LowercaseWords_Capitalized()
    {
        Assert.Equal("Hello World", "hello world".ToTitleCase());
    }

    [Fact]
    public void ToTitleCase_UppercaseWords_Normalized()
    {
        // ToTitleCase first lowercases then title-cases, so all-caps input is normalized.
        Assert.Equal("Hello World", "HELLO WORLD".ToTitleCase());
    }

    [Fact]
    public void ToTitleCase_ExplicitCulture_Used()
    {
        Assert.Equal("Hello World", "hello world".ToTitleCase(CultureInfo.InvariantCulture));
    }

    // ---- RemoveDiacritics ----

    [Fact]
    public void RemoveDiacritics_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, string.Empty.RemoveDiacritics());
    }

    [Fact]
    public void RemoveDiacritics_StripsAccents()
    {
        Assert.Equal("aeiou", "áéíóú".RemoveDiacritics());
        Assert.Equal("Cafe", "Café".RemoveDiacritics());
        Assert.Equal("naive", "naïve".RemoveDiacritics());
    }

    [Fact]
    public void RemoveDiacritics_StrokeMapping_DjToD()
    {
        Assert.Equal("D", "Đ".RemoveDiacritics());
        Assert.Equal("d", "đ".RemoveDiacritics());
        // Đ->D and đ->d; the plain letters are left as-is (no phonetic transliteration).
        Assert.Equal("Dorde", "Đorđe".RemoveDiacritics());
    }

    [Fact]
    public void RemoveDiacritics_PlainAscii_Unchanged()
    {
        Assert.Equal("plain", "plain".RemoveDiacritics());
    }

    [Fact]
    public void RemoveDiacritics_Ligature_Decomposed()
    {
        // FormKD compatibility decomposition splits the "fi" ligature.
        Assert.Equal("fi", "ﬁ".RemoveDiacritics());
    }
}