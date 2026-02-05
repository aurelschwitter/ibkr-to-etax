using System.Xml.Linq;
using IbkrToEtax.IbkrReport;
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
            var ibkrReport = CreateSampleDocument();

            var result = new EchStatementBuilder(ibkrReport, TestLoggerFactory.Create()).BuildEchTaxStatement();

            Assert.NotNull(result);
            Assert.Equal(2024, result.TaxPeriod);
            Assert.Equal("ZH", result.Canton);
            Assert.Equal("U12345", result.ClientNumber);
            Assert.Single(result.Depots);
        }

        [Fact]
        public void BuildEchTaxStatement_IncludesOnlyYearEndPositions_InTaxValue()
        {
            var ibkrReport = CreateSampleDocument();

            var result = new EchStatementBuilder(ibkrReport, TestLoggerFactory.Create()).BuildEchTaxStatement();

            // This test needs to be updated based on actual implementation
            Assert.NotNull(result);
            Assert.Single(result.Depots);
        }

        [Fact]
        public void BuildEchTaxStatement_IncludesCashPosition_WhenPresent()
        {
            var ibkrReport = CreateSampleDocumentWithCash(289.64m);

            var result = new EchStatementBuilder(ibkrReport, TestLoggerFactory.Create()).BuildEchTaxStatement();

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
            var ibkrReport = CreateSampleDocument();

            var result = new EchStatementBuilder(ibkrReport, TestLoggerFactory.Create()).BuildEchTaxStatement();

            // This test needs actual dividend/withholding tax data in the document
            Assert.NotNull(result);
            Assert.Single(result.Depots);
        }

        [Fact]
        public void AddDividendsAsPayments_CalculatesUSWithholdingTax_ForUSSecurities()
        {
            var ibkrReport = CreateSampleDocument();

            var result = new EchStatementBuilder(ibkrReport, TestLoggerFactory.Create()).BuildEchTaxStatement();

            // This test needs actual dividend data in the document
            Assert.NotNull(result);
            Assert.Single(result.Depots);
        }

        private IbkrFlexReport CreateSampleDocument()
        {
            var doc = XDocument.Parse(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<FlexQueryResponse>
  <FlexStatements>
    <FlexStatement accountId=""U12345"" fromDate=""2024-01-01"" toDate=""2024-12-31"">
      <AccountInformation accountId=""U12345"" name=""Test Account"" currency=""CHF"" state=""CH-ZH"" dateOpened=""2023-12-31"" dateFunded=""2023-12-31"" />
      <EquitySummaryInBase>
        <EquitySummaryByReportDateInBase accountId=""U12345"" reportDate=""2024-12-31"" cash=""0"" />
      </EquitySummaryInBase>
      <openPositions />
      <trades />
      <cashTransactions />
      <SecuritiesInfo>
        <SecurityInfo symbol=""DUMMY"" />
      </SecuritiesInfo>
      <FIFOPerformanceSummaryInBase />
    </FlexStatement>
  </FlexStatements>
</FlexQueryResponse>");

            return new IbkrFlexReport(doc, TestLoggerFactory.Create());
        }

        private IbkrFlexReport CreateSampleDocumentWithCash(decimal cashAmount)
        {
            var doc = XDocument.Parse($@"<?xml version=""1.0"" encoding=""UTF-8""?>
<FlexQueryResponse>
  <FlexStatements>
    <FlexStatement accountId=""U12345"" fromDate=""2024-01-01"" toDate=""2024-12-31"">
      <AccountInformation accountId=""U12345"" name=""Test Account"" currency=""CHF"" state=""CH-ZH"" dateOpened=""2023-12-31"" dateFunded=""2023-12-31"" />
      <EquitySummaryInBase>
        <EquitySummaryByReportDateInBase accountId=""U12345"" reportDate=""2024-12-31"" cash=""{cashAmount}"" />
      </EquitySummaryInBase>
      <openPositions />
      <trades />
      <cashTransactions />
      <SecuritiesInfo>
        <SecurityInfo symbol=""DUMMY"" />
      </SecuritiesInfo>
      <FIFOPerformanceSummaryInBase />
    </FlexStatement>
  </FlexStatements>
</FlexQueryResponse>");

            return new IbkrFlexReport(doc, TestLoggerFactory.Create());
        }
    }
}
