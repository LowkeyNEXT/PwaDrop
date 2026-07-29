using PwaDrop.Core;

namespace PwaDrop.Core.Tests;

public sealed class FileNameSanitizerTests
{
    [Theory]
    [InlineData("invoice.pdf", "invoice.pdf")]
    [InlineData("../../ticket.eml", "ticket.eml")]
    [InlineData("CON.txt", "_CON.txt")]
    [InlineData("bad:name?.pdf", "bad_name_.pdf")]
    [InlineData("trailing. ", "trailing")]
    public void SanitizeProducesSafeWindowsNames(string input, string expected)
    {
        Assert.Equal(expected, FileNameSanitizer.Sanitize(input, 0));
    }

    [Fact]
    public void MakeUniqueAddsStableSuffixes()
    {
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Assert.Equal("message.eml", FileNameSanitizer.MakeUnique("message.eml", claimed));
        Assert.Equal("message (2).eml", FileNameSanitizer.MakeUnique("message.eml", claimed));
        Assert.Equal("message (3).eml", FileNameSanitizer.MakeUnique("message.eml", claimed));
    }

    [Fact]
    public void SanitizeConstrainsLongNamesWithoutLosingExtension()
    {
        var result = FileNameSanitizer.Sanitize(new string('a', 240) + ".eml", 0);

        Assert.True(result.Length <= 180);
        Assert.EndsWith(".eml", result, StringComparison.Ordinal);
    }
}

