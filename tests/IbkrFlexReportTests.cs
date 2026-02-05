using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using IbkrToEtax.IbkrReport;
using Microsoft.Extensions.Logging;
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
    <FlexStatement accountId=""U12345"" fromDate=""2024-01-01"" toDate=""2024-12-31"">
      <AccountInformation accountId=""U12345"" currency=""CHF"" state=""CH-ZH"" dateOpened=""2022-01-01"" />
      <EquitySummaryInBase>
        <EquitySummaryByReportDateInBase accountId=""U12345"" reportDate=""2024-12-31"" cash=""0"" />
      </EquitySummaryInBase>
      <OpenPositions>
        <OpenPosition symbol=""AAPL"" levelOfDetail=""SUMMARY"" />
        <OpenPosition symbol=""MSFT"" levelOfDetail=""SUMMARY"" />
        <OpenPosition symbol=""GOOG"" levelOfDetail=""DETAIL"" />
      </OpenPositions>
      <Trades>
        <Trade accountId=""U12345"" symbol=""AAPL"" />
        <Trade accountId=""U12345"" symbol=""MSFT"" />
        <Trade accountId=""U12345"" symbol=""GOOG"" />
      </Trades>
      <CashTransactions>
        <CashTransaction accountId=""U12345"" type=""Dividends"" amount=""100.00"" />
        <CashTransaction accountId=""U12345"" type=""Dividends"" amount=""50.00"" />
        <CashTransaction accountId=""U12345"" type=""Withholding Tax"" amount=""-15.00"" />
        <CashTransaction accountId=""U12345"" type=""Dividends"" amount=""200.00"" />
      </CashTransactions>
      <FIFOPerformanceSummaryInBase />
    </FlexStatement>
  </FlexStatements>
</FlexQueryResponse>";

      var doc = XDocument.Parse(xml);
      var report = new IbkrFlexReport(doc, TestLoggerFactory.Create());

      Assert.Equal(2, report.OpenPositions.Count); // Only SUMMARY level
      Assert.Equal(3, report.Trades.Count);
      Assert.Equal(3, report.DividendList.Count); 
      Assert.Single(report.WithholdingTaxList); 
    }

    [Fact]
    public void ParseIbkrData_WithNoData_ReturnsEmptyLists()
    {
      var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<FlexQueryResponse>
  <FlexStatements>
    <FlexStatement accountId=""U12345"" fromDate=""2024-01-01"" toDate=""2024-12-31"">
      <AccountInformation accountId=""U12345"" currency=""CHF"" state=""CH-ZH"" dateOpened=""2022-01-01"" />
      <EquitySummaryInBase />
      <OpenPositions />
      <Trades />
      <CashTransactions />
      <FIFOPerformanceSummaryInBase />
    </FlexStatement>
  </FlexStatements>
</FlexQueryResponse>";

      var doc = XDocument.Parse(xml);
      var report = new IbkrFlexReport(doc, TestLoggerFactory.Create());

      Assert.Empty(report.OpenPositions);
      Assert.Empty(report.Trades);
      Assert.Empty(report.DividendList);
      Assert.Empty(report.WithholdingTaxList);
    }

    [Fact]
    public void ExtractDateRange_WithValidDates_ReturnsCorrectRange()
    {
      var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<FlexQueryResponse>
  <FlexStatements>
    <FlexStatement accountId=""U12345"" fromDate=""2024-01-01"" toDate=""2024-12-31"">
      <AccountInformation accountId=""U12345"" currency=""CHF"" state=""CH-ZH"" dateOpened=""2022-01-01"" />
      <EquitySummaryInBase />
      <OpenPositions />
      <Trades />
      <CashTransactions />
      <FIFOPerformanceSummaryInBase />
    </FlexStatement>
  </FlexStatements>
</FlexQueryResponse>";

      var doc = XDocument.Parse(xml);
      var report = new IbkrFlexReport(doc, TestLoggerFactory.Create());

      Assert.Equal(new DateTime(2024, 1, 1), report.StartDate);
      Assert.Equal(new DateTime(2024, 12, 31), report.EndDate);
      Assert.Equal(2024, report.TaxYear);
    }

    [Theory]
    [InlineData("2024-01-01", "2024-12-31")]
    [InlineData("2024-06-15", "2024-06-14")]
    [InlineData("2024-12-12", "2025-01-12")]
    [InlineData("2023-01-13", "2023-01-12")]

    public void ExtractDateRange_FailsWhenDatesAreIncorrect(string fromDate, string toDate)
    {
      var xml = $@"<FlexStatement accountId=""U12345"" fromDate=""{fromDate}"" toDate=""{toDate}"" ></FlexStatement>";

      var doc = XDocument.Parse(xml);

      Assert.Throws<Exception>(() =>
      {
        new IbkrFlexReport(doc, TestLoggerFactory.Create());
      });
    }
  }

}
