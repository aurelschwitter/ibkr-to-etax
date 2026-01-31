using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using System.Xml.Linq;
using IbkrToEtax.IbkrReport;
using Microsoft.Extensions.Logging;

namespace IbkrToEtax
{
    public class EchStatementBuilder(IbkrFlexReport ibkrReport, ILoggerFactory loggerFactory)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<EchStatementBuilder>();
        private readonly IbkrFlexReport _ibkrReport = ibkrReport;

        public static string GenerateEchId(string accountId, DateTime date, int sequenceNumber = 1)
        {
            // eCH-0196 Section 2.1: ID format
            // CH (country) + zeros + {accountId} + {YYYYMMDD} + {seq 2 digits}
            // Total length should be consistent with standard format
            string countryCode = "CH";
            string organizationId = "00000"; // 5-digit organization/clearing number

            // Pad account ID to fixed width (8 digits for client number)
            // If account has letters (like U14798214), keep them
            string customerNumber = accountId.Length <= 8
                ? accountId.PadLeft(8, '0')
                : accountId.Substring(accountId.Length - 8);

            // Add padding zeros between org and customer number to reach proper length
            int paddingZeros = 19 - (organizationId.Length + customerNumber.Length); // Total should be 19 chars before date
            string padding = new('0', Math.Max(0, paddingZeros));

            string dateStr = date.ToString("yyyyMMdd");
            string seqStr = sequenceNumber.ToString("D2");

            // Must start with alphanumeric (xs:ID requirement)
            return $"{countryCode}{organizationId}{padding}{customerNumber}{dateStr}{seqStr}";
        }

        public EchTaxStatement BuildEchTaxStatement()
        {
            var statement = new EchTaxStatement
            {
                Id = GenerateEchId(_ibkrReport.AccountId, _ibkrReport.EndDate),
                TaxPeriod = _ibkrReport.TaxYear,
                PeriodFrom = _ibkrReport.StartDate,
                PeriodTo = _ibkrReport.EndDate,
                Canton = _ibkrReport.Canton,
                ClientNumber = _ibkrReport.AccountId
            };

            var depot = new EchSecurityDepot { DepotNumber = _ibkrReport.AccountId };
            statement.Depots.Add(depot);

            // Process each position - use the latest (year-end) position for each symbol
            int positionId = 1;
            foreach (var summary in _ibkrReport.SummaryPerPositionList)
            {
                var security = BuildSecurity(summary, positionId++);
                depot.Securities.Add(security);
            }

            // Add cash position from year-end EquitySummary
            var yearEndEquitySummary = _ibkrReport.EquitySummaryList.FirstOrDefault(e =>
            e.AccountId == _ibkrReport.AccountId &&
            e.ReportDate.ToString("yyyy-MM-dd") == $"{_ibkrReport.TaxYear}-12-31");

            if (yearEndEquitySummary != null)
            {
                decimal cashBalance = yearEndEquitySummary.Cash;
                if (cashBalance > 0)
                {
                    var cashSecurity = new EchSecurity
                    {
                        PositionId = positionId++,
                        Isin = "",
                        Country = "CH",
                        Currency = "CHF",
                        SecurityCategory = "OTHER",
                        SecurityName = "Cash Balance",
                        TaxValue = new EchTaxValue
                        {
                            ReferenceDate = _ibkrReport.EndDate,
                            Quantity = 1,
                            UnitPrice = cashBalance,
                            Value = cashBalance
                        }
                    };
                    depot.Securities.Add(cashSecurity);
                }
            }

            ValidateStatement(statement, _ibkrReport);

            return statement;
        }

        private void ValidateStatement(EchTaxStatement statement, IbkrFlexReport report)
        {
            var totalTaxValue = statement.GetTotalTaxValue();
            var ibkrTotalValue = report.GetTotalTaxValue();
            if (totalTaxValue != ibkrTotalValue)
            {
                _logger.LogError("Total tax value mismatch: eCH statement total {EchTotal}, IBKR report total {IbkrTotal}",
                    totalTaxValue, ibkrTotalValue);
                throw new Exception("Total tax value validation failed.");
            }
        }

