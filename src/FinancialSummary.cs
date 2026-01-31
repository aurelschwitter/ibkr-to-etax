using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using IbkrToEtax.IbkrReport;
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

        public FinancialSummary Extract(IbkrFlexReport report)
        {
            var summary = new FinancialSummary();

            // Extract dividends by currency
            summary.DividendsByCurrency = ExtractDividendsByCurrency(report.DividendList, report.WithholdingTaxList);

            // CHF totals
            summary.TotalDividendsCHF = report.DividendList.Sum(d => DataHelper.ConvertToCHF(d, _logger));
            summary.TotalTaxCHF = report.WithholdingTaxList.Sum(wt => Math.Abs(DataHelper.ConvertToCHF(wt, _logger)));

            // Account values
            summary.AccountValues = ExtractAccountValues(report.EquitySummaryList, report.AccountId);

            // Other metrics
            summary.MarkToMarketPnl = ExtractMarkToMarketPnl(report.FifoTransactions);
            summary.DepositsWithdrawals = ExtractDepositsWithdrawals(report.DepositWithdrawalList, report.AccountId);
            summary.ChangeInDividendAccrual = ExtractDividendAccrualChange(report.EquitySummaryList, report.AccountId);
            summary.TotalCommissions = ExtractCommissions(report.Trades, report.AccountId);
            summary.FxTranslations = ExtractFxTranslations(report.FifoTransactions);

            return summary;
        }

        private List<CurrencySummary> ExtractDividendsByCurrency(List<IbkrCashTransaction> dividends, List<IbkrCashTransaction> withholdingTax)
        {
            var dividendsByCurrency = dividends
                .GroupBy(d => d.Currency)
                .Select(g => new
                {
                    Currency = g.Key,
                    TotalDividends = g.Sum(d => d.Amount)
                }).ToList();

            var taxByCurrency = withholdingTax
                .GroupBy(wt => wt.Currency)
                .Select(g => new
                {
                    Currency = g.Key,
                    TotalTax = Math.Abs(g.Sum(wt => wt.Amount))
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

        private AccountValueSummary? ExtractAccountValues(List<IbkrEquitySummary> equitySummaries, string accountId)
        {
            var filteredSummaries = equitySummaries
                .Where(e => e.AccountId == accountId)
                .OrderBy(e => e.ReportDate)
                .ToList();

            var startingSummary = filteredSummaries.FirstOrDefault();
            var endingSummary = filteredSummaries.LastOrDefault();
            if (startingSummary != null && endingSummary != null)
            {
                return new AccountValueSummary
                {
                    StartingValue = startingSummary.Total,
                    EndingValue = endingSummary.Total
                };
            }

            return null;
        }

        private decimal ExtractMarkToMarketPnl(List<IbkrFifoPerformanceSummary> fifoPerformanceSummaries)
        {
            var perfSummary = fifoPerformanceSummaries
                .FirstOrDefault(p => p.Description == "Total (All Assets)");

            return perfSummary != null
                ? perfSummary.TotalUnrealizedPnl
                : 0;
        }

        private decimal ExtractDepositsWithdrawals(List<IbkrCashTransaction> cashTransactions, string accountId)
        {
            return cashTransactions
                .Where(ct => ct.Type == "Deposits/Withdrawals" &&
                             ct.AccountId == accountId)
                .Sum(ct => ct.Amount);
        }

        private decimal ExtractDividendAccrualChange(List<IbkrEquitySummary> equitySummaries, string accountId)
        {
            var filteredSummaries = equitySummaries
                .Where(e => e.AccountId == accountId)
                .OrderBy(e => e.ReportDate)
                .ToList();

            if (equitySummaries.Count >= 2)
            {
                decimal startingAccruals = equitySummaries.First().DividendAccruals;
                decimal endingAccruals = equitySummaries.Last().DividendAccruals;
                return endingAccruals - startingAccruals;
            }

            return 0;
        }

        private decimal ExtractCommissions(List<IbkrTrade> trades, string accountId)
        {
            return trades
                .Where(t => t.AccountId == accountId &&
                            t.AssetCategory != "CASH")
                .Sum(t =>
                {
                    decimal ibComm = t.IbCommission;
                    decimal fx = t.FxRateToBase;
                    return Math.Abs(ibComm * fx);
                });
        }

        private decimal ExtractFxTranslations(List<IbkrFifoPerformanceSummary> fifoPerformanceSummaries)
        {
            var perfSummary = fifoPerformanceSummaries
                .FirstOrDefault(p => p.Description == "Total (All Assets)");

            return perfSummary != null
                ? perfSummary.TotalFxPnl
                : 0;
        }
    }
}
