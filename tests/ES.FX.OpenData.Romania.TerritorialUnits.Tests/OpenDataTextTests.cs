using ES.FX.OpenData.Romania.TerritorialUnits.Internal;

namespace ES.FX.OpenData.Romania.TerritorialUnits.Tests;

public class OpenDataTextTests
{
    [Theory]
    [InlineData("Cluj-Napoca", "cluj napoca")]
    [InlineData("  Alba Iulia  ", "alba iulia")]
    [InlineData("Piatra-Neamț", "piatra neamt")]
    public void Fold_strips_diacritics_spaces_hyphens_and_case(string input, string expected) =>
        Assert.Equal(expected, OpenDataText.Fold(input));

    [Fact]
    public void Fold_is_identical_for_cedilla_and_comma_below_encodings()
    {
        // "Iași" with legacy cedilla (U+015F) vs modern comma-below (U+0219) must fold identically.
        var cedilla = "Iaşi";
        var commaBelow = "Iași";

        Assert.Equal("iasi", OpenDataText.Fold(cedilla));
        Assert.Equal(OpenDataText.Fold(commaBelow), OpenDataText.Fold(cedilla));
    }
}