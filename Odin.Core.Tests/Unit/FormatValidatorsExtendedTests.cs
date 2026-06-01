using Odin.Core.Validation;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class FormatValidatorsExtendedTests
{
    private static bool IsValid(string value, string format)
    {
        var result = FormatValidators.ValidateFormat(value, format);
        Assert.NotNull(result);
        return result!.IsValid;
    }

    [Theory]
    [InlineData("urn:isbn:0451450523", true)]
    [InlineData("mailto:user@example.com", true)]
    [InlineData("/relative/path", false)]
    public void Uri(string value, bool expected) => Assert.Equal(expected, IsValid(value, "uri"));

    [Theory]
    [InlineData("example.com", true)]
    [InlineData("sub.example.co.uk", true)]
    [InlineData("-bad.example.com", false)]
    [InlineData("bad_underscore.com", false)]
    public void Hostname(string value, bool expected) => Assert.Equal(expected, IsValid(value, "hostname"));

    [Theory]
    [InlineData("2024-06-15T10:30:00", true)]
    [InlineData("2024-06-15T10:30:00Z", true)]
    [InlineData("2024-06-15 10:30:00", false)]
    [InlineData("2024-06-15", false)]
    public void Datetime(string value, bool expected) => Assert.Equal(expected, IsValid(value, "datetime"));

    [Theory]
    [InlineData("2024-06-15T10:30:00Z", true)]
    [InlineData("06/15/2024", false)]
    public void DateTimeAlias(string value, bool expected) => Assert.Equal(expected, IsValid(value, "date-time"));

    [Theory]
    [InlineData("4111111111111111", true)]
    [InlineData("4111111111111112", false)]
    [InlineData("411111111111", false)]
    public void CreditCard(string value, bool expected) => Assert.Equal(expected, IsValid(value, "credit-card"));

    [Theory]
    [InlineData("GB82WEST12345698765432", true)]
    [InlineData("DE89370400440532013000", true)]
    [InlineData("1234WEST", false)]
    public void Iban(string value, bool expected) => Assert.Equal(expected, IsValid(value, "iban"));

    [Theory]
    [InlineData("DEUTDEFF", true)]
    [InlineData("DEUTDEFF500", true)]
    [InlineData("DEUTDEFF5", false)]
    public void Bic(string value, bool expected) => Assert.Equal(expected, IsValid(value, "bic"));

    [Theory]
    [InlineData("BOFAUS3N", true)]
    [InlineData("BOFAUS3", false)]
    public void Swift(string value, bool expected) => Assert.Equal(expected, IsValid(value, "swift"));

    [Theory]
    [InlineData("021000021", true)]
    [InlineData("12345678", false)]
    public void Routing(string value, bool expected) => Assert.Equal(expected, IsValid(value, "routing"));

    [Theory]
    [InlineData("037833100", true)]
    [InlineData("03783310", false)]
    [InlineData("037833$00", false)]
    public void Cusip(string value, bool expected) => Assert.Equal(expected, IsValid(value, "cusip"));

    [Theory]
    [InlineData("US0378331005", true)]
    [InlineData("US037833100", false)]
    [InlineData("US037833100X", false)]
    public void Isin(string value, bool expected) => Assert.Equal(expected, IsValid(value, "isin"));

    [Theory]
    [InlineData("529900T8BM49AURSDO55", true)]
    [InlineData("529900T8BM49AURSDO5", false)]
    [InlineData("529900T8BM49AURSDO5$", false)]
    public void Lei(string value, bool expected) => Assert.Equal(expected, IsValid(value, "lei"));

    [Theory]
    [InlineData("1234567890", true)]
    [InlineData("123456789", false)]
    [InlineData("12345678901", false)]
    public void Npi(string value, bool expected) => Assert.Equal(expected, IsValid(value, "npi"));

    [Theory]
    [InlineData("AB1234567", true)]
    [InlineData("A1234567", false)]
    [InlineData("AB123456", false)]
    public void Dea(string value, bool expected) => Assert.Equal(expected, IsValid(value, "dea"));

    [Theory]
    [InlineData("490154203237518", true)]
    [InlineData("49015420323751", false)]
    [InlineData("4901542032375189", false)]
    public void Imei(string value, bool expected) => Assert.Equal(expected, IsValid(value, "imei"));

    [Theory]
    [InlineData("8901234567890123456", true)]
    [InlineData("89012345678901234567", true)]
    [InlineData("890123456789012345", false)]
    [InlineData("8901234567890123456X", false)]
    public void Iccid(string value, bool expected) => Assert.Equal(expected, IsValid(value, "iccid"));
}
