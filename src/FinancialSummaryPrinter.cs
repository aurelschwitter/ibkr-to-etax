using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using IbkrToEtax.IbkrReport;
using Microsoft.Extensions.Logging;

namespace IbkrToEtax
{
    public class FinancialSummaryPrinter(ILogger<FinancialSummaryPrinter> logger)
    {
        private readonly ILogger<FinancialSummaryPrinter> _logger = logger;
        private readonly FinancialSummaryExtractor _extractor = new(logger);

        public void PrintFinancialSummary(IbkrFlexReport report)
        {
            var summary = _extractor.Extract(report);

            Console.WriteLine();
            _logger.LogInformation("=== FINANCIAL SUMMARY ===");
            Console.WriteLine();

            PrintDividendsByCurrency(summary);
            PrintAccountValues(summary);
            PrintOtherMetrics(summary);

            _logger.LogInformation("\n===========================");

        }

        private void PrintDividendsByCurrency(FinancialSummary summary)
        {
            _logger.LogInformation("Dividends + Withholding Tax per Currency:");

            foreach (var curr in summary.DividendsByCurrency)
            {
                _logger.LogInformation("  {Currency}: Dividends: {Dividends:F2}, Tax: {Tax:F2}, Gross: {Gross:F2}",
                    curr.Currency, curr.TotalDividends, curr.TotalTax, curr.Gross);
            }
            Console.WriteLine();

            // CHF totals
            _logger.LogInformation("Total Dividends in CHF: {TotalDividends:F2}", summary.TotalDividendsCHF);
            _logger.LogInformation("Total Withholding Tax in CHF: {TotalTax:F2}", summary.TotalTaxCHF);
            Console.WriteLine();
        }

        private void PrintAccountValues(FinancialSummary summary)
        {
            if (summary.AccountValues != null)
            {
                _logger.LogInformation("Account Value Summary:");
                _logger.LogInformation("  Starting Value: {StartingValue:F2} CHF", summary.AccountValues.StartingValue);
                _logger.LogInformation("  Ending Value: {EndingValue:F2} CHF", summary.AccountValues.EndingValue);
                Console.WriteLine();
            }
        }

        private void PrintOtherMetrics(FinancialSummary summary)
        {
            _logger.LogInformation("Mark-to-Market P&L: {MtmPnl:F2} CHF", summary.MarkToMarketPnl);
            _logger.LogInformation("Deposits & Withdrawals: {DepositsWithdrawals:F2} CHF", summary.DepositsWithdrawals);
            _logger.LogInformation("Dividends: {TotalDividends:F2} CHF", summary.TotalDividendsCHF);
            _logger.LogInformation("Withholding Tax: {TotalTax:F2} CHF", summary.TotalTaxCHF);
            _logger.LogInformation("Change in Dividend Accrual: {AccrualChange:F2} CHF", summary.ChangeInDividendAccrual);
            _logger.LogInformation("Commissions: {TotalCommissions:F2} CHF", summary.TotalCommissions);
            _logger.LogInformation("Sales Tax: {SalesTax:F2} CHF", summary.SalesTax);
            _logger.LogInformation("Other FX Translations: {FxPnl:F2} CHF", summary.FxTranslations);
        }
    }
}
