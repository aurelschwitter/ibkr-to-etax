using System.Xml.Linq;

namespace IbkrToEtax.IbkrReport
{
    public class IbkrCashTransaction : IIbkrForeignCashValueElement
    {
        public string AccountId { get; private set; }
        public string AcctAlias { get; private set; }
        public string Model { get; private set; }
        public string Currency { get; private set; }
        public decimal FxRateToBase { get; private set; }
        public string AssetCategory { get; private set; }
        public string SubCategory { get; private set; }
        public string Symbol { get; private set; }
        public string Description { get; private set; }
        public string Conid { get; private set; }
        public string SecurityId { get; private set; }
        public string SecurityIdType { get; private set; }
        public string Cusip { get; private set; }
        public string Isin { get; private set; }
        public string Figi { get; private set; }
        public string ListingExchange { get; private set; }
        public string UnderlyingConid { get; private set; }
        public string UnderlyingSymbol { get; private set; }
        public string UnderlyingSecurityId { get; private set; }
        public string UnderlyingListingExchange { get; private set; }
        public string Issuer { get; private set; }
        public string IssuerCountryCode { get; private set; }
        public int Multiplier { get; private set; }
        public string Strike { get; private set; }
        public string Expiry { get; private set; }
        public string PutCall { get; private set; }
        public string PrincipalAdjustFactor { get; private set; }
        public DateTime DateTime { get; private set; }
        public DateTime SettleDate { get; private set; }
        public DateTime AvailableForTradingDate { get; private set; }
        public decimal Amount { get; private set; }
        public string Type { get; private set; }
        public string TradeId { get; private set; }
        public string Code { get; private set; }
        public string TransactionId { get; private set; }
        public DateTime ReportDate { get; private set; }
        public DateTime? ExDate { get; private set; }
        public string ClientReference { get; private set; }
        public string ActionId { get; private set; }
        public string LevelOfDetail { get; private set; }
        public string SerialNumber { get; private set; }
        public string DeliveryType { get; private set; }
        public string CommodityType { get; private set; }
        public decimal Fineness { get; private set; }
        public decimal Weight { get; private set; }

        public IbkrCashTransaction(XElement transaction)
        {
            AccountId = (string?)transaction.Attribute("accountId") ?? "";
            AcctAlias = (string?)transaction.Attribute("acctAlias") ?? "";
            Model = (string?)transaction.Attribute("model") ?? "";
            Currency = (string?)transaction.Attribute("currency") ?? "";
            FxRateToBase = DataHelper.ParseDecimal((string?)transaction.Attribute("fxRateToBase") ?? "0");
            AssetCategory = (string?)transaction.Attribute("assetCategory") ?? "";
            SubCategory = (string?)transaction.Attribute("subCategory") ?? "";
            Symbol = (string?)transaction.Attribute("symbol") ?? "";
            Description = (string?)transaction.Attribute("description") ?? "";
            Conid = (string?)transaction.Attribute("conid") ?? "";
            SecurityId = (string?)transaction.Attribute("securityID") ?? "";
            SecurityIdType = (string?)transaction.Attribute("securityIDType") ?? "";
            Cusip = (string?)transaction.Attribute("cusip") ?? "";
            Isin = (string?)transaction.Attribute("isin") ?? "";
            Figi = (string?)transaction.Attribute("figi") ?? "";
            ListingExchange = (string?)transaction.Attribute("listingExchange") ?? "";
            UnderlyingConid = (string?)transaction.Attribute("underlyingConid") ?? "";
            UnderlyingSymbol = (string?)transaction.Attribute("underlyingSymbol") ?? "";
            UnderlyingSecurityId = (string?)transaction.Attribute("underlyingSecurityID") ?? "";
            UnderlyingListingExchange = (string?)transaction.Attribute("underlyingListingExchange") ?? "";
            Issuer = (string?)transaction.Attribute("issuer") ?? "";
            IssuerCountryCode = (string?)transaction.Attribute("issuerCountryCode") ?? "";
            Multiplier = int.TryParse((string?)transaction.Attribute("multiplier"), out var mult) ? mult : 0;
            Strike = (string?)transaction.Attribute("strike") ?? "";
            Expiry = (string?)transaction.Attribute("expiry") ?? "";
            PutCall = (string?)transaction.Attribute("putCall") ?? "";
            PrincipalAdjustFactor = (string?)transaction.Attribute("principalAdjustFactor") ?? "";
            DateTime = ParseDate((string?)transaction.Attribute("dateTime") ?? "");
            SettleDate = ParseDate((string?)transaction.Attribute("settleDate") ?? "");
            AvailableForTradingDate = ParseDate((string?)transaction.Attribute("availableForTradingDate") ?? "");
            Amount = DataHelper.ParseDecimal((string?)transaction.Attribute("amount") ?? "0");
            Type = (string?)transaction.Attribute("type") ?? "";
            TradeId = (string?)transaction.Attribute("tradeID") ?? "";
            Code = (string?)transaction.Attribute("code") ?? "";
            TransactionId = (string?)transaction.Attribute("transactionID") ?? "";
            ReportDate = ParseDate((string?)transaction.Attribute("reportDate") ?? "");
            ExDate = DataHelper.ParseNullableDate((string?)transaction.Attribute("exDate"));
            ClientReference = (string?)transaction.Attribute("clientReference") ?? "";
            ActionId = (string?)transaction.Attribute("actionID") ?? "";
            LevelOfDetail = (string?)transaction.Attribute("levelOfDetail") ?? "";
            SerialNumber = (string?)transaction.Attribute("serialNumber") ?? "";
            DeliveryType = (string?)transaction.Attribute("deliveryType") ?? "";
            CommodityType = (string?)transaction.Attribute("commodityType") ?? "";
            Fineness = DataHelper.ParseDecimal((string?)transaction.Attribute("fineness") ?? "0");
            Weight = DataHelper.ParseDecimal((string?)transaction.Attribute("weight") ?? "0");
        }

        private static DateTime ParseDate(string date)
        {
            if (string.IsNullOrEmpty(date))
                return System.DateTime.MinValue;

            try
            {
                return System.DateTime.Parse(date);
            }
            catch
            {
                return System.DateTime.MinValue;
            }
        }
    }
}
