using System.Text.RegularExpressions;

namespace EquipamentosMedicosApi.Validation;

public static class ProfileValidation
{
    private static readonly HashSet<string> States = new(
        "AC AL AP AM BA CE DF ES GO MA MT MS MG PA PB PR PE PI RJ RN RS RO RR SC SP SE TO".Split(' '));

    public static string Digits(string value) => Regex.Replace(value, "[^0-9]", "");

    public static string NormalizePhone(string value)
    {
        var digits = Digits(value);
        return Regex.IsMatch(digits, "^55[0-9]{10,11}$") ? digits[2..] : digits;
    }

    public static bool IsValidPhone(string? value)
    {
        value = value?.Trim();
        return !string.IsNullOrEmpty(value) && value.Length <= 20 &&
            Regex.IsMatch(value, @"^\+?[0-9()\s-]+$") &&
            Regex.IsMatch(NormalizePhone(value), "^[1-9][0-9](?:[2-5][0-9]{7}|9[0-9]{8})$");
    }

    public static bool IsValidZipCode(string? value) =>
        string.IsNullOrWhiteSpace(value) || Regex.IsMatch(value.Trim(), "^[0-9]{5}-?[0-9]{3}$");

    public static bool IsValidState(string? value) =>
        string.IsNullOrWhiteSpace(value) || States.Contains(value.Trim().ToUpperInvariant());

    public static bool IsValidCpf(string? value)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value) ||
            !Regex.IsMatch(value, @"^(?:[0-9]{11}|[0-9]{3}\.[0-9]{3}\.[0-9]{3}-[0-9]{2})$"))
            return false;

        var digits = Digits(value);
        if (digits.Distinct().Count() == 1) return false;
        foreach (var length in new[] { 9, 10 })
        {
            var sum = 0;
            for (var index = 0; index < length; index++)
                sum += (digits[index] - '0') * (length + 1 - index);
            if ((sum * 10 % 11) % 10 != digits[length] - '0') return false;
        }
        return true;
    }
}
