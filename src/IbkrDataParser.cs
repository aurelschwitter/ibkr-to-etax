using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace IbkrToEtax
{
    public static class IbkrDataParser
    {
        private const string LEVEL_SUMMARY = "SUMMARY";

        public static (List<XElement> openPositions, List<XElement> trades, List<XElement> dividends,
                List<XElement> withholdingTax) ParseIbkrData(XDocument doc, string accountId)
        {
            var cashTransactions = doc.Descendants("CashTransaction")
                .Where(ct => (string?)ct.Attribute("accountId") == accountId)
                .ToList();

            return (
                openPositions: doc.Descendants("OpenPosition")
                    .Where(op => (string?)op.Attribute("levelOfDetail") == LEVEL_SUMMARY)
                    .ToList(),
                trades: doc.Descendants("Trade")
                    .Where(t => (string?)t.Attribute("accountId") == accountId)
                    .ToList(),
                dividends: cashTransactions
                    .Where(ct => (string?)ct.Attribute("type") == "Dividends")
                    .ToList(),
                withholdingTax: cashTransactions
                    .Where(ct => (string?)ct.Attribute("type") == "Withholding Tax")
                    .ToList()
            );
        }

        public static (DateTime periodFrom, DateTime periodTo, int taxYear) ExtractDateRange(List<XElement> flexStatements)
        {
            var fromDate = flexStatements
                .Select(fs => (string?)fs.Attribute("fromDate"))
                .Where(d => !string.IsNullOrEmpty(d))
                .Select(d => DateTime.Parse(d!))
                .OrderBy(d => d)
                .FirstOrDefault();

            var toDate = flexStatements
                .Select(fs => (string?)fs.Attribute("toDate"))
                .Where(d => !string.IsNullOrEmpty(d))
                .Select(d => DateTime.Parse(d!))
                .OrderByDescending(d => d)
                .FirstOrDefault();

            if (fromDate == default || toDate == default)
            {
                // Fallback to current year
                int currentYear = DateTime.Now.Year;
                return (new DateTime(currentYear, 1, 1), new DateTime(currentYear, 12, 31), currentYear);
            }

            return (fromDate, toDate, toDate.Year);
        }

        public static void PrintDataLoadSummary(ILogger logger, List<XElement> openPositions, List<XElement> trades,
                                         List<XElement> dividends, List<XElement> withholdingTax,
                                         XElement? accountInfo)
        {
            logger?.LogInformation("Loaded IBKR data: {PositionCount} positions, {TradeCount} trades, {DividendCount} dividends, {WithholdingTaxCount} withholding tax entries",
                openPositions.Count, trades.Count, dividends.Count, withholdingTax.Count);

            if (accountInfo != null)
            {
                string accountId = (string?)accountInfo.Attribute("accountId") ?? "Unknown";
                string accountName = (string?)accountInfo.Attribute("name") ?? "Unknown";
                logger?.LogInformation("Account: {AccountId} - {AccountName}", accountId, accountName);
            }
        }
    }
}
