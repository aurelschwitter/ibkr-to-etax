using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Xml.Linq;
using System.Globalization;

namespace IbkrToEtax
{
    // eCH-0196 Model Classes
    class EchTaxStatement
    {
        public string Id { get; set; } = "TS-2025";
        public DateTime CreationDate { get; set; } = DateTime.Now;
        public int TaxPeriod { get; set; } = 2025;
        public DateTime PeriodFrom { get; set; } = new DateTime(2025, 1, 1);
        public DateTime PeriodTo { get; set; } = new DateTime(2025, 12, 31);
        public string Canton { get; set; } = "ZH";
        public string Institution { get; set; } = "Interactive Brokers";
        public string ClientNumber { get; set; } = "U14798214";
        public List<EchSecurityDepot> Depots { get; set; } = new();
    }

    class EchSecurityDepot
    {
        public string DepotNumber { get; set; }
        public List<EchSecurity> Securities { get; set; } = new();
    }

    class EchSecurity
    {
        public int PositionId { get; set; }
        public string Isin { get; set; }
        public string Country { get; set; }
        public string Currency { get; set; }
        public string SecurityCategory { get; set; }
        public string SecurityName { get; set; }
        public EchTaxValue TaxValue { get; set; }
        public List<EchPayment> Payments { get; set; } = new();
        public List<EchStock> Stocks { get; set; } = new();
    }

    class EchTaxValue
    {
        public DateTime ReferenceDate { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Value { get; set; }
    }

    class EchPayment
    {
        public DateTime PaymentDate { get; set; }
        public DateTime? ExDate { get; set; }
        public decimal Quantity { get; set; }
        public decimal Amount { get; set; }
        public decimal GrossRevenueA { get; set; }
        public decimal GrossRevenueB { get; set; }
        public decimal WithHoldingTaxClaim { get; set; }
    }

    class EchStock
    {
        public DateTime ReferenceDate { get; set; }
        public bool IsMutation { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Value { get; set; }
    }

    class Program
    {
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
                
                // Parse IBKR data - collect all data from all FlexStatement periods
                // Filter to DETAIL level only to avoid SUMMARY duplicates
                var openPositions = doc.Descendants("OpenPosition")
                    .Where(op => (string)op.Attribute("levelOfDetail") == "SUMMARY")
                    .ToList();
                var trades = doc.Descendants("Trade")
                    .Where(t => (string)t.Attribute("accountId") == "U14798214")
                    .ToList();
                var cashTransactions = doc.Descendants("CashTransaction")
                    .Where(ct => (string)ct.Attribute("accountId") == "U14798214") // DETAIL level only
                    .ToList();
                var dividends = cashTransactions.Where(ct => (string)ct.Attribute("type") == "Dividends").ToList();
                var withholdingTax = cashTransactions.Where(ct => (string)ct.Attribute("type") == "Withholding Tax").ToList();
                var accountInfo = doc.Descendants("AccountInformation").FirstOrDefault();

                Console.WriteLine($"Loaded IBKR data: {openPositions.Count} positions, {trades.Count} trades, {dividends.Count} dividends, {withholdingTax.Count} withholding tax entries");
                
                if (accountInfo != null)
                {
                    string accountId = (string)accountInfo.Attribute("accountId") ?? "Unknown";
                    string accountName = (string)accountInfo.Attribute("name") ?? "Unknown";
                    Console.WriteLine($"Account: {accountId} - {accountName}");
                }

                // Create eCH tax statement
                var echStatement = new EchTaxStatement
                {
                    TaxPeriod = 2025,
                    PeriodFrom = new DateTime(2025, 1, 1),
                    PeriodTo = new DateTime(2025, 12, 31)
                };

                var depot = new EchSecurityDepot { DepotNumber = "U14798214" };
                echStatement.Depots.Add(depot);

                // Group positions by symbol
                var positionsBySymbol = openPositions
                    .GroupBy(p => (string)p.Attribute("symbol"))
                    .ToDictionary(g => g.Key, g => g.First());

