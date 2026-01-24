using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace IbkrToEtax
{
    public static class EchStatementBuilder
    {
        public static string GenerateEchId(string accountId, DateTime date, int sequenceNumber = 1)
        {
            // eCH-0196 Section 2.1: ID format
            // CH (country) + 00000 (org) + 01 (page) + {accountId 14 chars} + {YYYYMMDD} + {seq 2 digits}
            string countryCode = "CH";
            string organizationId = "00000"; // Placeholder - should be clearing number or UID
            string pageNumber = "01";
            string customerNumber = accountId.PadLeft(14, '0');
            if (customerNumber.Length > 14)
                customerNumber = customerNumber.Substring(customerNumber.Length - 14);
            string dateStr = date.ToString("yyyyMMdd");
            string seqStr = sequenceNumber.ToString("D2");
            
            // Must start with alphanumeric (xs:ID requirement)
            return $"{countryCode}{organizationId}{pageNumber}{customerNumber}{dateStr}{seqStr}";
        }

        public static EchTaxStatement BuildEchTaxStatement(XDocument doc, List<XElement> openPositions, List<XElement> trades,
                                                     List<XElement> dividends, List<XElement> withholdingTax,
                                                     string accountId, int taxYear, DateTime periodFrom, DateTime periodTo, string canton = "ZH")
        {
            var statement = new EchTaxStatement
            {
                Id = GenerateEchId(accountId, periodTo),
                TaxPeriod = taxYear,
                PeriodFrom = periodFrom,
                PeriodTo = periodTo,
                Canton = canton,
                ClientNumber = accountId
            };

            var depot = new EchSecurityDepot { DepotNumber = accountId };
            statement.Depots.Add(depot);

            // Process each position - use the latest (year-end) position for each symbol
            var positionsBySymbol = openPositions
                .Select(p => new { Symbol = (string?)p.Attribute("symbol"), ReportDate = (string?)p.Attribute("reportDate"), Position = p })
                .Where(x => !string.IsNullOrEmpty(x.Symbol))
                .GroupBy(x => x.Symbol!)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ReportDate).First().Position);

            int positionId = 1;
            foreach (var (symbol, position) in positionsBySymbol)
            {
                var security = BuildSecurity(position, symbol, positionId++, trades, dividends, withholdingTax, taxYear);
                depot.Securities.Add(security);
            }

            // Add cash position from year-end EquitySummary
            var yearEndEquitySummary = doc.Descendants("EquitySummaryByReportDateInBase")
                .Where(e => (string?)e.Attribute("accountId") == accountId && 
                           (string?)e.Attribute("reportDate") == $"{taxYear}-12-31")
                .FirstOrDefault();

            if (yearEndEquitySummary != null)
            {
                decimal cashBalance = DataHelper.ParseDecimal((string?)yearEndEquitySummary.Attribute("cash"));
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
                            ReferenceDate = new DateTime(taxYear, 12, 31),
                            Quantity = 1,
                            UnitPrice = cashBalance,
                            Value = cashBalance
                        }
                    };
                    depot.Securities.Add(cashSecurity);
                }
            }

            return statement;
        }

        private static EchSecurity BuildSecurity(XElement position, string symbol, int positionId,
                                         List<XElement> trades, List<XElement> dividends,
                                         List<XElement> withholdingTax, int taxYear)
        {
            // Only include NAV (TaxValue) if the position is from the year-end date
            string reportDate = (string?)position.Attribute("reportDate") ?? "";
            DateTime yearEndDate = new DateTime(taxYear, 12, 31);
            bool isYearEndPosition = reportDate == yearEndDate.ToString("yyyy-MM-dd");

            var security = new EchSecurity
            {
                PositionId = positionId,
                Isin = (string?)position.Attribute("isin") ?? "",
                Country = DataHelper.MapCountry((string?)position.Attribute("issuerCountryCode") ?? "CH"),
                Currency = (string?)position.Attribute("currency") ?? "",
                SecurityCategory = DataHelper.MapSecurityCategory((string?)position.Attribute("assetCategory") ?? "STK"),
                SecurityName = (string?)position.Attribute("description") ?? symbol,
                TaxValue = isYearEndPosition ? new EchTaxValue
                {
                    ReferenceDate = yearEndDate,
                    Quantity = DataHelper.ParseDecimal((string?)position.Attribute("position")),
                    UnitPrice = DataHelper.ParseDecimal((string?)position.Attribute("markPrice")),
                    Value = DataHelper.ParseDecimal((string?)position.Attribute("positionValueInBase"))
                } : null
            };

            AddTradesAsStockMutations(security, symbol, trades);
            AddDividendsAsPayments(security, symbol, dividends, withholdingTax);

            return security;
        }

        private static void AddTradesAsStockMutations(EchSecurity security, string symbol, List<XElement> trades)
        {
            var symbolTrades = trades
                .Where(t => (string?)t.Attribute("symbol") == symbol)
                .OrderBy(t => (string?)t.Attribute("tradeDate") ?? "");

            foreach (var trade in symbolTrades)
            {
                var tradeDateStr = (string?)trade.Attribute("tradeDate");
                if (string.IsNullOrEmpty(tradeDateStr)) continue;

                security.Stocks.Add(new EchStock
                {
                    ReferenceDate = DateTime.Parse(tradeDateStr),
                    IsMutation = true,
                    Quantity = DataHelper.ParseDecimal((string?)trade.Attribute("quantity")),
                    UnitPrice = DataHelper.ParseDecimal((string?)trade.Attribute("tradePrice")),
                    Value = Math.Abs(DataHelper.ParseDecimal((string?)trade.Attribute("proceeds")))
                });
            }
        }

        private static void AddDividendsAsPayments(EchSecurity security, string symbol,
                                           List<XElement> dividends, List<XElement> withholdingTax)
        {
            // Deduplicate by actionID to handle multiple FlexStatement periods
            var symbolDividends = dividends
                .Where(d => (string?)d.Attribute("symbol") == symbol)
                .GroupBy(d => (string?)d.Attribute("actionID") ?? "")
                .Select(g => g.First())
                .ToList();

            foreach (var dividend in symbolDividends)
            {
                string settleDate = (string?)dividend.Attribute("settleDate") ?? "";
                string actionID = (string?)dividend.Attribute("actionID") ?? "";
                decimal netAmount = DataHelper.ParseDecimal((string?)dividend.Attribute("amount"));

                // Skip reversals
                if (netAmount <= 0) continue;

                decimal netAmountCHF = DataHelper.ConvertToCHF(dividend);
                decimal taxAmountCHF = FindMatchingWithholdingTax(symbol, settleDate, actionID, withholdingTax);
                decimal grossAmountCHF = netAmountCHF + taxAmountCHF;

                // Calculate additional withholding tax for USA (15% of gross amount for US securities)
                decimal additionalWithholdingTaxUSA = 0;
                if (security.Country == "US")
                {
                    // US tax treaty with Switzerland: 15% withholding on dividends
                    additionalWithholdingTaxUSA = grossAmountCHF * 0.15m;
                }

                security.Payments.Add(new EchPayment
                {
                    PaymentDate = DateTime.Parse(settleDate),
                    ExDate = DataHelper.ParseNullableDate((string?)dividend.Attribute("exDate")),
                    Quantity = 0,
                    Amount = grossAmountCHF,
                    GrossRevenueA = 0,  // Swiss securities
                    GrossRevenueB = grossAmountCHF,  // Foreign securities
                    WithHoldingTaxClaim = taxAmountCHF,
                    AdditionalWithHoldingTaxUSA = additionalWithholdingTaxUSA
                });
            }
        }

        private static decimal FindMatchingWithholdingTax(string symbol, string settleDate, string actionID, List<XElement> withholdingTax)
        {
            var taxTransactions = withholdingTax.Where(wt =>
                (string?)wt.Attribute("symbol") == symbol &&
                (string?)wt.Attribute("settleDate") == settleDate &&
                (string?)wt.Attribute("actionID") == actionID);

            return Math.Abs(taxTransactions.Sum(wt => DataHelper.ConvertToCHF(wt)));
        }
    }
}
