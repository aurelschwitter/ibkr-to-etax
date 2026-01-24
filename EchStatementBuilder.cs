using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace IbkrToEtax
{
    public static class EchStatementBuilder
    {
        private const string CANTON = "ZH";

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

        public static EchTaxStatement BuildEchTaxStatement(List<XElement> openPositions, List<XElement> trades,
                                                     List<XElement> dividends, List<XElement> withholdingTax,
                                                     string accountId, int taxYear, DateTime periodFrom, DateTime periodTo)
        {
            var statement = new EchTaxStatement
            {
                Id = GenerateEchId(accountId, periodTo),
                TaxPeriod = taxYear,
                PeriodFrom = periodFrom,
                PeriodTo = periodTo,
                Canton = CANTON,
                ClientNumber = accountId
            };

            var depot = new EchSecurityDepot { DepotNumber = accountId };
            statement.Depots.Add(depot);

            // Process each position
            var positionsBySymbol = openPositions
                .Select(p => new { Symbol = (string?)p.Attribute("symbol"), Position = p })
                .Where(x => !string.IsNullOrEmpty(x.Symbol))
                .GroupBy(x => x.Symbol!)
                .ToDictionary(g => g.Key, g => g.First().Position);

            int positionId = 1;
            foreach (var (symbol, position) in positionsBySymbol)
            {
                var security = BuildSecurity(position, symbol, positionId++, trades, dividends, withholdingTax, taxYear);
                depot.Securities.Add(security);
            }

            return statement;
        }

        private static EchSecurity BuildSecurity(XElement position, string symbol, int positionId,
                                         List<XElement> trades, List<XElement> dividends,
                                         List<XElement> withholdingTax, int taxYear)
        {
            var security = new EchSecurity
            {
                PositionId = positionId,
                Isin = (string?)position.Attribute("isin") ?? "",
                Country = DataHelper.MapCountry((string?)position.Attribute("issuerCountryCode") ?? "CH"),
                Currency = (string?)position.Attribute("currency") ?? "",
                SecurityCategory = DataHelper.MapSecurityCategory((string?)position.Attribute("assetCategory") ?? "STK"),
                SecurityName = (string?)position.Attribute("description") ?? symbol,
                TaxValue = new EchTaxValue
                {
                    ReferenceDate = new DateTime(taxYear, 12, 31),
                    Quantity = DataHelper.ParseDecimal((string?)position.Attribute("position")),
                    UnitPrice = DataHelper.ParseDecimal((string?)position.Attribute("markPrice")),
                    Value = DataHelper.ParseDecimal((string?)position.Attribute("positionValueInBase"))
                }
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
                decimal netAmount = DataHelper.ParseDecimal((string?)dividend.Attribute("amount"));

                // Skip reversals
                if (netAmount <= 0) continue;

                decimal netAmountCHF = DataHelper.ConvertToCHF(dividend);
                decimal taxAmountCHF = FindMatchingWithholdingTax(symbol, settleDate, withholdingTax);
                decimal grossAmountCHF = netAmountCHF + taxAmountCHF;

                security.Payments.Add(new EchPayment
                {
                    PaymentDate = DateTime.Parse(settleDate),
                    ExDate = DataHelper.ParseNullableDate((string?)dividend.Attribute("exDate")),
                    Quantity = 0,
                    Amount = grossAmountCHF,
                    GrossRevenueA = 0,  // Swiss securities
                    GrossRevenueB = grossAmountCHF,  // Foreign securities
                    WithHoldingTaxClaim = taxAmountCHF
                });
            }
        }

        private static decimal FindMatchingWithholdingTax(string symbol, string settleDate, List<XElement> withholdingTax)
        {
            var taxTransaction = withholdingTax.FirstOrDefault(wt =>
                (string?)wt.Attribute("symbol") == symbol &&
                (string?)wt.Attribute("settleDate") == settleDate);

            return taxTransaction != null ? Math.Abs(DataHelper.ConvertToCHF(taxTransaction)) : 0;
        }
    }
}
