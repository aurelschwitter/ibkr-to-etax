using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace IbkrToEtax.Tests
{
    public class IntegrationTests
    {
        [Fact]
        public void EndToEnd_ParseIbkrAndGenerateEchXml_Success()
        {
            // Load sample IBKR data
            var testDataPath = Path.Combine("TestData", "sample-ibkr-data.xml");
            var doc = XDocument.Load(testDataPath);
            
            var accountId = "U12345";
            
            // Parse IBKR data
            var (openPositions, trades, dividends, withholdingTax) = IbkrDataParser.ParseIbkrData(doc, accountId);
            
            Assert.Equal(2, openPositions.Count);
            Assert.Equal(2, trades.Count);
            Assert.Equal(2, dividends.Count);
            Assert.Equal(2, withholdingTax.Count);
            
            // Extract date range
            var flexStatements = doc.Descendants("FlexStatement").ToList();
            var (periodFrom, periodTo, taxYear) = IbkrDataParser.ExtractDateRange(flexStatements);
            
            Assert.Equal(2024, taxYear);
            Assert.Equal(new DateTime(2024, 1, 1), periodFrom);
            Assert.Equal(new DateTime(2024, 12, 31), periodTo);
            
            // Build eCH tax statement
            var statement = EchStatementBuilder.BuildEchTaxStatement(
                doc, openPositions, trades, dividends, withholdingTax,
                accountId, taxYear, periodFrom, periodTo, "ZH");
            
            Assert.NotNull(statement);
            Assert.Equal(2024, statement.TaxPeriod);
            Assert.Equal("ZH", statement.Canton);
            Assert.Single(statement.Depots);
            
            var depot = statement.Depots[0];
            Assert.Equal(3, depot.Securities.Count); // 2 stocks + cash
            
            // Check that positions have tax values
            var stocksWithTaxValue = depot.Securities.Where(s => s.TaxValue != null).ToList();
            Assert.Equal(3, stocksWithTaxValue.Count); // All should have year-end positions
            
            // Check dividend payments
            var securitiesWithPayments = depot.Securities.Where(s => s.Payments.Count > 0).ToList();
            Assert.Equal(2, securitiesWithPayments.Count); // AAPL and MSFT
            
            // Generate eCH XML
            var echXml = EchXmlGenerator.GenerateEchXml(statement);
            
            Assert.NotNull(echXml);
            Assert.NotNull(echXml.Root);
            Assert.Equal("taxStatement", echXml.Root.Name.LocalName);
            
            XNamespace ns = "http://www.ech.ch/xmlns/eCH-0196/2";
            var securities = echXml.Descendants(ns + "security").ToList();
            Assert.Equal(3, securities.Count);
            
            // Verify totals are calculated
            var totalTaxValue = decimal.Parse(echXml.Root.Attribute("totalTaxValue")!.Value);
            Assert.True(totalTaxValue > 0);
            
            var totalGrossRevenueB = decimal.Parse(echXml.Root.Attribute("totalGrossRevenueB")!.Value);
            Assert.True(totalGrossRevenueB > 0); // Foreign dividends
        }

        [Fact]
        public void EndToEnd_VerifyWithholdingTaxMatching_Success()
        {
            var testDataPath = Path.Combine("TestData", "sample-ibkr-data.xml");
            var doc = XDocument.Load(testDataPath);
            var accountId = "U12345";
            
            var (openPositions, trades, dividends, withholdingTax) = IbkrDataParser.ParseIbkrData(doc, accountId);
            var flexStatements = doc.Descendants("FlexStatement").ToList();
            var (periodFrom, periodTo, taxYear) = IbkrDataParser.ExtractDateRange(flexStatements);
            
            var statement = EchStatementBuilder.BuildEchTaxStatement(
                doc, openPositions, trades, dividends, withholdingTax,
                accountId, taxYear, periodFrom, periodTo, "ZH");
            
            // Check AAPL dividend
            var applSecurity = statement.Depots[0].Securities.FirstOrDefault(s => s.SecurityName.Contains("APPLE"));
            Assert.NotNull(applSecurity);
            Assert.Single(applSecurity.Payments);
            
            var applPayment = applSecurity.Payments[0];
            // Net: 2.04 USD * 0.85 = 1.734 CHF
            // Tax: 0.36 USD * 0.85 = 0.306 CHF
            // Gross: 2.04 + 0.36 = 2.40 USD * 0.85 = 2.04 CHF
            Assert.True(applPayment.WithHoldingTaxClaim > 0);
            Assert.True(applPayment.Amount > applPayment.WithHoldingTaxClaim);
            
            // Check MSFT dividend
            var msftSecurity = statement.Depots[0].Securities.FirstOrDefault(s => s.SecurityName.Contains("MICROSOFT"));
            Assert.NotNull(msftSecurity);
            Assert.Single(msftSecurity.Payments);
            
            var msftPayment = msftSecurity.Payments[0];
            Assert.True(msftPayment.WithHoldingTaxClaim > 0);
        }

        [Fact]
        public void EndToEnd_VerifyCashPositionIncluded_Success()
        {
            var testDataPath = Path.Combine("TestData", "sample-ibkr-data.xml");
            var doc = XDocument.Load(testDataPath);
            var accountId = "U12345";
            
            var (openPositions, trades, dividends, withholdingTax) = IbkrDataParser.ParseIbkrData(doc, accountId);
            var flexStatements = doc.Descendants("FlexStatement").ToList();
            var (periodFrom, periodTo, taxYear) = IbkrDataParser.ExtractDateRange(flexStatements);
            
            var statement = EchStatementBuilder.BuildEchTaxStatement(
                doc, openPositions, trades, dividends, withholdingTax,
                accountId, taxYear, periodFrom, periodTo, "ZH");
            
            // Find cash position
            var cashSecurity = statement.Depots[0].Securities.FirstOrDefault(s => s.SecurityName == "Cash Balance");
            Assert.NotNull(cashSecurity);
            Assert.Equal("OTHER", cashSecurity.SecurityCategory);
            Assert.Equal("CHF", cashSecurity.Currency);
            Assert.NotNull(cashSecurity.TaxValue);
            Assert.Equal(289.64m, cashSecurity.TaxValue.Value);
        }

        [Fact]
        public void EndToEnd_VerifyStockMutationsIncluded_Success()
        {
            var testDataPath = Path.Combine("TestData", "sample-ibkr-data.xml");
            var doc = XDocument.Load(testDataPath);
            var accountId = "U12345";
            
            var (openPositions, trades, dividends, withholdingTax) = IbkrDataParser.ParseIbkrData(doc, accountId);
            var flexStatements = doc.Descendants("FlexStatement").ToList();
            var (periodFrom, periodTo, taxYear) = IbkrDataParser.ExtractDateRange(flexStatements);
            
            var statement = EchStatementBuilder.BuildEchTaxStatement(
                doc, openPositions, trades, dividends, withholdingTax,
                accountId, taxYear, periodFrom, periodTo, "ZH");
            
            // Check that trades were converted to stock mutations
            var applSecurity = statement.Depots[0].Securities.FirstOrDefault(s => s.SecurityName.Contains("APPLE"));
            Assert.NotNull(applSecurity);
            Assert.Single(applSecurity.Stocks);
            
            var stock = applSecurity.Stocks[0];
            Assert.True(stock.IsMutation);
            Assert.Equal(10, stock.Quantity);
            Assert.Equal(new DateTime(2024, 3, 15), stock.ReferenceDate);
            
            var msftSecurity = statement.Depots[0].Securities.FirstOrDefault(s => s.SecurityName.Contains("MICROSOFT"));
            Assert.NotNull(msftSecurity);
            Assert.Single(msftSecurity.Stocks);
        }
    }
}
