using System;
using System.Globalization;
using System.Xml.Linq;
using IbkrToEtax.IbkrReport;
using Microsoft.Extensions.Logging;

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
                _ => ibkrCountry ?? "n/a"
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

        public static decimal ConvertToCHF(IIbkrForeignCashValueElement element, ILogger? logger = null)
        {
            if (element.FxRateToBase == 0)
            {
                logger?.LogWarning("FX rate is zero, cannot convert to CHF");
            }

            return element.Amount * element.FxRateToBase;
        }

        /// <summary>
        /// Rounds a total/sum value according to eCH-0196 standard (DIN 1333):
        /// - Amounts < 100: 3 decimal places
        /// - Amounts >= 100: 2 decimal places
        /// - Standard rounding (0.5 rounds up = MidpointRounding.AwayFromZero)
        /// </summary>
        public static decimal RoundTotal(decimal value)
        {
            int decimals = Math.Abs(value) < 100 ? 3 : 2;
            return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Formats a total value according to eCH-0196 standard for XML output.
        /// The decimal places are determined by the rounded value.
        /// </summary>
        public static string FormatTotal(decimal value)
        {
            decimal rounded = RoundTotal(value);
            // Check the rounded value to determine decimal places
            int decimals = Math.Abs(rounded) < 100 ? 3 : 2;
            return rounded.ToString($"F{decimals}", CultureInfo.InvariantCulture);
        }
    }
}
