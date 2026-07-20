using System;
using System.Collections.Generic;
using System.Linq;

namespace ClientApp.Services
{
    public class CountryInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty; // e.g. "+1", "+91", "+44"
        public string Flag { get; set; } = string.Empty; // e.g. "🇺🇸", "🇮🇳"
        public string DisplayText => $"{Flag} {Code} ({Name})";

        public override string ToString()
        {
            return Code;
        }
    }

    public static class CountryCodeHelper
    {
        private static readonly List<CountryInfo> Countries = new List<CountryInfo>
        {
            new CountryInfo { Name = "United States", Code = "+1", Flag = "🇺🇸" },
            new CountryInfo { Name = "India", Code = "+91", Flag = "🇮🇳" },
            new CountryInfo { Name = "United Kingdom", Code = "+44", Flag = "🇬🇧" },
            new CountryInfo { Name = "Canada", Code = "+1", Flag = "🇨🇦" },
            new CountryInfo { Name = "Australia", Code = "+61", Flag = "🇦🇺" },
            new CountryInfo { Name = "Germany", Code = "+49", Flag = "🇩🇪" },
            new CountryInfo { Name = "France", Code = "+33", Flag = "🇫🇷" },
            new CountryInfo { Name = "Italy", Code = "+39", Flag = "🇮🇹" },
            new CountryInfo { Name = "Japan", Code = "+81", Flag = "🇯🇵" },
            new CountryInfo { Name = "China", Code = "+86", Flag = "🇨🇳" },
            new CountryInfo { Name = "Brazil", Code = "+55", Flag = "🇧🇷" },
            new CountryInfo { Name = "South Africa", Code = "+27", Flag = "🇿🇦" },
            new CountryInfo { Name = "Mexico", Code = "+52", Flag = "🇲🇽" },
            new CountryInfo { Name = "Spain", Code = "+34", Flag = "🇪🇸" },
            new CountryInfo { Name = "Russia", Code = "+7", Flag = "🇷🇺" },
            new CountryInfo { Name = "Netherlands", Code = "+31", Flag = "🇳🇱" },
            new CountryInfo { Name = "Sweden", Code = "+46", Flag = "🇸🇪" },
            new CountryInfo { Name = "Switzerland", Code = "+41", Flag = "🇨🇭" },
            new CountryInfo { Name = "Singapore", Code = "+65", Flag = "🇸🇬" },
            new CountryInfo { Name = "New Zealand", Code = "+64", Flag = "🇳🇿" },
            new CountryInfo { Name = "United Arab Emirates", Code = "+971", Flag = "🇦🇪" },
            new CountryInfo { Name = "Saudi Arabia", Code = "+966", Flag = "🇸🇦" },
            new CountryInfo { Name = "Turkey", Code = "+90", Flag = "🇹🇷" },
            new CountryInfo { Name = "Argentina", Code = "+54", Flag = "🇦🇷" },
            new CountryInfo { Name = "Colombia", Code = "+57", Flag = "🇨🇴" },
            new CountryInfo { Name = "Indonesia", Code = "+62", Flag = "🇮🇩" },
            new CountryInfo { Name = "Malaysia", Code = "+60", Flag = "🇲🇾" },
            new CountryInfo { Name = "Philippines", Code = "+63", Flag = "🇵🇭" },
            new CountryInfo { Name = "Thailand", Code = "+66", Flag = "🇹🇭" },
            new CountryInfo { Name = "Vietnam", Code = "+84", Flag = "🇻🇳" },
            new CountryInfo { Name = "Pakistan", Code = "+92", Flag = "🇵🇰" },
            new CountryInfo { Name = "Bangladesh", Code = "+880", Flag = "🇧🇩" },
            new CountryInfo { Name = "Nigeria", Code = "+234", Flag = "🇳🇬" },
            new CountryInfo { Name = "Egypt", Code = "+20", Flag = "🇪🇬" },
            new CountryInfo { Name = "Poland", Code = "+48", Flag = "🇵🇱" },
            new CountryInfo { Name = "Belgium", Code = "+32", Flag = "🇧🇪" },
            new CountryInfo { Name = "Austria", Code = "+43", Flag = "🇦🇹" },
            new CountryInfo { Name = "Denmark", Code = "+45", Flag = "🇩🇰" },
            new CountryInfo { Name = "Finland", Code = "+358", Flag = "🇫🇮" },
            new CountryInfo { Name = "Norway", Code = "+47", Flag = "🇳🇴" },
            new CountryInfo { Name = "Ireland", Code = "+353", Flag = "🇮🇪" },
            new CountryInfo { Name = "Portugal", Code = "+351", Flag = "🇵🇹" },
            new CountryInfo { Name = "Greece", Code = "+30", Flag = "🇬🇷" },
            new CountryInfo { Name = "Israel", Code = "+972", Flag = "🇮🇱" },
            new CountryInfo { Name = "Hong Kong", Code = "+852", Flag = "🇭🇰" },
            new CountryInfo { Name = "Taiwan", Code = "+886", Flag = "🇹🇼" },
            new CountryInfo { Name = "South Korea", Code = "+82", Flag = "🇰🇷" },
            new CountryInfo { Name = "Ukraine", Code = "+380", Flag = "🇺🇦" },
            new CountryInfo { Name = "Chile", Code = "+56", Flag = "🇨🇱" },
            new CountryInfo { Name = "Peru", Code = "+51", Flag = "🇵🇪" },
            new CountryInfo { Name = "Venezuela", Code = "+58", Flag = "🇻🇪" },
            new CountryInfo { Name = "Ecuador", Code = "+593", Flag = "🇪🇨" },
            new CountryInfo { Name = "Morocco", Code = "+212", Flag = "🇲🇦" },
            new CountryInfo { Name = "Kenya", Code = "+254", Flag = "🇰🇪" },
            new CountryInfo { Name = "Sri Lanka", Code = "+94", Flag = "🇱🇰" },
            new CountryInfo { Name = "Nepal", Code = "+977", Flag = "🇳🇵" }
        };

        public static List<CountryInfo> GetCountries()
        {
            return Countries.OrderBy(c => c.Name).ToList();
        }

        public static bool IsPhoneNumberValid(string number)
        {
            if (string.IsNullOrWhiteSpace(number)) return false;
            // Allow spaces, dashes, parentheses
            string clean = System.Text.RegularExpressions.Regex.Replace(number, @"[\s\-\(\)]", "");
            // Standard length limit for active phone numbers (between 7 and 15 digits)
            return System.Text.RegularExpressions.Regex.IsMatch(clean, @"^\d{7,15}$");
        }

        /// <summary>
        /// Tries to split a stored full phone number (e.g. "+919876543210")
        /// into (countryCode, localNumber).
        /// If no matching prefix is found, returns (defaultPrefix, inputNumber).
        /// </summary>
        public static (string countryCode, string localNumber) ParsePhoneNumber(string fullNumber, string defaultPrefix = "+1")
        {
            if (string.IsNullOrWhiteSpace(fullNumber))
                return (defaultPrefix, string.Empty);

            fullNumber = fullNumber.Trim();
            if (!fullNumber.StartsWith("+"))
                return (defaultPrefix, fullNumber);

            // Sort countries by prefix code length descending to match longer prefixes first (e.g., +880 before +8)
            var sortedCountries = Countries.OrderByDescending(c => c.Code.Length).ToList();
            foreach (var country in sortedCountries)
            {
                if (fullNumber.StartsWith(country.Code))
                {
                    string local = fullNumber.Substring(country.Code.Length).Trim();
                    return (country.Code, local);
                }
            }

            return (defaultPrefix, fullNumber);
        }
    }
}
