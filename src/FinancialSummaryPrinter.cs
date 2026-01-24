using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace IbkrToEtax
{
    public static class FinancialSummaryPrinter
    {
        public static void PrintFinancialSummary(XDocument doc, List<XElement> dividends,
                                          List<XElement> withholdingTax, List<XElement> trades,
                                          string accountId)
        {
            Console.WriteLine();
            Console.WriteLine("=== FINANCIAL SUMMARY ===");
            Console.WriteLine();

            PrintDividendsByCurrency(dividends, withholdingTax);
            PrintAccountValues(doc, accountId);
            PrintOtherMetrics(doc, dividends, withholdingTax, trades, accountId);

            Console.WriteLine();
            Console.WriteLine("===========================");
        }

        private static void PrintDividendsByCurrency(List<XElement> dividends, List<XElement> withholdingTax)
        {
            Console.WriteLine("Dividends + Withholding Tax per Currency:");

            var dividendsByCurrency = dividends
                .GroupBy(d => (string?)d.Attribute("currency") ?? "")
                .Select(g => new
                {
                    Currency = g.Key,
                    TotalDividends = g.Sum(d => DataHelper.ParseDecimal((string?)d.Attribute("amount")))
                }).ToList();

            var taxByCurrency = withholdingTax
                .GroupBy(wt => (string?)wt.Attribute("currency") ?? "")
                .Select(g => new
                {
                    Currency = g.Key,
                    TotalTax = Math.Abs(g.Sum(wt => DataHelper.ParseDecimal((string?)wt.Attribute("amount"))))
                }).ToList();

            foreach (var curr in dividendsByCurrency)
            {
                var tax = taxByCurrency.FirstOrDefault(t => t.Currency == curr.Currency);
                decimal totalTax = tax?.TotalTax ?? 0;
                Console.WriteLine($"  {curr.Currency}: Dividends: {curr.TotalDividends:F2}, " +
                                $"Tax: {totalTax:F2}, Gross: {(curr.TotalDividends + totalTax):F2}");
            }
            Console.WriteLine();

            // CHF totals
            decimal totalDividendsCHF = dividends.Sum(d => DataHelper.ConvertToCHF(d));
            decimal totalTaxCHF = withholdingTax.Sum(wt => Math.Abs(DataHelper.ConvertToCHF(wt)));
            Console.WriteLine($"Total Dividends in CHF: {totalDividendsCHF:F2}");
            Console.WriteLine($"Total Withholding Tax in CHF: {totalTaxCHF:F2}");
            Console.WriteLine();
        }

        private static void PrintAccountValues(XDocument doc, string accountId)
        {
            var equitySummaries = doc.Descendants("EquitySummaryByReportDateInBase")
                .Where(e => (string?)e.Attribute("accountId") == accountId)
                .OrderBy(e => (string?)e.Attribute("reportDate") ?? "")
                .ToList();

            var startingSummary = equitySummaries.FirstOrDefault();
            var endingSummary = equitySummaries.LastOrDefault();

            if (startingSummary != null && endingSummary != null)
            {
                decimal startingValue = DataHelper.ParseDecimal((string?)startingSummary.Attribute("total"));
                decimal endingValue = DataHelper.ParseDecimal((string?)endingSummary.Attribute("total"));

                Console.WriteLine("Account Value Summary:");
                Console.WriteLine($"  Starting Value: {startingValue:F2} CHF");
                Console.WriteLine($"  Ending Value: {endingValue:F2} CHF");
                Console.WriteLine();
            }
        }

        private static void PrintOtherMetrics(XDocument doc, List<XElement> dividends,
                                      List<XElement> withholdingTax, List<XElement> trades,
                                      string accountId)
        {
            // Mark-to-Market
            var perfSummary = doc.Descendants("FIFOPerformanceSummaryUnderlying")
                .FirstOrDefault(p => (string?)p.Attribute("description") == "Total (All Assets)");
            if (perfSummary != null)
            {
                decimal mtmPnl = DataHelper.ParseDecimal((string?)perfSummary.Attribute("totalUnrealizedPnl"));
                Console.WriteLine($"Mark-to-Market P&L: {mtmPnl:F2} CHF");
            }

            // Deposits & Withdrawals
            var depositsWithdrawals = doc.Descendants("CashTransaction")
                .Where(ct => (string?)ct.Attribute("type") == "Deposits/Withdrawals" &&
                             (string?)ct.Attribute("accountId") == accountId)
                .Sum(ct => DataHelper.ParseDecimal((string?)ct.Attribute("amount")));
            Console.WriteLine($"Deposits & Withdrawals: {depositsWithdrawals:F2} CHF");

            // Dividends & Tax
            decimal totalDividendsCHF = dividends.Sum(d => DataHelper.ConvertToCHF(d));
            decimal totalTaxCHF = withholdingTax.Sum(wt => Math.Abs(DataHelper.ConvertToCHF(wt)));
            Console.WriteLine($"Dividends: {totalDividendsCHF:F2} CHF");
            Console.WriteLine($"Withholding Tax: {totalTaxCHF:F2} CHF");

            // Change in Dividend Accrual
            var equitySummaries = doc.Descendants("EquitySummaryByReportDateInBase")
                .Where(e => (string?)e.Attribute("accountId") == accountId)
                .OrderBy(e => (string?)e.Attribute("reportDate") ?? "")
                .ToList();

            if (equitySummaries.Count >= 2)
            {
                decimal startingAccruals = DataHelper.ParseDecimal((string?)equitySummaries.First().Attribute("dividendAccruals"));
                decimal endingAccruals = DataHelper.ParseDecimal((string?)equitySummaries.Last().Attribute("dividendAccruals"));
                Console.WriteLine($"Change in Dividend Accrual: {(endingAccruals - startingAccruals):F2} CHF");
            }

            // Commissions
            var totalCommissions = trades
                .Where(t => (string?)t.Attribute("accountId") == accountId &&
                            (string?)t.Attribute("assetCategory") != "CASH")
                .Sum(t =>
                {
                    decimal ibComm = DataHelper.ParseDecimal((string?)t.Attribute("ibCommission"));
                    decimal fx = DataHelper.ParseDecimal((string?)t.Attribute("fxRateToBase"));
                    return Math.Abs(ibComm * fx);
                });
            Console.WriteLine($"Commissions: {totalCommissions:F2} CHF");

            // Sales Tax
            var transactionTaxes = doc.Descendants("TransactionTax")
                .Where(tt => (string?)tt.Attribute("accountId") == accountId)
                .Sum(tt => DataHelper.ParseDecimal((string?)tt.Attribute("taxInBase")));
            Console.WriteLine($"Sales Tax: {transactionTaxes:F2} CHF");

            // FX Translations
            if (perfSummary != null)
            {
                decimal fxPnl = DataHelper.ParseDecimal((string?)perfSummary.Attribute("totalFxPnl"));
                Console.WriteLine($"Other FX Translations: {fxPnl:F2} CHF");
            }
        }
    }
}
