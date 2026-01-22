using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Globalization;

namespace IbkrToEtax
{
    class Program
    {
        // Constants
        private const string LEVEL_DETAIL = "DETAIL";
        private const string LEVEL_SUMMARY = "SUMMARY";
        private const string CANTON = "ZH";

        static int Main(string[] args)
        {
            Console.WriteLine("ibkr-to-etax: eTax XML parser starting...");

            string defaultFile = "eTax.xml";
            string filePath = args.Length > 0 ? args[0] : defaultFile;

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"File not found: {filePath}");
                return 2;
            }

            try
            {
                Console.WriteLine($"Loading {filePath}...");
                var doc = XDocument.Load(filePath);

                // Extract account ID from XML
                var accountInfo = doc.Descendants("AccountInformation").FirstOrDefault();
                string accountId = (string?)accountInfo?.Attribute("accountId");

                // check that there is a account id in the xml
                if (string.IsNullOrEmpty(accountId))
                {
                    Console.WriteLine("Account ID not found in XML.");
                    return 4;
                }

                // Extract date range from FlexStatements
                var flexStatements = doc.Descendants("FlexStatement").ToList();
                var (periodFrom, periodTo, taxYear) = ExtractDateRange(flexStatements);

                // Parse and display IBKR data
                var (openPositions, trades, dividends, withholdingTax) = ParseIbkrData(doc, accountId);
                PrintDataLoadSummary(openPositions, trades, dividends, withholdingTax, accountInfo);

                // Build eCH tax statement
                var echStatement = BuildEchTaxStatement(openPositions, trades, dividends, withholdingTax, accountId, taxYear, periodFrom, periodTo);

                // Display financial summary
                PrintFinancialSummary(doc, dividends, withholdingTax, trades, accountId);

                // Generate output
                SaveAndDisplayOutput(echStatement);

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing XML: {ex.Message}");
                return 3;
            }
        }

        #region Data Parsing

        static (List<XElement> openPositions, List<XElement> trades, List<XElement> dividends,
                List<XElement> withholdingTax) ParseIbkrData(XDocument doc, string accountId)
        {
            var cashTransactions = doc.Descendants("CashTransaction")
                .Where(ct => (string)ct.Attribute("accountId") == accountId)
                .ToList();

            return (
                openPositions: doc.Descendants("OpenPosition")
                    .Where(op => (string)op.Attribute("levelOfDetail") == LEVEL_SUMMARY)
                    .ToList(),
                trades: doc.Descendants("Trade")
                    .Where(t => (string)t.Attribute("accountId") == accountId)
                    .ToList(),
                dividends: cashTransactions
                    .Where(ct => (string)ct.Attribute("type") == "Dividends")
                    .ToList(),
                withholdingTax: cashTransactions
                    .Where(ct => (string)ct.Attribute("type") == "Withholding Tax")
                    .ToList()
            );
        }

        static void PrintDataLoadSummary(List<XElement> openPositions, List<XElement> trades,
                                         List<XElement> dividends, List<XElement> withholdingTax,
                                         XElement? accountInfo)
        {
            Console.WriteLine($"Loaded IBKR data: {openPositions.Count} positions, {trades.Count} trades, " +
                            $"{dividends.Count} dividends, {withholdingTax.Count} withholding tax entries");

            if (accountInfo != null)
            {
                string accountId = (string)accountInfo.Attribute("accountId") ?? "Unknown";
                string accountName = (string)accountInfo.Attribute("name") ?? "Unknown";
                Console.WriteLine($"Account: {accountId} - {accountName}");
            }
        }

        #endregion

        #region Statement Building

        static EchTaxStatement BuildEchTaxStatement(List<XElement> openPositions, List<XElement> trades,
                                                     List<XElement> dividends, List<XElement> withholdingTax,
                                                     string accountId, int taxYear, DateTime periodFrom, DateTime periodTo)
        {
            var statement = new EchTaxStatement
            {
                Id = $"TS-{taxYear}",
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
                .GroupBy(p => (string)p.Attribute("symbol"))
                .ToDictionary(g => g.Key, g => g.First());

            int positionId = 1;
            foreach (var (symbol, position) in positionsBySymbol)
            {
                var security = BuildSecurity(position, symbol, positionId++, trades, dividends, withholdingTax, taxYear);
                depot.Securities.Add(security);
            }

            return statement;
        }

        static EchSecurity BuildSecurity(XElement position, string symbol, int positionId,
                                         List<XElement> trades, List<XElement> dividends,
                                         List<XElement> withholdingTax, int taxYear)
        {
            var security = new EchSecurity
            {
                PositionId = positionId,
                Isin = (string)position.Attribute("isin") ?? "",
                Country = MapCountry((string)position.Attribute("issuerCountryCode")),
                Currency = (string)position.Attribute("currency") ?? "",
                SecurityCategory = MapSecurityCategory((string)position.Attribute("assetCategory")),
                SecurityName = (string)position.Attribute("description") ?? symbol,
                TaxValue = new EchTaxValue
                {
                    ReferenceDate = new DateTime(taxYear, 12, 31),
                    Quantity = ParseDecimal((string)position.Attribute("position")),
                    UnitPrice = ParseDecimal((string)position.Attribute("markPrice")),
                    Value = ParseDecimal((string)position.Attribute("positionValueInBase"))
                }
            };

            AddTradesAsStockMutations(security, symbol, trades);
            AddDividendsAsPayments(security, symbol, dividends, withholdingTax);

            return security;
        }

        static void AddTradesAsStockMutations(EchSecurity security, string symbol, List<XElement> trades)
        {
            var symbolTrades = trades
                .Where(t => (string)t.Attribute("symbol") == symbol)
                .OrderBy(t => (string)t.Attribute("tradeDate"));

            foreach (var trade in symbolTrades)
            {
                security.Stocks.Add(new EchStock
                {
                    ReferenceDate = DateTime.Parse((string)trade.Attribute("tradeDate")),
                    IsMutation = true,
                    Quantity = ParseDecimal((string)trade.Attribute("quantity")),
                    UnitPrice = ParseDecimal((string)trade.Attribute("tradePrice")),
                    Value = Math.Abs(ParseDecimal((string)trade.Attribute("proceeds")))
                });
            }
        }

        static void AddDividendsAsPayments(EchSecurity security, string symbol,
                                           List<XElement> dividends, List<XElement> withholdingTax)
        {
            // Deduplicate by actionID to handle multiple FlexStatement periods
            var symbolDividends = dividends
                .Where(d => (string)d.Attribute("symbol") == symbol)
                .GroupBy(d => (string)d.Attribute("actionID"))
                .Select(g => g.First())
                .ToList();

            foreach (var dividend in symbolDividends)
            {
                string settleDate = (string)dividend.Attribute("settleDate") ?? "";
                decimal netAmount = ParseDecimal((string)dividend.Attribute("amount"));

                // Skip reversals
                if (netAmount <= 0) continue;

                decimal netAmountCHF = ConvertToCHF(dividend);
                decimal taxAmountCHF = FindMatchingWithholdingTax(symbol, settleDate, withholdingTax);
                decimal grossAmountCHF = netAmountCHF + taxAmountCHF;

                security.Payments.Add(new EchPayment
                {
                    PaymentDate = DateTime.Parse(settleDate),
                    ExDate = ParseNullableDate((string)dividend.Attribute("exDate")),
                    Quantity = 0,
                    Amount = grossAmountCHF,
                    GrossRevenueA = 0,  // Swiss securities
                    GrossRevenueB = grossAmountCHF,  // Foreign securities
                    WithHoldingTaxClaim = taxAmountCHF
                });
            }
        }

        static decimal FindMatchingWithholdingTax(string symbol, string settleDate, List<XElement> withholdingTax)
        {
            var taxTransaction = withholdingTax.FirstOrDefault(wt =>
                (string)wt.Attribute("symbol") == symbol &&
                (string)wt.Attribute("settleDate") == settleDate);

            return taxTransaction != null ? Math.Abs(ConvertToCHF(taxTransaction)) : 0;
        }

        #endregion

        #region Financial Summary

        static void PrintFinancialSummary(XDocument doc, List<XElement> dividends,
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

        static void PrintDividendsByCurrency(List<XElement> dividends, List<XElement> withholdingTax)
        {
            Console.WriteLine("Dividends + Withholding Tax per Currency:");

            var dividendsByCurrency = dividends
                .GroupBy(d => (string)d.Attribute("currency"))
                .Select(g => new
                {
                    Currency = g.Key,
                    TotalDividends = g.Sum(d => ParseDecimal((string)d.Attribute("amount")))
                }).ToList();

            var taxByCurrency = withholdingTax
                .GroupBy(wt => (string)wt.Attribute("currency"))
                .Select(g => new
                {
                    Currency = g.Key,
                    TotalTax = Math.Abs(g.Sum(wt => ParseDecimal((string)wt.Attribute("amount"))))
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
            decimal totalDividendsCHF = dividends.Sum(d => ConvertToCHF(d));
            decimal totalTaxCHF = withholdingTax.Sum(wt => Math.Abs(ConvertToCHF(wt)));
            Console.WriteLine($"Total Dividends in CHF: {totalDividendsCHF:F2}");
            Console.WriteLine($"Total Withholding Tax in CHF: {totalTaxCHF:F2}");
            Console.WriteLine();
        }

        static void PrintAccountValues(XDocument doc, string accountId)
        {
            var equitySummaries = doc.Descendants("EquitySummaryByReportDateInBase")
                .Where(e => (string)e.Attribute("accountId") == accountId)
                .OrderBy(e => (string)e.Attribute("reportDate"))
                .ToList();

            var startingSummary = equitySummaries.FirstOrDefault();
            var endingSummary = equitySummaries.LastOrDefault();

            if (startingSummary != null && endingSummary != null)
            {
                decimal startingValue = ParseDecimal((string)startingSummary.Attribute("total"));
                decimal endingValue = ParseDecimal((string)endingSummary.Attribute("total"));

                Console.WriteLine("Account Value Summary:");
                Console.WriteLine($"  Starting Value: {startingValue:F2} CHF");
                Console.WriteLine($"  Ending Value: {endingValue:F2} CHF");
                Console.WriteLine();
            }
        }

        static void PrintOtherMetrics(XDocument doc, List<XElement> dividends,
                                      List<XElement> withholdingTax, List<XElement> trades,
                                      string accountId)
        {
            // Mark-to-Market
            var perfSummary = doc.Descendants("FIFOPerformanceSummaryUnderlying")
                .FirstOrDefault(p => (string)p.Attribute("description") == "Total (All Assets)");
            if (perfSummary != null)
            {
                decimal mtmPnl = ParseDecimal((string)perfSummary.Attribute("totalUnrealizedPnl"));
                Console.WriteLine($"Mark-to-Market P&L: {mtmPnl:F2} CHF");
            }

            // Deposits & Withdrawals
            var depositsWithdrawals = doc.Descendants("CashTransaction")
                .Where(ct => (string)ct.Attribute("type") == "Deposits/Withdrawals" &&
                             (string)ct.Attribute("accountId") == accountId)
                .Sum(ct => ParseDecimal((string)ct.Attribute("amount")));
            Console.WriteLine($"Deposits & Withdrawals: {depositsWithdrawals:F2} CHF");

            // Dividends & Tax
            decimal totalDividendsCHF = dividends.Sum(d => ConvertToCHF(d));
            decimal totalTaxCHF = withholdingTax.Sum(wt => Math.Abs(ConvertToCHF(wt)));
            Console.WriteLine($"Dividends: {totalDividendsCHF:F2} CHF");
            Console.WriteLine($"Withholding Tax: {totalTaxCHF:F2} CHF");

            // Change in Dividend Accrual
            var equitySummaries = doc.Descendants("EquitySummaryByReportDateInBase")
                .Where(e => (string)e.Attribute("accountId") == accountId)
                .OrderBy(e => (string)e.Attribute("reportDate"))
                .ToList();

            if (equitySummaries.Count >= 2)
            {
                decimal startingAccruals = ParseDecimal((string)equitySummaries.First().Attribute("dividendAccruals"));
                decimal endingAccruals = ParseDecimal((string)equitySummaries.Last().Attribute("dividendAccruals"));
                Console.WriteLine($"Change in Dividend Accrual: {(endingAccruals - startingAccruals):F2} CHF");
            }

            // Commissions
            var totalCommissions = trades
                .Where(t => (string)t.Attribute("accountId") == accountId &&
                            (string)t.Attribute("assetCategory") != "CASH")
                .Sum(t =>
                {
                    decimal ibComm = ParseDecimal((string)t.Attribute("ibCommission"));
                    decimal fx = ParseDecimal((string)t.Attribute("fxRateToBase"));
                    return Math.Abs(ibComm * fx);
                });
            Console.WriteLine($"Commissions: {totalCommissions:F2} CHF");

            // Sales Tax
            var transactionTaxes = doc.Descendants("TransactionTax")
                .Where(tt => (string)tt.Attribute("accountId") == accountId)
                .Sum(tt => ParseDecimal((string)tt.Attribute("taxInBase")));
            Console.WriteLine($"Sales Tax: {transactionTaxes:F2} CHF");

            // FX Translations
            if (perfSummary != null)
            {
                decimal fxPnl = ParseDecimal((string)perfSummary.Attribute("totalFxPnl"));
                Console.WriteLine($"Other FX Translations: {fxPnl:F2} CHF");
            }
        }

        #endregion

        #region Output Generation

        static void SaveAndDisplayOutput(EchTaxStatement statement)
        {
            var echXml = GenerateEchXml(statement);
            string outputPath = "eCH-0196-output.xml";
            echXml.Save(outputPath);

            var depot = statement.Depots.First();
            Console.WriteLine();
            Console.WriteLine($"✓ Generated eCH-0196 tax statement: {outputPath}");
            Console.WriteLine($"  - {depot.Securities.Count} securities");
            Console.WriteLine($"  - {depot.Securities.Sum(s => s.Stocks.Count)} stock mutations");
            Console.WriteLine($"  - {depot.Securities.Sum(s => s.Payments.Count)} dividend payments");
        }

        #endregion

        #region Utility Methods

        static decimal ParseDecimal(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
        }

        static DateTime? ParseNullableDate(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            return DateTime.TryParse(value, out var result) ? result : null;
        }

        static string MapCountry(string ibkrCountry)
        {
            // IBKR uses country codes, eCH uses ISO2
            return ibkrCountry switch
            {
                "US" => "US",
                "CH" => "CH",
                _ => ibkrCountry ?? "CH"
            };
        }

        static string MapSecurityCategory(string assetCategory)
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

        static decimal ConvertToCHF(XElement transaction)
        {
            decimal amount = ParseDecimal((string)transaction.Attribute("amount"));
            decimal fxRate = ParseDecimal((string)transaction.Attribute("fxRateToBase"));
            return amount * fxRate;
        }

        static XDocument GenerateEchXml(EchTaxStatement statement)
        {
            XNamespace ns = "http://www.ech.ch/xmlns/eCH-0196/2";

            var root = new XElement(ns + "taxStatement",
                new XAttribute("id", statement.Id),
                new XAttribute("creationDate", statement.CreationDate.ToString("yyyy-MM-ddTHH:mm:ss")),
                new XAttribute("taxPeriod", statement.TaxPeriod),
                new XAttribute("periodFrom", statement.PeriodFrom.ToString("yyyy-MM-dd")),
                new XAttribute("periodTo", statement.PeriodTo.ToString("yyyy-MM-dd")),
                new XAttribute("canton", statement.Canton),
                new XAttribute("totalTaxValue", statement.Depots.Sum(d => d.Securities.Sum(s => s.TaxValue?.Value ?? 0))),
                new XAttribute("totalGrossRevenueA", statement.Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.GrossRevenueA)))),
                new XAttribute("totalGrossRevenueB", statement.Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.GrossRevenueB)))),
                new XAttribute("totalWithHoldingTaxClaim", statement.Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.WithHoldingTaxClaim)))),
                new XAttribute("minorVersion", 0),

                new XElement(ns + "institution",
                    new XAttribute("name", statement.Institution)),

                new XElement(ns + "client",
                    new XAttribute("clientNumber", statement.ClientNumber)),

                new XElement(ns + "listOfSecurities",
                    new XAttribute("totalTaxValue", statement.Depots.Sum(d => d.Securities.Sum(s => s.TaxValue?.Value ?? 0))),
                    new XAttribute("totalGrossRevenueA", statement.Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.GrossRevenueA)))),
                    new XAttribute("totalGrossRevenueB", statement.Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.GrossRevenueB)))),
                    new XAttribute("totalWithHoldingTaxClaim", statement.Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.WithHoldingTaxClaim)))),
                    new XAttribute("totalLumpSumTaxCredit", 0),
                    new XAttribute("totalNonRecoverableTax", 0),
                    new XAttribute("totalAdditionalWithHoldingTaxUSA", 0),
                    new XAttribute("totalGrossRevenueIUP", 0),
                    new XAttribute("totalGrossRevenueConversion", 0),

                    from depot in statement.Depots
                    select new XElement(ns + "depot",
                        new XAttribute("depotNumber", depot.DepotNumber),

                        from sec in depot.Securities
                        select new XElement(ns + "security",
                            new XAttribute("positionId", sec.PositionId),
                            string.IsNullOrEmpty(sec.Isin) ? null : new XAttribute("isin", sec.Isin),
                            new XAttribute("country", sec.Country),
                            new XAttribute("currency", sec.Currency),
                            new XAttribute("quotationType", "PIECE"),
                            new XAttribute("securityCategory", sec.SecurityCategory),
                            new XAttribute("securityName", sec.SecurityName),

                            sec.TaxValue == null ? null : new XElement(ns + "taxValue",
                                new XAttribute("referenceDate", sec.TaxValue.ReferenceDate.ToString("yyyy-MM-dd")),
                                new XAttribute("quotationType", "PIECE"),
                                new XAttribute("quantity", sec.TaxValue.Quantity),
                                new XAttribute("balanceCurrency", sec.Currency),
                                new XAttribute("unitPrice", sec.TaxValue.UnitPrice),
                                new XAttribute("value", sec.TaxValue.Value)),

                            from payment in sec.Payments
                            select new XElement(ns + "payment",
                                new XAttribute("paymentDate", payment.PaymentDate.ToString("yyyy-MM-dd")),
                                payment.ExDate.HasValue ? new XAttribute("exDate", payment.ExDate.Value.ToString("yyyy-MM-dd")) : null,
                                new XAttribute("quotationType", "PIECE"),
                                new XAttribute("quantity", payment.Quantity),
                                new XAttribute("amountCurrency", sec.Currency),
                                new XAttribute("amount", payment.Amount),
                                new XAttribute("grossRevenueA", payment.GrossRevenueA),
                                new XAttribute("grossRevenueB", payment.GrossRevenueB),
                                new XAttribute("withHoldingTaxClaim", payment.WithHoldingTaxClaim)),

                            from stock in sec.Stocks
                            select new XElement(ns + "stock",
                                new XAttribute("referenceDate", stock.ReferenceDate.ToString("yyyy-MM-dd")),
                                new XAttribute("mutation", stock.IsMutation ? "true" : "false"),
                                new XAttribute("quotationType", "PIECE"),
                                new XAttribute("quantity", stock.Quantity),
                                new XAttribute("balanceCurrency", sec.Currency),
                                new XAttribute("unitPrice", stock.UnitPrice),
                                new XAttribute("value", stock.Value))
                        )
                    )
                )
            );

            return new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                root
            );
        }

        #endregion

    }
}