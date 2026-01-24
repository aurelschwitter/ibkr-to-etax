using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace IbkrToEtax.Tests
{
    public class IbkrDataParserTests
    {
        [Fact]
        public void ParseIbkrData_WithValidXml_ReturnsCorrectCounts()
        {
            var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<FlexQueryResponse>
  <FlexStatements>
    <FlexStatement accountId=""U12345"">
      <OpenPositions>
        <OpenPosition symbol=""AAPL"" levelOfDetail=""SUMMARY"" />
        <OpenPosition symbol=""MSFT"" levelOfDetail=""SUMMARY"" />
        <OpenPosition symbol=""GOOG"" levelOfDetail=""DETAIL"" />
      </OpenPositions>
      <Trades>
        <Trade accountId=""U12345"" symbol=""AAPL"" />
        <Trade accountId=""U12345"" symbol=""MSFT"" />
        <Trade accountId=""U99999"" symbol=""GOOG"" />
      </Trades>
      <CashTransactions>
        <CashTransaction accountId=""U12345"" type=""Dividends"" amount=""100.00"" />
        <CashTransaction accountId=""U12345"" type=""Dividends"" amount=""50.00"" />
        <CashTransaction accountId=""U12345"" type=""Withholding Tax"" amount=""-15.00"" />
        <CashTransaction accountId=""U99999"" type=""Dividends"" amount=""200.00"" />
      </CashTransactions>
    </FlexStatement>
  </FlexStatements>
</FlexQueryResponse>";

            var doc = XDocument.Parse(xml);
            var (openPositions, trades, dividends, withholdingTax) = IbkrDataParser.ParseIbkrData(doc, "U12345");

            Assert.Equal(2, openPositions.Count); // Only SUMMARY level
            Assert.Equal(2, trades.Count); // Only U12345 account
            Assert.Equal(2, dividends.Count); // Only U12345 dividends
            Assert.Equal(1, withholdingTax.Count); // Only U12345 withholding tax
        }

        [Fact]
        public void ParseIbkrData_WithNoData_ReturnsEmptyLists()
        {
            var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<FlexQueryResponse>
  <FlexStatements>
    <FlexStatement accountId=""U12345"" />
  </FlexStatements>
</FlexQueryResponse>";

            var doc = XDocument.Parse(xml);
            var (openPositions, trades, dividends, withholdingTax) = IbkrDataParser.ParseIbkrData(doc, "U12345");

            Assert.Empty(openPositions);
            Assert.Empty(trades);
            Assert.Empty(dividends);
            Assert.Empty(withholdingTax);
        }

        [Fact]
        public void ExtractDateRange_WithValidDates_ReturnsCorrectRange()
        {
            var flexStatements = new List<XElement>
            {
                new XElement("FlexStatement",
                    new XAttribute("fromDate", "2024-01-01"),
                    new XAttribute("toDate", "2024-12-31"))
            };

            var (periodFrom, periodTo, taxYear) = IbkrDataParser.ExtractDateRange(flexStatements);

            Assert.Equal(new DateTime(2024, 1, 1), periodFrom);
            Assert.Equal(new DateTime(2024, 12, 31), periodTo);
            Assert.Equal(2024, taxYear);
        }

        [Fact]
        public void ExtractDateRange_WithMultipleStatements_UsesEarliestAndLatest()
        {
            var flexStatements = new List<XElement>
            {
                new XElement("FlexStatement",
                    new XAttribute("fromDate", "2024-06-01"),
                    new XAttribute("toDate", "2024-08-31")),
                new XElement("FlexStatement",
                    new XAttribute("fromDate", "2024-01-01"),
                    new XAttribute("toDate", "2024-05-31")),
                new XElement("FlexStatement",
                    new XAttribute("fromDate", "2024-09-01"),
                    new XAttribute("toDate", "2024-12-31"))
            };

            var (periodFrom, periodTo, taxYear) = IbkrDataParser.ExtractDateRange(flexStatements);

            Assert.Equal(new DateTime(2024, 1, 1), periodFrom);
            Assert.Equal(new DateTime(2024, 12, 31), periodTo);
            Assert.Equal(2024, taxYear);
        }

        [Fact]
        public void ExtractDateRange_WithNoValidDates_UsesFallbackToCurrentYear()
        {
            var flexStatements = new List<XElement>
            {
                new XElement("FlexStatement")
            };

            var (periodFrom, periodTo, taxYear) = IbkrDataParser.ExtractDateRange(flexStatements);
            int currentYear = DateTime.Now.Year;

            Assert.Equal(new DateTime(currentYear, 1, 1), periodFrom);
            Assert.Equal(new DateTime(currentYear, 12, 31), periodTo);
            Assert.Equal(currentYear, taxYear);
        }
    }
}
