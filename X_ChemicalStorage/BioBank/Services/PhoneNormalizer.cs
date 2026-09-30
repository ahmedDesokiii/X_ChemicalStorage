using System.Text.RegularExpressions;

namespace X_ChemicalStorage.BioBank.Services
{
    public static class PhoneNormalizer
    {
        /// يحوّل الأرقام المصرية والدولية لصيغة +CCXXXXXXXX. يرجّع null لو الرقم مش صالح.
        public static string? Normalize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            var s = Regex.Replace(input.Trim(), @"[\s\-\(\)\.]", "");
            if (s.StartsWith("+")) { /* ok */ }
            else if (s.StartsWith("00")) s = "+" + s.Substring(2);
            else if (s.StartsWith("20") && s.Length == 12) s = "+" + s;          // 2010xxxxxxxx
            else if (s.StartsWith("0") && s.Length == 11) s = "+20" + s.Substring(1); // 010xxxxxxxx
            else return null;

            return Regex.IsMatch(s, @"^\+\d{8,15}$") ? s : null;
        }
    }
}