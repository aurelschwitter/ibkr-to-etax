using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace IbkrToEtax.IbkrReport
{
    public class IbkrOpenPosition
    {
        public string AccountId { get; private set; }
        public string Currency { get; private set; }
        public decimal FxRateToBase { get; private set; }
        public string AssetCategory { get; private set; }
        public string AssetSubCatgory { get; private set; }
        public string Symbol { get; private set; }

        public string Description { get; private set; }
        public string SecurityId { get; private set; }
        public string SecurityIdType { get; private set; }
        public string Isin { get; private set; }
        public string IssuerCountryCode { get; private set; }
        public DateTime ReportDate { get; private set; }
        public decimal Quantity { get; private set; }
        public decimal MarkPrice { get; private set; }
        public decimal PositionValue { get; private set; }
        public decimal PositionValueInBase { get; private set; }
        public decimal CostBasisPrice { get; private set; }
        public string LevelOfDetail { get; private set; }

        public IbkrOpenPosition(XElement op)
        {
            AccountId = (string?)op.Attribute("accountId") ?? "";
            Currency = (string?)op.Attribute("currency") ?? "";
            FxRateToBase = DataHelper.ParseDecimal((string?)op.Attribute("fxRateToBase") ?? "0");
            AssetCategory = (string?)op.Attribute("assetCategory") ?? "";
            AssetSubCatgory = (string?)op.Attribute("subCategory") ?? "";
            Symbol = (string?)op.Attribute("symbol") ?? "";
            Description = (string?)op.Attribute("description") ?? "";
            SecurityId = (string?)op.Attribute("securityID") ?? "";
            SecurityIdType = (string?)op.Attribute("securityIDType") ?? "";
            Isin = (string?)op.Attribute("isin") ?? "";
            IssuerCountryCode = (string?)op.Attribute("issuerCountryCode") ?? "";
            ReportDate = DateTime.Parse((string?)op.Attribute("reportDate") ?? DateTime.Now.ToString());
            Quantity = DataHelper.ParseDecimal((string?)op.Attribute("position") ?? "0");
            MarkPrice = DataHelper.ParseDecimal((string?)op.Attribute("markPrice") ?? "0");
            PositionValue = DataHelper.ParseDecimal((string?)op.Attribute("positionValue") ?? "0");
            PositionValueInBase = DataHelper.ParseDecimal((string?)op.Attribute("positionValueInBase") ?? "0");
            CostBasisPrice = DataHelper.ParseDecimal((string?)op.Attribute("costBasisPrice") ?? "0");
            LevelOfDetail = (string?)op.Attribute("levelOfDetail") ?? "";
        }

        public decimal GetPositionValue(ILogger _logger)
        {

            bool hasPositionValueInBase = PositionValueInBase != 0;
            bool hasPositionValue = PositionValue != 0;
            bool hasFxRateToBase = FxRateToBase != 0;

            if (hasPositionValueInBase)
            {
                // if positionValueInBase exists, use it directly
                return PositionValueInBase;
            }
            else if (hasPositionValue && hasFxRateToBase)
            {
                // if both positionValue and fxRateToBase exist, calculate positionValueInBase
                decimal positionValue = PositionValue;
                decimal fxRateToBase = FxRateToBase;
                decimal positionValueInBase = positionValue * fxRateToBase;
                return positionValueInBase;
            }
            else
            {
                _logger.LogWarning("Position of symbol {Symbol}, isin {isin} is missing position value information.",
                                   Symbol ?? "",
                                   Isin ?? "");
                return 0;
            }
        }
    }
}