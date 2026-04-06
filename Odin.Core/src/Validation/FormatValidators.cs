using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Odin.Core.Validation
{
    /// <summary>
    /// Result of a format validation check.
    /// </summary>
    public sealed class FormatValidationResult
    {
        /// <summary>Whether the value is valid for the format.</summary>
        public bool IsValid { get; }

        /// <summary>Error message if invalid.</summary>
        public string ErrorMessage { get; }

        private FormatValidationResult(bool isValid, string errorMessage)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }

        /// <summary>Creates a successful result.</summary>
        public static FormatValidationResult Valid() => new FormatValidationResult(true, "");

        /// <summary>Creates an error result.</summary>
        public static FormatValidationResult Error(string message) => new FormatValidationResult(false, message);
    }

    /// <summary>
    /// Validates string values against named format constraints: email, url, uuid, ssn,
    /// vin, phone, zip, ipv4, ipv6, creditcard, date-iso, naic, fein, currency-code,
    /// country-alpha2, country-alpha3, state-us.
    /// All regex operations use <see cref="TimeSpan"/> timeout for ReDoS protection.
    /// </summary>
    public static class FormatValidators
    {
        /// <summary>
        /// Validates a string value against a named format.
        /// </summary>
        /// <param name="value">The string value to validate.</param>
        /// <param name="format">The format name (case-insensitive).</param>
        /// <returns>
        /// A <see cref="FormatValidationResult"/> if the format is recognized;
        /// null if the format name is not recognized.
        /// </returns>
        public static FormatValidationResult ValidateFormat(string value, string format)
        {
            switch (format.ToLowerInvariant())
            {
                case "email": return ValidateEmail(value);
                case "url": return ValidateUrl(value);
                case "uuid": return ValidateUuid(value);
                case "ssn": return ValidateSsn(value);
                case "vin": return ValidateVin(value);
                case "phone": return ValidatePhone(value);
                case "zip": return ValidateZip(value);
                case "ipv4": return ValidateIpv4(value);
                case "ipv6": return ValidateIpv6(value);
                case "creditcard": return ValidateCreditCard(value);
                case "date-iso": return ValidateDateIso(value);
                case "naic": return ValidateNaic(value);
                case "fein": return ValidateFein(value);
                case "currency-code": return ValidateCurrencyCode(value);
                case "country-alpha2": return ValidateCountryAlpha2(value);
                case "country-alpha3": return ValidateCountryAlpha3(value);
                case "state-us": return ValidateStateUs(value);
                default: return null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Individual Validators
        // ─────────────────────────────────────────────────────────────────────────

        private static FormatValidationResult ValidateEmail(string value)
        {
            int atCount = 0;
            int atPos = -1;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '@')
                {
                    atCount++;
                    atPos = i;
                }
            }

            if (atCount != 1)
                return FormatValidationResult.Error("Invalid email format");

            var local = value.Substring(0, atPos);
            if (local.Length == 0)
                return FormatValidationResult.Error("Invalid email format");

            var domain = value.Substring(atPos + 1);
            if (domain.Length == 0 || domain.IndexOf('.') < 0)
                return FormatValidationResult.Error("Invalid email format");

            return FormatValidationResult.Valid();
        }

        private static FormatValidationResult ValidateUrl(string value)
        {
            if (value.StartsWith("http://", StringComparison.Ordinal) ||
                value.StartsWith("https://", StringComparison.Ordinal))
                return FormatValidationResult.Valid();
            return FormatValidationResult.Error("Invalid URL format");
        }

        private static FormatValidationResult ValidateUuid(string value)
        {
            // 8-4-4-4-12 hex with dashes: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
            if (value.Length != 36)
                return FormatValidationResult.Error("Invalid UUID format");

            int[] groupLengths = { 8, 4, 4, 4, 12 };
            int pos = 0;
            for (int g = 0; g < groupLengths.Length; g++)
            {
                for (int i = 0; i < groupLengths[g]; i++)
                {
                    if (pos >= value.Length || !IsHexDigit(value[pos]))
                        return FormatValidationResult.Error("Invalid UUID format");
                    pos++;
                }
                if (g < 4)
                {
                    if (pos >= value.Length || value[pos] != '-')
                        return FormatValidationResult.Error("Invalid UUID format");
                    pos++;
                }
            }

            return FormatValidationResult.Valid();
        }

        private static FormatValidationResult ValidateSsn(string value)
        {
            var digits = ExtractDigits(value);
            if (digits.Length != 9)
                return FormatValidationResult.Error("Invalid SSN format");

            // Could be ###-##-#### or #########
            bool validFormat = value.Length == 9
                || (value.Length == 11 && value[3] == '-' && value[6] == '-');
            if (!validFormat)
                return FormatValidationResult.Error("Invalid SSN format");

            // Area code (first 3 digits) cannot be 000
            if (digits[0] == '0' && digits[1] == '0' && digits[2] == '0')
                return FormatValidationResult.Error("Invalid SSN - area code cannot be 000");

            return FormatValidationResult.Valid();
        }

        private static FormatValidationResult ValidateVin(string value)
        {
            if (value.Length != 17)
                return FormatValidationResult.Error("VIN must be 17 characters");

            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                char upper = char.ToUpperInvariant(ch);
                if (upper == 'I' || upper == 'O' || upper == 'Q')
                    return FormatValidationResult.Error("VIN cannot contain I, O, or Q");
                if (!char.IsLetterOrDigit(ch))
                    return FormatValidationResult.Error("VIN must be 17 characters");
            }

            return FormatValidationResult.Valid();
        }

        private static FormatValidationResult ValidatePhone(string value)
        {
            int digitCount = 0;
            bool seenPlus = false;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (ch >= '0' && ch <= '9')
                {
                    digitCount++;
                }
                else if (ch == '-' || ch == ' ' || ch == '(' || ch == ')')
                {
                    // allowed formatting chars
                }
                else if (ch == '+')
                {
                    if (i != 0 || seenPlus)
                        return FormatValidationResult.Error("Invalid phone format");
                    seenPlus = true;
                }
                else
                {
                    return FormatValidationResult.Error("Invalid phone format");
                }
            }

            if (digitCount < 7)
                return FormatValidationResult.Error("Invalid phone format");

            return FormatValidationResult.Valid();
        }

        private static FormatValidationResult ValidateZip(string value)
        {
            if (value.Length == 5)
            {
                for (int i = 0; i < 5; i++)
                {
                    if (!char.IsDigit(value[i]))
                        return FormatValidationResult.Error("Invalid ZIP format");
                }
                return FormatValidationResult.Valid();
            }

            if (value.Length == 10)
            {
                for (int i = 0; i < 5; i++)
                {
                    if (!char.IsDigit(value[i]))
                        return FormatValidationResult.Error("Invalid ZIP format");
                }
                if (value[5] != '-')
                    return FormatValidationResult.Error("Invalid ZIP format");
                for (int i = 6; i < 10; i++)
                {
                    if (!char.IsDigit(value[i]))
                        return FormatValidationResult.Error("Invalid ZIP format");
                }
                return FormatValidationResult.Valid();
            }

            return FormatValidationResult.Error("Invalid ZIP format");
        }

        private static FormatValidationResult ValidateIpv4(string value)
        {
            var parts = value.Split('.');
            if (parts.Length != 4)
                return FormatValidationResult.Error("Invalid IPv4 format");

            for (int i = 0; i < 4; i++)
            {
                var part = parts[i];
                if (part.Length == 0 || part.Length > 3)
                    return FormatValidationResult.Error("Invalid IPv4 format");

                for (int j = 0; j < part.Length; j++)
                {
                    if (!char.IsDigit(part[j]))
                        return FormatValidationResult.Error("Invalid IPv4 format");
                }

                // No leading zeros (except "0" itself)
                if (part.Length > 1 && part[0] == '0')
                    return FormatValidationResult.Error("Invalid IPv4 format");

                if (!uint.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n > 255)
                    return FormatValidationResult.Error("Invalid IPv4 format");
            }

            return FormatValidationResult.Valid();
        }

        private static FormatValidationResult ValidateIpv6(string value)
        {
            if (value.IndexOf("::", StringComparison.Ordinal) >= 0)
            {
                // Handle :: compressed notation
                int first = value.IndexOf("::", StringComparison.Ordinal);
                if (value.IndexOf("::", first + 2, StringComparison.Ordinal) >= 0)
                    return FormatValidationResult.Error("Invalid IPv6 format");

                var left = value.Substring(0, first);
                var right = value.Substring(first + 2);

                var leftGroups = left.Length == 0 ? new string[0] : left.Split(':');
                var rightGroups = right.Length == 0 ? new string[0] : right.Split(':');

                int total = leftGroups.Length + rightGroups.Length;
                if (total > 7)
                    return FormatValidationResult.Error("Invalid IPv6 format");

                foreach (var group in leftGroups)
                {
                    if (!IsValidIpv6Group(group))
                        return FormatValidationResult.Error("Invalid IPv6 format");
                }
                foreach (var group in rightGroups)
                {
                    if (!IsValidIpv6Group(group))
                        return FormatValidationResult.Error("Invalid IPv6 format");
                }

                return FormatValidationResult.Valid();
            }
            else
            {
                // Full notation: exactly 8 groups
                var groups = value.Split(':');
                if (groups.Length != 8)
                    return FormatValidationResult.Error("Invalid IPv6 format");

                foreach (var group in groups)
                {
                    if (!IsValidIpv6Group(group))
                        return FormatValidationResult.Error("Invalid IPv6 format");
                }

                return FormatValidationResult.Valid();
            }
        }

        private static bool IsValidIpv6Group(string group)
        {
            if (group.Length == 0 || group.Length > 4)
                return false;
            for (int i = 0; i < group.Length; i++)
            {
                if (!IsHexDigit(group[i]))
                    return false;
            }
            return true;
        }

        private static FormatValidationResult ValidateCreditCard(string value)
        {
            // Verify only valid characters
            for (int i = 0; i < value.Length; i++)
            {
                char b = value[i];
                if (!char.IsDigit(b) && b != ' ' && b != '-')
                    return FormatValidationResult.Error("Invalid credit card format");
            }

            // Extract digits only
            var digits = new byte[value.Length];
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsDigit(value[i]))
                    digits[count++] = (byte)(value[i] - '0');
            }

            if (count < 13 || count > 19)
                return FormatValidationResult.Error("Invalid credit card format");

            // Luhn checksum
            if (!LuhnCheck(digits, count))
                return FormatValidationResult.Error("Invalid credit card checksum");

            return FormatValidationResult.Valid();
        }

        private static bool LuhnCheck(byte[] digits, int count)
        {
            int sum = 0;
            for (int i = count - 1, j = 0; i >= 0; i--, j++)
            {
                int n = digits[i];
                if (j % 2 == 1)
                {
                    n *= 2;
                    if (n > 9) n -= 9;
                }
                sum += n;
            }
            return sum % 10 == 0;
        }

        private static FormatValidationResult ValidateDateIso(string value)
        {
            // YYYY-MM-DD
            if (value.Length != 10)
                return FormatValidationResult.Error($"Value '{value}' does not match date-iso format (YYYY-MM-DD)");

            for (int i = 0; i < 10; i++)
            {
                if (i == 4 || i == 7)
                {
                    if (value[i] != '-')
                        return FormatValidationResult.Error($"Value '{value}' does not match date-iso format (YYYY-MM-DD)");
                }
                else
                {
                    if (!char.IsDigit(value[i]))
                        return FormatValidationResult.Error($"Value '{value}' does not match date-iso format (YYYY-MM-DD)");
                }
            }

            return FormatValidationResult.Valid();
        }

        private static FormatValidationResult ValidateNaic(string value)
        {
            if (value.Length != 5)
                return FormatValidationResult.Error("Invalid NAIC code format");
            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsDigit(value[i]))
                    return FormatValidationResult.Error("Invalid NAIC code format");
            }
            return FormatValidationResult.Valid();
        }

        private static FormatValidationResult ValidateFein(string value)
        {
            // ##-#######
            if (value.Length != 10)
                return FormatValidationResult.Error("Invalid FEIN format");
            if (!char.IsDigit(value[0]) || !char.IsDigit(value[1]) || value[2] != '-')
                return FormatValidationResult.Error("Invalid FEIN format");
            for (int i = 3; i < 10; i++)
            {
                if (!char.IsDigit(value[i]))
                    return FormatValidationResult.Error("Invalid FEIN format");
            }
            return FormatValidationResult.Valid();
        }

        private static FormatValidationResult ValidateCurrencyCode(string value)
        {
            if (value.Length != 3)
                return FormatValidationResult.Error("Unknown currency code");
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] < 'A' || value[i] > 'Z')
                    return FormatValidationResult.Error("Unknown currency code");
            }

            if (Array.BinarySearch(CurrencyCodes, value, StringComparer.Ordinal) < 0)
                return FormatValidationResult.Error("Unknown currency code");

            return FormatValidationResult.Valid();
        }

        private static FormatValidationResult ValidateCountryAlpha2(string value)
        {
            if (value.Length != 2)
                return FormatValidationResult.Error("Invalid country code");
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] < 'A' || value[i] > 'Z')
                    return FormatValidationResult.Error("Invalid country code");
            }

            if (Array.BinarySearch(CountryAlpha2Codes, value, StringComparer.Ordinal) < 0)
                return FormatValidationResult.Error("Invalid country code");

            return FormatValidationResult.Valid();
        }

        private static FormatValidationResult ValidateCountryAlpha3(string value)
        {
            if (value.Length != 3)
                return FormatValidationResult.Error("Invalid country code");
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] < 'A' || value[i] > 'Z')
                    return FormatValidationResult.Error("Invalid country code");
            }

            if (Array.BinarySearch(CountryAlpha3Codes, value, StringComparer.Ordinal) < 0)
                return FormatValidationResult.Error("Invalid country code");

            return FormatValidationResult.Valid();
        }

        private static FormatValidationResult ValidateStateUs(string value)
        {
            if (value.Length != 2)
                return FormatValidationResult.Error("Invalid US state code");
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] < 'A' || value[i] > 'Z')
                    return FormatValidationResult.Error("Invalid US state code");
            }

            if (Array.BinarySearch(StateUsCodes, value, StringComparer.Ordinal) < 0)
                return FormatValidationResult.Error("Invalid US state code");

            return FormatValidationResult.Valid();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────

        private static bool IsHexDigit(char ch)
        {
            return (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');
        }

        private static char[] ExtractDigits(string value)
        {
            var result = new char[value.Length];
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsDigit(value[i]))
                    result[count++] = value[i];
            }
            var digits = new char[count];
            Array.Copy(result, digits, count);
            return digits;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Lookup Tables (sorted for binary search)
        // ─────────────────────────────────────────────────────────────────────────

        private static readonly string[] CurrencyCodes =
        {
            "AED", "ARS", "AUD", "BDT", "BGN", "BHD", "BRL", "CAD", "CHF", "CLP",
            "CNY", "COP", "CZK", "DKK", "EGP", "EUR", "GBP", "GHS", "HKD", "HRK",
            "HUF", "IDR", "ILS", "INR", "ISK", "JOD", "JPY", "KES", "KRW", "KWD",
            "LBP", "MAD", "MXN", "MYR", "NGN", "NOK", "NZD", "OMR", "PEN", "PHP",
            "PKR", "PLN", "QAR", "RON", "RUB", "SAR", "SEK", "SGD", "THB", "TRY",
            "TWD", "TZS", "UAH", "UGX", "USD", "VND", "ZAR",
        };

        private static readonly string[] CountryAlpha2Codes =
        {
            "AE", "AR", "AT", "AU", "BD", "BE", "BG", "BH", "BR", "CA",
            "CH", "CL", "CN", "CO", "CY", "CZ", "DE", "DK", "EG", "ES",
            "FI", "FR", "GB", "GH", "GR", "HK", "HR", "HU", "ID", "IE",
            "IL", "IN", "IS", "IT", "JO", "JP", "KE", "KR", "KW", "LB",
            "MA", "MX", "MY", "NG", "NL", "NO", "NZ", "OM", "PE", "PH",
            "PK", "PL", "PT", "QA", "RO", "RU", "SA", "SE", "SG", "TH",
            "TR", "TW", "TZ", "UA", "UG", "US", "VN", "ZA",
        };

        private static readonly string[] CountryAlpha3Codes =
        {
            "ARE", "ARG", "AUS", "AUT", "BEL", "BGD", "BGR", "BHR", "BRA", "CAN",
            "CHE", "CHL", "CHN", "COL", "CYP", "CZE", "DEU", "DNK", "EGY", "ESP",
            "FIN", "FRA", "GBR", "GHA", "GRC", "HKG", "HRV", "HUN", "IDN", "IND",
            "IRL", "ISL", "ISR", "ITA", "JOR", "JPN", "KEN", "KOR", "KWT", "LBN",
            "MAR", "MEX", "MYS", "NGA", "NLD", "NOR", "NZL", "OMN", "PAK", "PER",
            "PHL", "POL", "PRT", "QAT", "ROU", "RUS", "SAU", "SGP", "SWE", "THA",
            "TUR", "TWN", "TZA", "UGA", "UKR", "USA", "VNM", "ZAF",
        };

        private static readonly string[] StateUsCodes =
        {
            "AK", "AL", "AR", "AS", "AZ", "CA", "CO", "CT", "DC", "DE",
            "FL", "GA", "GU", "HI", "IA", "ID", "IL", "IN", "KS", "KY",
            "LA", "MA", "MD", "ME", "MI", "MN", "MO", "MP", "MS", "MT",
            "NC", "ND", "NE", "NH", "NJ", "NM", "NV", "NY", "OH", "OK",
            "OR", "PA", "PR", "RI", "SC", "SD", "TN", "TX", "UT", "VA",
            "VI", "VT", "WA", "WI", "WV", "WY",
        };
    }
}