                int positionId = 1;
                foreach (var posKvp in positionsBySymbol)
                {
                    var pos = posKvp.Value;
                    string symbol = posKvp.Key;
                    
                    var security = new EchSecurity
                    {
                        PositionId = positionId++,
                        Isin = (string)pos.Attribute("isin") ?? "",
                        Country = MapCountry((string)pos.Attribute("issuerCountryCode")),
                        Currency = (string)pos.Attribute("currency") ?? "",
                        SecurityCategory = MapSecurityCategory((string)pos.Attribute("assetCategory")),
                        SecurityName = (string)pos.Attribute("description") ?? symbol,
                        TaxValue = new EchTaxValue
                        {
                            ReferenceDate = new DateTime(2025, 12, 31),
                            Quantity = ParseDecimal((string)pos.Attribute("position")),
                            UnitPrice = ParseDecimal((string)pos.Attribute("markPrice")),
                            Value = ParseDecimal((string)pos.Attribute("positionValueInBase"))
                        }
                    };

                    // Add trades for this symbol as stock mutations
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

                    // Add dividends for this symbol as payments from CashTransactions
                    // Deduplicate by actionID to get unique dividend events (handles multiple FlexStatement periods)
                    var symbolDividends = dividends
                        .Where(d => (string)d.Attribute("symbol") == symbol)
                        .GroupBy(d => (string)d.Attribute("actionID"))
                        .Select(g => g.First())
                        .ToList();

                    foreach (var div in symbolDividends)
                    {
                        string settleDate = (string)div.Attribute("settleDate") ?? "";
                        decimal netAmount = ParseDecimal((string)div.Attribute("amount"));
                        decimal fxRate = ParseDecimal((string)div.Attribute("fxRateToBase"));
                        decimal netAmountCHF = netAmount * fxRate;
                        
                        // Find matching withholding tax transaction
                        var taxTransaction = withholdingTax
                            .FirstOrDefault(wt => 
                                (string)wt.Attribute("symbol") == symbol &&
                                (string)wt.Attribute("settleDate") == settleDate);
                        
                        decimal taxAmountCHF = 0;
                        if (taxTransaction != null)
                        {
                            decimal taxAmount = ParseDecimal((string)taxTransaction.Attribute("amount"));
                            decimal taxFxRate = ParseDecimal((string)taxTransaction.Attribute("fxRateToBase"));
                            taxAmountCHF = Math.Abs(taxAmount * taxFxRate);
                        }
                        
                        decimal grossAmountCHF = netAmountCHF + taxAmountCHF;
                        
                        // Only process positive amounts (actual payments, not reversals)
                        if (netAmount > 0)
                        {
                            security.Payments.Add(new EchPayment
                            {
                                PaymentDate = DateTime.Parse(settleDate),
                                ExDate = ParseNullableDate((string)div.Attribute("exDate")),
                                Quantity = 0, // Quantity not in CashTransaction
                                Amount = grossAmountCHF, // Gross amount in CHF
                                GrossRevenueA = 0, // Swiss securities with withholding tax
                                GrossRevenueB = grossAmountCHF, // Foreign securities  
                                WithHoldingTaxClaim = taxAmountCHF // Tax in CHF
                            });
                        }
                    }

                    depot.Securities.Add(security);
                }

                // Calculate summary statistics
                Console.WriteLine();
                Console.WriteLine("=== FINANCIAL SUMMARY ===");
                Console.WriteLine();

                // 1. Dividends and Withholding Tax by Currency
                Console.WriteLine("Dividends + Withholding Tax per Currency:");
                var dividendsByCurrency = dividends
                    .GroupBy(d => (string)d.Attribute("currency"))
                    .Select(g => new {
                        Currency = g.Key,
                        TotalDividends = g.Sum(d => ParseDecimal((string)d.Attribute("amount"))),
                        Count = g.Count()
                    }).ToList();

                var taxByCurrency = withholdingTax
                    .GroupBy(wt => (string)wt.Attribute("currency"))
                    .Select(g => new {
                        Currency = g.Key,
                        TotalTax = Math.Abs(g.Sum(wt => ParseDecimal((string)wt.Attribute("amount")))),
                        Count = g.Count()
                    }).ToList();

                foreach (var curr in dividendsByCurrency)
                {
                    var tax = taxByCurrency.FirstOrDefault(t => t.Currency == curr.Currency);
                    decimal totalTax = tax?.TotalTax ?? 0;
                    Console.WriteLine($"  {curr.Currency}: Dividends: {curr.TotalDividends:F2}, Tax: {totalTax:F2}, Gross: {(curr.TotalDividends + totalTax):F2}");
                }
                Console.WriteLine();

