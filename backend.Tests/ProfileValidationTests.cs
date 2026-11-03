using EquipamentosMedicosApi.Validation;

namespace backend.Tests;

public sealed class ProfileValidationTests
{
    [Theory]
    [InlineData("52998224725", true)]
    [InlineData("529.982.247-25", true)]
    [InlineData("12345678901", false)]
    [InlineData("11111111111", false)]
    [InlineData("529a98224725", false)]
    [InlineData("5299822472", false)]
    public void ChecksCpfFormatAndCheckDigits(string value, bool valid) =>
        Assert.Equal(valid, ProfileValidation.IsValidCpf(value));

    [Theory]
    [InlineData("11999999999", true)]
    [InlineData("(11) 3333-4444", true)]
    [InlineData("+55 (11) 99999-9999", true)]
    [InlineData("telefone123", false)]
    [InlineData("01999999999", false)]
    [InlineData("1199999999", false)]
    [InlineData("11123456789", false)]
    [InlineData("", false)]
    public void ChecksBrazilianPhoneWithAreaCode(string value, bool valid) =>
        Assert.Equal(valid, ProfileValidation.IsValidPhone(value));

    [Theory]
    [InlineData("01001-000", true)]
    [InlineData("01001000", true)]
    [InlineData("0100100", false)]
    [InlineData("010010000", false)]
    [InlineData("abcdefgh", false)]
    public void ChecksZipCodeFormat(string value, bool valid) =>
        Assert.Equal(valid, ProfileValidation.IsValidZipCode(value));

    [Theory]
    [InlineData("sp", true)]
    [InlineData("DF", true)]
    [InlineData("ZZ", false)]
    [InlineData("São Paulo", false)]
    public void ChecksStateCode(string value, bool valid) =>
        Assert.Equal(valid, ProfileValidation.IsValidState(value));

    [Fact]
    public void NormalizesFormattingWithoutRemovingNationalAreaCode()
    {
        Assert.Equal("11999999999", ProfileValidation.NormalizePhone("+55 (11) 99999-9999"));
        Assert.Equal("55999999999", ProfileValidation.NormalizePhone("(55) 99999-9999"));
        Assert.Equal("52998224725", ProfileValidation.Digits("529.982.247-25"));
    }

    [Fact]
    public void OptionalAddressCanBeAbsentInApi()
    {
        Assert.True(ProfileValidation.IsValidZipCode(null));
        Assert.True(ProfileValidation.IsValidState(""));
    }
}
