using System;
using System.Globalization;
using System.Xml.Linq;

namespace IbkrToEtax
{
    public static class DataHelper
    {
        public static decimal ParseDecimal(string? value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
        }

        public static DateTime? ParseNullableDate(string? value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            return DateTime.TryParse(value, out var result) ? result : null;
        }

        public static string MapCountry(string ibkrCountry)
        {
            // IBKR uses country codes, eCH uses ISO2
            return ibkrCountry switch
            {
                "US" => "US",
                "CH" => "CH",
                _ => ibkrCountry ?? "CH"
            };
        }

        public static string MapSecurityCategory(string assetCategory)
        {
            // Map IBKR asset categories to eCH security categories
            return assetCategory switch
            {
                "STK" => "SHARE",  // Stocks/ETFs
                "BOND" => "BOND",
                "OPT" => "OPTION",
                "FUT" => "DEVT",
                _ => "OTHER"
            };
        }

        public static decimal ConvertToCHF(XElement transaction)
        {
            decimal amount = ParseDecimal((string?)transaction.Attribute("amount"));
            decimal fxRate = ParseDecimal((string?)transaction.Attribute("fxRateToBase"));
            return amount * fxRate;
        }
    }
}