                // 2. CHF Totals (convert all amounts to CHF using fxRateToBase)
                decimal totalDividendsCHF = dividends.Sum(d => {
                    decimal amount = ParseDecimal((string)d.Attribute("amount"));
                    decimal fxRate = ParseDecimal((string)d.Attribute("fxRateToBase"));
                    return amount * fxRate;
                });
                decimal totalTaxCHF = withholdingTax.Sum(wt => {
                    decimal amount = ParseDecimal((string)wt.Attribute("amount"));
                    decimal fxRate = ParseDecimal((string)wt.Attribute("fxRateToBase"));
                    return Math.Abs(amount * fxRate);
                });
                Console.WriteLine($"Total Dividends in CHF: {totalDividendsCHF:F2}");
                Console.WriteLine($"Total Withholding Tax in CHF: {totalTaxCHF:F2}");
                Console.WriteLine();

                // 3. Account Value Summary
                // Get first and last equity summaries
                var equitySummaries = doc.Descendants("EquitySummaryByReportDateInBase")
                    .Where(e => (string)e.Attribute("accountId") == "U14798214")
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

                // 4. Performance and Other Metrics
                var perfSummaries = doc.Descendants("FIFOPerformanceSummaryUnderlying")
                    .Where(p => (string)p.Attribute("description") == "Total (All Assets)")
                    .ToList();

                var lastPerfSummary = perfSummaries.LastOrDefault();
                decimal mtmPnl = 0;
                if (lastPerfSummary != null)
                {
                    mtmPnl = ParseDecimal((string)lastPerfSummary.Attribute("totalUnrealizedPnl"));
                    Console.WriteLine($"Mark-to-Market P&L: {mtmPnl:F2} CHF");
                }

                // 5. Deposits & Withdrawals
                var depositsWithdrawals = doc.Descendants("CashTransaction")
                    .Where(ct => (string)ct.Attribute("type") == "Deposits/Withdrawals" && 
                                 (string)ct.Attribute("accountId") == "U14798214")
                    .Sum(ct => ParseDecimal((string)ct.Attribute("amount")));
                Console.WriteLine($"Deposits & Withdrawals: {depositsWithdrawals:F2} CHF");

                // 6. Dividends summary (already calculated above)
                Console.WriteLine($"Dividends: {totalDividendsCHF:F2} CHF");

                // 7. Withholding Tax (already calculated above)
                Console.WriteLine($"Withholding Tax: {totalTaxCHF:F2} CHF");

                // 8. Change in Dividend Accrual
                decimal changeInAccruals = 0;
                if (startingSummary != null && endingSummary != null)
                {
                    decimal startingAccruals = ParseDecimal((string)startingSummary.Attribute("dividendAccruals"));
                    decimal endingAccruals = ParseDecimal((string)endingSummary.Attribute("dividendAccruals"));
                    changeInAccruals = endingAccruals - startingAccruals;
                    Console.WriteLine($"Change in Dividend Accrual: {changeInAccruals:F2} CHF");
                }

                // 9. Commissions
                var totalCommissions = trades
                    .Where(t => (string)t.Attribute("accountId") == "U14798214" && 
                                (string)t.Attribute("assetCategory") != "CASH") // Exclude FX trades
                    .Sum(t => {
                        decimal ibComm = ParseDecimal((string)t.Attribute("ibCommission"));
                        decimal fx = ParseDecimal((string)t.Attribute("fxRateToBase"));
                        return Math.Abs(ibComm * fx);
                    });
                Console.WriteLine($"Commissions: {totalCommissions:F2} CHF");

                // 10. Sales Tax (from TransactionTaxes)
                var transactionTaxes = doc.Descendants("TransactionTax")
                    .Where(tt => (string)tt.Attribute("accountId") == "U14798214")
                    .Sum(tt => ParseDecimal((string)tt.Attribute("taxInBase")));
                Console.WriteLine($"Sales Tax: {transactionTaxes:F2} CHF");

                // 11. Other FX Translations
                var fxTrades = trades
                    .Where(t => (string)t.Attribute("assetCategory") == "CASH" &&
                                (string)t.Attribute("accountId") == "U14798214")
                    .ToList();
                decimal fxPnl = lastPerfSummary != null ? 
                    ParseDecimal((string)lastPerfSummary.Attribute("totalFxPnl")) : 0;
                Console.WriteLine($"Other FX Translations: {fxPnl:F2} CHF");

