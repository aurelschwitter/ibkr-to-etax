using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace IbkrToEtax
{
    public class FinancialSummary
    {
        public List<CurrencySummary> DividendsByCurrency { get; set; } = new();
        public decimal TotalDividendsCHF { get; set; }
        public decimal TotalTaxCHF { get; set; }
        public AccountValueSummary? AccountValues { get; set; }
        public decimal MarkToMarketPnl { get; set; }
        public decimal DepositsWithdrawals { get; set; }
        public decimal ChangeInDividendAccrual { get; set; }
        public decimal TotalCommissions { get; set; }
        public decimal SalesTax { get; set; }
        public decimal FxTranslations { get; set; }
    }

    public class CurrencySummary
    {
        public string Currency { get; set; } = "";
        public decimal TotalDividends { get; set; }
        public decimal TotalTax { get; set; }
        public decimal Gross => TotalDividends + TotalTax;
    }

    public class AccountValueSummary
    {
        public decimal StartingValue { get; set; }
        public decimal EndingValue { get; set; }
    }

    public class FinancialSummaryExtractor
    {
        private readonly ILogger? _logger;

        public FinancialSummaryExtractor(ILogger? logger = null)
        {
            _logger = logger;
        }

        public FinancialSummary Extract(XDocument doc, List<XElement> dividends,
                                        List<XElement> withholdingTax, List<XElement> trades,
                                        string accountId)
        {
            var summary = new FinancialSummary();

            // Extract dividends by currency
            summary.DividendsByCurrency = ExtractDividendsByCurrency(dividends, withholdingTax);

            // CHF totals
            summary.TotalDividendsCHF = dividends.Sum(d => DataHelper.ConvertToCHF(d, _logger));
            summary.TotalTaxCHF = withholdingTax.Sum(wt => Math.Abs(DataHelper.ConvertToCHF(wt, _logger)));

            // Account values
            summary.AccountValues = ExtractAccountValues(doc, accountId);

            // Other metrics
            summary.MarkToMarketPnl = ExtractMarkToMarketPnl(doc);
            summary.DepositsWithdrawals = ExtractDepositsWithdrawals(doc, accountId);
            summary.ChangeInDividendAccrual = ExtractDividendAccrualChange(doc, accountId);
            summary.TotalCommissions = ExtractCommissions(trades, accountId);
            summary.SalesTax = ExtractSalesTax(doc, accountId);
            summary.FxTranslations = ExtractFxTranslations(doc);

            return summary;
        }

        private List<CurrencySummary> ExtractDividendsByCurrency(List<XElement> dividends, List<XElement> withholdingTax)
        {
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

            return dividendsByCurrency.Select(curr =>
            {
                var tax = taxByCurrency.FirstOrDefault(t => t.Currency == curr.Currency);
                return new CurrencySummary
                {
                    Currency = curr.Currency,
                    TotalDividends = curr.TotalDividends,
                    TotalTax = tax?.TotalTax ?? 0
                };
            }).ToList();
        }

        private AccountValueSummary? ExtractAccountValues(XDocument doc, string accountId)
        {
            var equitySummaries = doc.Descendants("EquitySummaryByReportDateInBase")
                .Where(e => (string?)e.Attribute("accountId") == accountId)
                .OrderBy(e => (string?)e.Attribute("reportDate") ?? "")
                .ToList();

            var startingSummary = equitySummaries.FirstOrDefault();
            var endingSummary = equitySummaries.LastOrDefault();

            if (startingSummary != null && endingSummary != null)
            {
                return new AccountValueSummary
                {
                    StartingValue = DataHelper.ParseDecimal((string?)startingSummary.Attribute("total")),
                    EndingValue = DataHelper.ParseDecimal((string?)endingSummary.Attribute("total"))
                };
            }

            return null;
        }

        private decimal ExtractMarkToMarketPnl(XDocument doc)
        {
            var perfSummary = doc.Descendants("FIFOPerformanceSummaryUnderlying")
                .FirstOrDefault(p => (string?)p.Attribute("description") == "Total (All Assets)");
            
            return perfSummary != null 
                ? DataHelper.ParseDecimal((string?)perfSummary.Attribute("totalUnrealizedPnl"))
                : 0;
        }

        private decimal ExtractDepositsWithdrawals(XDocument doc, string accountId)
        {
            return doc.Descendants("CashTransaction")
                .Where(ct => (string?)ct.Attribute("type") == "Deposits/Withdrawals" &&
                             (string?)ct.Attribute("accountId") == accountId)
                .Sum(ct => DataHelper.ParseDecimal((string?)ct.Attribute("amount")));
        }

        private decimal ExtractDividendAccrualChange(XDocument doc, string accountId)
        {
            var equitySummaries = doc.Descendants("EquitySummaryByReportDateInBase")
                .Where(e => (string?)e.Attribute("accountId") == accountId)
                .OrderBy(e => (string?)e.Attribute("reportDate") ?? "")
                .ToList();

            if (equitySummaries.Count >= 2)
            {
                decimal startingAccruals = DataHelper.ParseDecimal((string?)equitySummaries.First().Attribute("dividendAccruals"));
                decimal endingAccruals = DataHelper.ParseDecimal((string?)equitySummaries.Last().Attribute("dividendAccruals"));
                return endingAccruals - startingAccruals;
            }

            return 0;
        }

        private decimal ExtractCommissions(List<XElement> trades, string accountId)
        {
            return trades
                .Where(t => (string?)t.Attribute("accountId") == accountId &&
                            (string?)t.Attribute("assetCategory") != "CASH")
                .Sum(t =>
                {
                    decimal ibComm = DataHelper.ParseDecimal((string?)t.Attribute("ibCommission"));
                    decimal fx = DataHelper.ParseDecimal((string?)t.Attribute("fxRateToBase"));
                    return Math.Abs(ibComm * fx);
                });
        }

        private decimal ExtractSalesTax(XDocument doc, string accountId)
        {
            return doc.Descendants("TransactionTax")
                .Where(tt => (string?)tt.Attribute("accountId") == accountId)
                .Sum(tt => DataHelper.ParseDecimal((string?)tt.Attribute("taxInBase")));
        }

        private decimal ExtractFxTranslations(XDocument doc)
        {
            var perfSummary = doc.Descendants("FIFOPerformanceSummaryUnderlying")
                .FirstOrDefault(p => (string?)p.Attribute("description") == "Total (All Assets)");
            
            return perfSummary != null
                ? DataHelper.ParseDecimal((string?)perfSummary.Attribute("totalFxPnl"))
                : 0;
        }
    }
}