        private EchSecurity BuildSecurity(IbkrSummaryPerPosition summary, int positionId)
        {
            // Only include NAV (TaxValue) if the position is from the year-end date
            DateTime reportDate = summary.OpenPosition?.ReportDate ?? default;
            DateTime reportEndDate = _ibkrReport.EndDate;
            bool isReportLastDatePosition = summary.OpenPosition == null || Math.Abs((reportDate - reportEndDate).TotalDays) == 0;

            // helper to calulate tax value
            // as the field positionValueInBase is not always available
            // fallback to manual calculation
            decimal taxValue = summary.OpenPosition != null ? summary.OpenPosition.GetPositionValue(_logger) : 0;

            var security = new EchSecurity
            {
                PositionId = positionId,
                Isin = summary.Isin ?? "",
                Country = DataHelper.MapCountry(summary.SecurityInfo.IssuerCountryCode),
                Currency = summary.SecurityInfo.Currency,
                SecurityCategory = DataHelper.MapSecurityCategory(summary.SecurityInfo.AssetCategory),
                SecurityName = summary.SecurityInfo.Description ?? summary.SecurityInfo.Symbol,
                TaxValue = isReportLastDatePosition ? new EchTaxValue
                {
                    ReferenceDate = reportEndDate,
                    Quantity = summary.OpenPosition?.Quantity ?? 0,
                    UnitPrice = summary.OpenPosition?.MarkPrice ?? 0,
                    Value = taxValue
                } : null
            };

            AddTradesAsStockMutations(security, summary.Trades);
            AddDividendsAsPayments(security, summary.DividendList, summary.WithholdingTaxList);

            return security;
        }

        private static void AddTradesAsStockMutations(EchSecurity security, List<IbkrTrade> trades)
        {
            var symbolTrades = trades
                .Where(t => t.Isin == security.Isin)
                .OrderBy(t => t.TradeDate);

            foreach (var trade in symbolTrades)
            {
                var tradeDate = trade.TradeDate;
                if (tradeDate == default) continue;

                var quantity = trade.Quantity;
                string name = quantity > 0 ? "Kauf" : "Verkauf"; // "Purchase" or "Sale"

                security.Stocks.Add(new EchStock
                {
                    ReferenceDate = tradeDate,
                    IsMutation = true,
                    Name = name,
                    Quantity = Math.Abs(quantity),
                    UnitPrice = trade.TradePrice,
                    Value = Math.Abs(trade.TradeMoney)
                });
            }
        }

        private void AddDividendsAsPayments(EchSecurity security, List<IbkrCashTransaction> dividends, List<IbkrCashTransaction> withholdingTax)
        {
            // make sure only dividends for this security are processed
            var symbolDividends = dividends
                .Where(d => d.Isin == security.Isin)
                .ToList();

            foreach (var dividend in symbolDividends)
            {
                DateTime settleDate = dividend.SettleDate;
                string actionID = dividend.ActionId;
                decimal netAmount = dividend.Amount;

                // Skip reversals
                if (netAmount <= 0) continue;

                decimal netAmountCHF = DataHelper.ConvertToCHF(dividend, _logger);
                decimal taxAmountCHF = FindMatchingWithholdingTax(security.Isin, settleDate, actionID, withholdingTax);
                decimal grossAmountCHF = netAmountCHF + taxAmountCHF;

                // Calculate additional withholding tax for USA (15% of gross amount for US securities)
                decimal additionalWithholdingTaxUSA = 0;
                if (security.Country == "US")
                {
                    // US tax treaty with Switzerland: 15% withholding on dividends
                    additionalWithholdingTaxUSA = grossAmountCHF * 0.15m;
                }

                // Determine GrossRevenueA vs GrossRevenueB based on security country
                // GrossRevenueA = Swiss securities (CH)
                // GrossRevenueB = Foreign securities (all others)
                decimal grossRevenueA = security.Country == "CH" ? grossAmountCHF : 0;
                decimal grossRevenueB = security.Country != "CH" ? grossAmountCHF : 0;

                security.Payments.Add(new EchPayment
                {
                    PaymentDate = dividend.SettleDate,
                    ExDate = dividend.ExDate,
                    Name = "Dividendenzahlung",
                    Quantity = 0,
                    Amount = grossAmountCHF,
                    GrossRevenueA = grossRevenueA,
                    GrossRevenueB = grossRevenueB,
                    WithHoldingTaxClaim = taxAmountCHF,
                    AdditionalWithHoldingTaxUSA = additionalWithholdingTaxUSA
                });
            }
        }

        private decimal FindMatchingWithholdingTax(string isin, DateTime settleDate, string actionID, List<IbkrCashTransaction> withholdingTax)
        {
            var taxTransactions = withholdingTax.Where(wt =>
                wt.Isin == isin &&
                wt.SettleDate == settleDate &&
                wt.ActionId == actionID);

            return Math.Abs(taxTransactions.Sum(wt => DataHelper.ConvertToCHF(wt, _logger)));
        }
    }
}
