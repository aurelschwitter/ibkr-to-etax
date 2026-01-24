using System;
using System.Globalization;
using System.Xml.Linq;
using Xunit;

namespace IbkrToEtax.Tests
{
    public class DataHelperTests
    {
        [Theory]
        [InlineData("123.45", 123.45)]
        [InlineData("1,234.56", 1234.56)]
        [InlineData("-999.99", -999.99)]
        [InlineData("0", 0)]
        [InlineData("", 0)]
        [InlineData(null, 0)]
        public void ParseDecimal_VariousInputs_ReturnsExpectedValue(string? input, decimal expected)
        {
            var result = DataHelper.ParseDecimal(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("2024-12-31", 2024, 12, 31)]
        [InlineData("2024-01-01", 2024, 1, 1)]
        [InlineData("", null, null, null)]
        [InlineData(null, null, null, null)]
        public void ParseNullableDate_VariousInputs_ReturnsExpectedValue(string? input, int? year, int? month, int? day)
        {
            var result = DataHelper.ParseNullableDate(input);
            
            if (year.HasValue)
            {
                Assert.NotNull(result);
                Assert.Equal(year.Value, result!.Value.Year);
                Assert.Equal(month!.Value, result.Value.Month);
                Assert.Equal(day!.Value, result.Value.Day);
            }
            else
            {
                Assert.Null(result);
            }
        }

        [Theory]
        [InlineData("US", "US")]
        [InlineData("CH", "CH")]
        [InlineData("DE", "DE")]
        [InlineData(null, "CH")]
        public void MapCountry_VariousCountryCodes_ReturnsExpectedMapping(string? input, string expected)
        {
            var result = DataHelper.MapCountry(input!);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("STK", "SHARE")]
        [InlineData("BOND", "BOND")]
        [InlineData("OPT", "OPTION")]
        [InlineData("FUT", "DEVT")]
        [InlineData("UNKNOWN", "OTHER")]
        public void MapSecurityCategory_VariousAssetCategories_ReturnsExpectedMapping(string input, string expected)
        {
            var result = DataHelper.MapSecurityCategory(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ConvertToCHF_WithValidData_ReturnsCorrectConversion()
        {
            var xml = new XElement("Transaction",
                new XAttribute("amount", "100.00"),
                new XAttribute("fxRateToBase", "0.85"));
            
            var result = DataHelper.ConvertToCHF(xml);
            
            Assert.Equal(85.00m, result);
        }

        [Fact]
        public void ConvertToCHF_WithNoFxRate_ReturnsZero()
        {
            var xml = new XElement("Transaction",
                new XAttribute("amount", "100.00"));
            
            var result = DataHelper.ConvertToCHF(xml);
            
            Assert.Equal(0m, result);
        }

        [Fact]
        public void ConvertToCHF_WithNegativeAmount_ReturnsNegativeResult()
        {
            var xml = new XElement("Transaction",
                new XAttribute("amount", "-50.00"),
                new XAttribute("fxRateToBase", "1.10"));
            
            var result = DataHelper.ConvertToCHF(xml);
            
            Assert.Equal(-55.00m, result);
        }
    }
}