                Console.WriteLine();
                Console.WriteLine("===========================");
                Console.WriteLine();
                Console.WriteLine("=== COMPARISON TO OFFICIAL VALUES ===");
                Console.WriteLine();
                Console.WriteLine($"{"Item",-30} {"Official",15} {"Calculated",15} {"Difference",15}");
                Console.WriteLine(new string('-', 75));
                
                decimal officialStarting = 57759.20m;
                decimal officialMTM = 7657.14m;
                decimal officialDeposits = 63800.00m;
                decimal officialDividends = 1846.61m;
                decimal officialWithholding = -344.58m;
                decimal officialAccruals = 6.62m;
                decimal officialCommissions = -82.24m;
                decimal officialSalesTax = -0.11m;
                decimal officialFX = -6.62m;
                decimal officialEnding = 130636.02m;

                decimal calcStarting = startingSummary != null ? ParseDecimal((string)startingSummary.Attribute("total")) : 0;
                decimal calcMTM = mtmPnl;
                decimal calcDeposits = depositsWithdrawals;
                decimal calcDividends = totalDividendsCHF;
                decimal calcWithholding = -totalTaxCHF; // Should be negative
                decimal calcAccruals = startingSummary != null && endingSummary != null ? 
                    ParseDecimal((string)endingSummary.Attribute("dividendAccruals")) - 
                    ParseDecimal((string)startingSummary.Attribute("dividendAccruals")) : 0;
                decimal calcCommissions = -totalCommissions; // Should be negative
                decimal calcSalesTax = -transactionTaxes; // Should be negative
                decimal calcFX = fxPnl;
                decimal calcEnding = endingSummary != null ? ParseDecimal((string)endingSummary.Attribute("total")) : 0;

                Console.WriteLine($"{"Starting Value",-30} {officialStarting,15:N2} {calcStarting,15:N2} {(calcStarting - officialStarting),15:N2}");
                Console.WriteLine($"{"Mark-to-Market",-30} {officialMTM,15:N2} {calcMTM,15:N2} {(calcMTM - officialMTM),15:N2}");
                Console.WriteLine($"{"Deposits & Withdrawals",-30} {officialDeposits,15:N2} {calcDeposits,15:N2} {(calcDeposits - officialDeposits),15:N2}");
                Console.WriteLine($"{"Dividends",-30} {officialDividends,15:N2} {calcDividends,15:N2} {(calcDividends - officialDividends),15:N2}");
                Console.WriteLine($"{"Withholding Tax",-30} {officialWithholding,15:N2} {calcWithholding,15:N2} {(calcWithholding - officialWithholding),15:N2}");
                Console.WriteLine($"{"Change in Dividend Accruals",-30} {officialAccruals,15:N2} {calcAccruals,15:N2} {(calcAccruals - officialAccruals),15:N2}");
                Console.WriteLine($"{"Commissions",-30} {officialCommissions,15:N2} {calcCommissions,15:N2} {(calcCommissions - officialCommissions),15:N2}");
                Console.WriteLine($"{"Sales Tax",-30} {officialSalesTax,15:N2} {calcSalesTax,15:N2} {(calcSalesTax - officialSalesTax),15:N2}");
                Console.WriteLine($"{"Other FX Translations",-30} {officialFX,15:N2} {calcFX,15:N2} {(calcFX - officialFX),15:N2}");
                Console.WriteLine($"{"Ending Value",-30} {officialEnding,15:N2} {calcEnding,15:N2} {(calcEnding - officialEnding),15:N2}");
                Console.WriteLine();

                // Generate eCH XML output
                var echXml = GenerateEchXml(echStatement);
                string outputPath = "eCH-0196-output.xml";
                echXml.Save(outputPath);

                Console.WriteLine();
                Console.WriteLine($"✓ Generated eCH-0196 tax statement: {outputPath}");
                Console.WriteLine($"  - {depot.Securities.Count} securities");
                Console.WriteLine($"  - {depot.Securities.Sum(s => s.Stocks.Count)} stock mutations");
                Console.WriteLine($"  - {depot.Securities.Sum(s => s.Payments.Count)} dividend payments");

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing XML: {ex.Message}");
                return 3;
            }
        }

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

    }
}