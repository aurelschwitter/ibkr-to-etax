using System;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace IbkrToEtax.Tests
{
    public class EchXmlGeneratorTests
    {
        [Fact]
        public void GenerateEchXml_WithValidStatement_CreatesValidXml()
        {
            var statement = CreateSampleStatement();

            var result = EchXmlGenerator.GenerateEchXml(statement);

            Assert.NotNull(result);
            Assert.NotNull(result.Root);
            Assert.Equal("taxStatement", result.Root.Name.LocalName);
        }

        [Fact]
        public void GenerateEchXml_IncludesCorrectNamespace()
        {
            var statement = CreateSampleStatement();

            var result = EchXmlGenerator.GenerateEchXml(statement);

            Assert.Equal("http://www.ech.ch/xmlns/eCH-0196/2", result.Root!.Name.NamespaceName);
        }

        [Fact]
        public void GenerateEchXml_IncludesAllRequiredAttributes()
        {
            var statement = CreateSampleStatement();

            var result = EchXmlGenerator.GenerateEchXml(statement);
            var root = result.Root!;

            Assert.NotNull(root.Attribute("id"));
            Assert.NotNull(root.Attribute("creationDate"));
            Assert.NotNull(root.Attribute("taxPeriod"));
            Assert.NotNull(root.Attribute("periodFrom"));
            Assert.NotNull(root.Attribute("periodTo"));
            Assert.NotNull(root.Attribute("canton"));
            Assert.NotNull(root.Attribute("totalTaxValue"));
            Assert.NotNull(root.Attribute("minorVersion"));
        }

        [Fact]
        public void GenerateEchXml_CalculatesCorrectTotalTaxValue()
        {
            var statement = CreateSampleStatement();
            var expectedTotal = statement.Depots.Sum(d => d.Securities.Sum(s => s.TaxValue?.Value ?? 0));

            var result = EchXmlGenerator.GenerateEchXml(statement);
            var totalTaxValue = decimal.Parse(result.Root!.Attribute("totalTaxValue")!.Value);

            Assert.Equal(expectedTotal, totalTaxValue);
        }

        [Fact]
        public void GenerateEchXml_CalculatesCorrectTotalGrossRevenue()
        {
            var statement = CreateSampleStatement();
            var expectedRevenueA = statement.Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.GrossRevenueA)));
            var expectedRevenueB = statement.Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.GrossRevenueB)));

            var result = EchXmlGenerator.GenerateEchXml(statement);

            var revenueA = decimal.Parse(result.Root!.Attribute("totalGrossRevenueA")!.Value);
            var revenueB = decimal.Parse(result.Root!.Attribute("totalGrossRevenueB")!.Value);

            Assert.Equal(expectedRevenueA, revenueA);
            Assert.Equal(expectedRevenueB, revenueB);
        }

        [Fact]
        public void GenerateEchXml_IncludesAllSecurities()
        {
            var statement = CreateSampleStatement();

            var result = EchXmlGenerator.GenerateEchXml(statement);
            XNamespace ns = "http://www.ech.ch/xmlns/eCH-0196/2";
            var securities = result.Descendants(ns + "security").ToList();

            Assert.Equal(2, securities.Count);
        }

        [Fact]
        public void GenerateEchXml_SecurityHasCorrectAttributes()
        {
            var statement = CreateSampleStatement();

            var result = EchXmlGenerator.GenerateEchXml(statement);
            XNamespace ns = "http://www.ech.ch/xmlns/eCH-0196/2";
            var security = result.Descendants(ns + "security").First();

            Assert.NotNull(security.Attribute("positionId"));
            Assert.NotNull(security.Attribute("country"));
            Assert.NotNull(security.Attribute("currency"));
            Assert.NotNull(security.Attribute("securityCategory"));
            Assert.NotNull(security.Attribute("securityName"));
        }

        [Fact]
        public void GenerateEchXml_PaymentUsesChfCurrency()
        {
            var statement = CreateSampleStatement();

            var result = EchXmlGenerator.GenerateEchXml(statement);
            XNamespace ns = "http://www.ech.ch/xmlns/eCH-0196/2";
            var payment = result.Descendants(ns + "payment").First();

            Assert.Equal("CHF", payment.Attribute("amountCurrency")!.Value);
        }

        [Fact]
        public void GenerateEchXml_SecurityWithoutTaxValue_OmitsElement()
        {
            var statement = new EchTaxStatement
            {
                Id = "TEST001",
                TaxPeriod = 2024,
                PeriodFrom = new DateTime(2024, 1, 1),
                PeriodTo = new DateTime(2024, 12, 31),
                Canton = "ZH",
                ClientNumber = "U12345",
                Institution = "Test Bank"
            };

            var depot = new EchSecurityDepot { DepotNumber = "U12345" };
            depot.Securities.Add(new EchSecurity
            {
                PositionId = 1,
                Isin = "US0378331005",
                Country = "US",
                Currency = "USD",
                SecurityCategory = "SHARE",
                SecurityName = "Apple Inc.",
                TaxValue = null // No tax value
            });
            statement.Depots.Add(depot);

            var result = EchXmlGenerator.GenerateEchXml(statement);
            XNamespace ns = "http://www.ech.ch/xmlns/eCH-0196/2";
            var taxValue = result.Descendants(ns + "taxValue").FirstOrDefault();

            Assert.Null(taxValue);
        }

        [Fact]
        public void GenerateEchXml_IncludesStockMutations()
        {
            var statement = CreateSampleStatement();

            var result = EchXmlGenerator.GenerateEchXml(statement);
            XNamespace ns = "http://www.ech.ch/xmlns/eCH-0196/2";
            var stocks = result.Descendants(ns + "stock").ToList();

            Assert.NotEmpty(stocks);
            var stock = stocks.First();
            Assert.NotNull(stock.Attribute("referenceDate"));
            Assert.NotNull(stock.Attribute("mutation"));
            Assert.NotNull(stock.Attribute("quantity"));
        }

        private EchTaxStatement CreateSampleStatement()
        {
            var statement = new EchTaxStatement
            {
                Id = "CH0000001U0000000123452024123101",
                TaxPeriod = 2024,
                PeriodFrom = new DateTime(2024, 1, 1),
                PeriodTo = new DateTime(2024, 12, 31),
                Canton = "ZH",
                ClientNumber = "U12345",
                Institution = "Interactive Brokers"
            };

            var depot = new EchSecurityDepot { DepotNumber = "U12345" };

            // Security 1: AAPL with position and dividend
            var apple = new EchSecurity
            {
                PositionId = 1,
                Isin = "US0378331005",
                Country = "US",
                Currency = "USD",
                SecurityCategory = "SHARE",
                SecurityName = "Apple Inc.",
                TaxValue = new EchTaxValue
                {
                    ReferenceDate = new DateTime(2024, 12, 31),
                    Quantity = 10,
                    UnitPrice = 150.00m,
                    Value = 1275.00m
                }
            };

            apple.Payments.Add(new EchPayment
            {
                PaymentDate = new DateTime(2024, 5, 15),
                ExDate = new DateTime(2024, 5, 10),
                Quantity = 0,
                Amount = 100.00m,
                GrossRevenueA = 0,
                GrossRevenueB = 100.00m,
                WithHoldingTaxClaim = 15.00m,
                AdditionalWithHoldingTaxUSA = 15.00m
            });

            apple.Stocks.Add(new EchStock
            {
                ReferenceDate = new DateTime(2024, 3, 15),
                IsMutation = true,
                Quantity = 10,
                UnitPrice = 145.00m,
                Value = 1450.00m
            });

            depot.Securities.Add(apple);

            // Security 2: Cash
            var cash = new EchSecurity
            {
                PositionId = 2,
                Isin = "",
                Country = "CH",
                Currency = "CHF",
                SecurityCategory = "OTHER",
                SecurityName = "Cash Balance",
                TaxValue = new EchTaxValue
                {
                    ReferenceDate = new DateTime(2024, 12, 31),
                    Quantity = 1,
                    UnitPrice = 289.64m,
                    Value = 289.64m
                }
            };

            depot.Securities.Add(cash);
            statement.Depots.Add(depot);

            return statement;
        }
    }
}
