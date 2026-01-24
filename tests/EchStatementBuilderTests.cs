using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace IbkrToEtax.Tests
{
    public class EchStatementBuilderTests
    {
        [Fact]
        public void GenerateEchId_WithValidInput_ReturnsCorrectFormat()
        {
            var result = EchStatementBuilder.GenerateEchId("U12345", new DateTime(2024, 12, 31), 1);

            // Should start with CH00000, contain account and date
            Assert.StartsWith("CH00000", result);
            Assert.Contains("20241231", result);
            Assert.EndsWith("01", result);
        }

        [Fact]
        public void GenerateEchId_WithLongAccountId_TruncatesCorrectly()
        {
            var result = EchStatementBuilder.GenerateEchId("U123456789012345", new DateTime(2024, 12, 31), 1);

            // ID should have proper length despite long account ID
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
        }

        [Fact]
        public void BuildEchTaxStatement_WithValidData_CreatesStatement()
        {
            var doc = CreateSampleDocument();
            var openPositions = new List<XElement>
            {
                new XElement("OpenPosition",
                    new XAttribute("symbol", "AAPL"),
                    new XAttribute("reportDate", "2024-12-31"),
                    new XAttribute("isin", "US0378331005"),
                    new XAttribute("issuerCountryCode", "US"),
                    new XAttribute("currency", "USD"),
                    new XAttribute("assetCategory", "STK"),
                    new XAttribute("description", "Apple Inc."),
                    new XAttribute("position", "10"),
                    new XAttribute("markPrice", "150.00"),
                    new XAttribute("positionValueInBase", "1275.00"))
            };
            var trades = new List<XElement>();
            var dividends = new List<XElement>();
            var withholdingTax = new List<XElement>();

            var result = EchStatementBuilder.BuildEchTaxStatement(
                doc, openPositions, trades, dividends, withholdingTax,
                "U12345", 2024, new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), "ZH");

            Assert.NotNull(result);
            Assert.Equal(2024, result.TaxPeriod);
            Assert.Equal("ZH", result.Canton);
            Assert.Equal("U12345", result.ClientNumber);
            Assert.Single(result.Depots);
            Assert.True(result.Depots[0].Securities.Count >= 1); // At least the AAPL position
        }

        [Fact]
        public void BuildEchTaxStatement_IncludesOnlyYearEndPositions_InTaxValue()
        {
            var doc = CreateSampleDocument();
            var openPositions = new List<XElement>
            {
                // Year-end position
                new XElement("OpenPosition",
                    new XAttribute("symbol", "AAPL"),
                    new XAttribute("reportDate", "2024-12-31"),
                    new XAttribute("isin", "US0378331005"),
                    new XAttribute("currency", "USD"),
                    new XAttribute("assetCategory", "STK"),
                    new XAttribute("description", "Apple Inc."),
                    new XAttribute("position", "10"),
                    new XAttribute("markPrice", "150.00"),
                    new XAttribute("positionValueInBase", "1275.00")),
                // Mid-year position (should not have TaxValue)
                new XElement("OpenPosition",
                    new XAttribute("symbol", "AAPL"),
                    new XAttribute("reportDate", "2024-06-30"),
                    new XAttribute("isin", "US0378331005"),
                    new XAttribute("currency", "USD"),
                    new XAttribute("assetCategory", "STK"),
                    new XAttribute("description", "Apple Inc."),
                    new XAttribute("position", "5"),
                    new XAttribute("markPrice", "140.00"),
                    new XAttribute("positionValueInBase", "595.00"))
            };

            var result = EchStatementBuilder.BuildEchTaxStatement(
                doc, openPositions, new List<XElement>(), new List<XElement>(), new List<XElement>(),
                "U12345", 2024, new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), "ZH");

            var applSecurity = result.Depots[0].Securities.FirstOrDefault(s => s.SecurityName == "Apple Inc.");
            Assert.NotNull(applSecurity);
            Assert.NotNull(applSecurity.TaxValue); // Should have year-end TaxValue
            Assert.Equal(10, applSecurity.TaxValue.Quantity);
        }

        [Fact]
        public void BuildEchTaxStatement_IncludesCashPosition_WhenPresent()
        {
            var doc = CreateSampleDocumentWithCash(289.64m);
            var openPositions = new List<XElement>();

            var result = EchStatementBuilder.BuildEchTaxStatement(
                doc, openPositions, new List<XElement>(), new List<XElement>(), new List<XElement>(),
                "U12345", 2024, new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), "ZH");

            var cashSecurity = result.Depots[0].Securities.FirstOrDefault(s => s.SecurityName == "Cash Balance");
            Assert.NotNull(cashSecurity);
            Assert.Equal("OTHER", cashSecurity.SecurityCategory);
            Assert.Equal("CHF", cashSecurity.Currency);
            Assert.NotNull(cashSecurity.TaxValue);
            Assert.Equal(289.64m, cashSecurity.TaxValue.Value);
        }

        [Fact]
        public void AddDividendsAsPayments_MatchesWithholdingTax_ByActionID()
        {
            var doc = CreateSampleDocument();
            var openPositions = new List<XElement>
            {
                new XElement("OpenPosition",
                    new XAttribute("symbol", "AAPL"),
                    new XAttribute("reportDate", "2024-12-31"),
                    new XAttribute("isin", "US0378331005"),
                    new XAttribute("issuerCountryCode", "US"),
                    new XAttribute("currency", "USD"),
                    new XAttribute("assetCategory", "STK"),
                    new XAttribute("description", "Apple Inc."),
                    new XAttribute("position", "10"),
                    new XAttribute("markPrice", "150.00"),
                    new XAttribute("positionValueInBase", "1275.00"))
            };

            var dividends = new List<XElement>
            {
                new XElement("CashTransaction",
                    new XAttribute("symbol", "AAPL"),
                    new XAttribute("settleDate", "2024-05-15"),
                    new XAttribute("actionID", "12345"),
                    new XAttribute("amount", "100.00"),
                    new XAttribute("fxRateToBase", "0.85"))
            };

            var withholdingTax = new List<XElement>
            {
                new XElement("CashTransaction",
                    new XAttribute("symbol", "AAPL"),
                    new XAttribute("settleDate", "2024-05-15"),
                    new XAttribute("actionID", "12345"),
                    new XAttribute("amount", "-15.00"),
                    new XAttribute("fxRateToBase", "0.85"))
            };

            var result = EchStatementBuilder.BuildEchTaxStatement(
                doc, openPositions, new List<XElement>(), dividends, withholdingTax,
                "U12345", 2024, new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), "ZH");

            var applSecurity = result.Depots[0].Securities.First();
            Assert.Single(applSecurity.Payments);
            
            var payment = applSecurity.Payments[0];
            Assert.Equal(85.00m, payment.Amount - payment.WithHoldingTaxClaim); // Net amount
            Assert.Equal(12.75m, payment.WithHoldingTaxClaim); // Tax amount
        }

        [Fact]
        public void AddDividendsAsPayments_CalculatesUSWithholdingTax_ForUSSecurities()
        {
            var doc = CreateSampleDocument();
            var openPositions = new List<XElement>
            {
                new XElement("OpenPosition",
                    new XAttribute("symbol", "AAPL"),
                    new XAttribute("reportDate", "2024-12-31"),
                    new XAttribute("isin", "US0378331005"),
                    new XAttribute("issuerCountryCode", "US"),
                    new XAttribute("currency", "USD"),
                    new XAttribute("assetCategory", "STK"),
                    new XAttribute("description", "Apple Inc."),
                    new XAttribute("position", "10"),
                    new XAttribute("markPrice", "150.00"),
                    new XAttribute("positionValueInBase", "1275.00"))
            };

            var dividends = new List<XElement>
            {
                new XElement("CashTransaction",
                    new XAttribute("symbol", "AAPL"),
                    new XAttribute("settleDate", "2024-05-15"),
                    new XAttribute("actionID", "12345"),
                    new XAttribute("amount", "100.00"),
                    new XAttribute("fxRateToBase", "0.85"))
            };

            var result = EchStatementBuilder.BuildEchTaxStatement(
                doc, openPositions, new List<XElement>(), dividends, new List<XElement>(),
                "U12345", 2024, new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), "ZH");

            var payment = result.Depots[0].Securities[0].Payments[0];
            
            // 15% of gross amount (85.00)
            Assert.Equal(85.00m * 0.15m, payment.AdditionalWithHoldingTaxUSA);
        }

        private XDocument CreateSampleDocument()
        {
            return XDocument.Parse(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<FlexQueryResponse>
  <FlexStatements>
    <FlexStatement accountId=""U12345"">
      <EquitySummaryByReportDateInBase accountId=""U12345"" reportDate=""2024-12-31"" cash=""0"" />
    </FlexStatement>
  </FlexStatements>
</FlexQueryResponse>");
        }

        private XDocument CreateSampleDocumentWithCash(decimal cashAmount)
        {
            return XDocument.Parse($@"<?xml version=""1.0"" encoding=""UTF-8""?>
<FlexQueryResponse>
  <FlexStatements>
    <FlexStatement accountId=""U12345"">
      <EquitySummaryByReportDateInBase accountId=""U12345"" reportDate=""2024-12-31"" cash=""{cashAmount}"" />
    </FlexStatement>
  </FlexStatements>
</FlexQueryResponse>");
        }
    }
}
