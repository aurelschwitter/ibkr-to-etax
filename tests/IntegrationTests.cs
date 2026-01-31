using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using IbkrToEtax.IbkrReport;
using Microsoft.Extensions.Logging;
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

            // Parse IBKR data
            var report = new IbkrFlexReport(doc, new LoggerFactory());

            Assert.Equal(2, report.OpenPositions.Count);
            Assert.Equal(2, report.Trades.Count);
            Assert.Equal(2, report.DividendList.Count);
            Assert.Equal(2, report.WithholdingTaxList.Count);

            // Extract date range

            Assert.Equal(2024, report.TaxYear);
            Assert.Equal(new DateTime(2024, 1, 1), report.StartDate);
            Assert.Equal(new DateTime(2024, 12, 31), report.EndDate);

            // Build eCH tax statement
            var statement = new EchStatementBuilder(report, new LoggerFactory()).BuildEchTaxStatement();

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

            var report = new IbkrFlexReport(doc, new LoggerFactory());

            var statement = new EchStatementBuilder(report, new LoggerFactory()).BuildEchTaxStatement();

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

            var report = new IbkrFlexReport(doc, new LoggerFactory());

            var statement = new EchStatementBuilder(report, new LoggerFactory()).BuildEchTaxStatement();

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

            var report = new IbkrFlexReport(doc, new LoggerFactory());

            var statement = new EchStatementBuilder(report, new LoggerFactory()).BuildEchTaxStatement();

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
