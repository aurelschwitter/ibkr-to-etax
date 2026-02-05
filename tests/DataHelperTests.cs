using IbkrToEtax.IbkrReport;
using Xunit;

namespace IbkrToEtax.Tests
{
    public class DataHelperTests
    {
        // Mock class for testing ConvertToCHF
        private class MockForeignCashElement : IIbkrForeignCashValueElement
        {
            public string Currency { get; set; } = "";
            public decimal FxRateToBase { get; set; }
            public decimal Amount { get; set; }
        }

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
        [InlineData(null, "n/a")]
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
            var element = new MockForeignCashElement
            {
                Amount = 100.00m,
                FxRateToBase = 0.85m
            };
            
            var result = DataHelper.ConvertToCHF(element);
            
            Assert.Equal(85.00m, result);
        }

        [Fact]
        public void ConvertToCHF_WithNoFxRate_ReturnsZero()
        {
            var element = new MockForeignCashElement
            {
                Amount = 100.00m,
                FxRateToBase = 0m
            };
            
            var result = DataHelper.ConvertToCHF(element);
            
            Assert.Equal(0m, result);
        }

        [Fact]
        public void ConvertToCHF_WithNegativeAmount_ReturnsNegativeResult()
        {
            var element = new MockForeignCashElement
            {
                Amount = -50.00m,
                FxRateToBase = 1.10m
            };
            
            var result = DataHelper.ConvertToCHF(element);
            
            Assert.Equal(-55.00m, result);
        }

        [Theory]
        [InlineData(99.9999, 100.000)] // < 100, 3 decimals, rounds to 100.000
        [InlineData(99.9944, 99.994)] // < 100, round down
        [InlineData(99.9945, 99.995)] // < 100, round up
        [InlineData(50.1234, 50.123)] // < 100, round down
        [InlineData(50.1235, 50.124)] // < 100, round up
        [InlineData(100.00, 100.00)] // >= 100, 2 decimals
        [InlineData(100.004, 100.00)] // >= 100, round down
        [InlineData(100.005, 100.01)] // >= 100, round up (0.5 rounds up per DIN 1333)
        [InlineData(1000.124, 1000.12)] // >= 100, round down
        [InlineData(1000.125, 1000.13)] // >= 100, round up
        [InlineData(57759.20474748, 57759.20)] // Real value from output
        [InlineData(215.1081339, 215.11)] // Real value from output
        [InlineData(45.961090410, 45.961)] // Real value < 100
        public void RoundTotal_VariousAmounts_ReturnsCorrectRounding(decimal input, decimal expected)
        {
            var result = DataHelper.RoundTotal(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(99.9999, "100.00")] // Rounds to 100.00, uses 2 decimals
        [InlineData(50.1235, "50.124")] // < 100, 3 decimals
        [InlineData(100.005, "100.01")] // >= 100, 2 decimals
        [InlineData(1000.125, "1000.13")] // >= 100, 2 decimals
        [InlineData(57759.20474748, "57759.20")] // Real value
        [InlineData(0, "0.000")] // Zero < 100, 3 decimals
        public void FormatTotal_VariousAmounts_ReturnsCorrectFormat(decimal input, string expected)
        {
            var result = DataHelper.FormatTotal(input);
            Assert.Equal(expected, result);
        }
    }
}
