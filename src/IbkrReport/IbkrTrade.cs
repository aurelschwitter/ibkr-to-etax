using System.Xml.Linq;

namespace IbkrToEtax.IbkrReport
{
    public class IbkrTrade
    {
        public string AccountId { get; private set; }
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
        public string IssuerCountryCode { get; private set; }
        public string TradeId { get; private set; }
        public int Multiplier { get; private set; }
        public DateTime ReportDate { get; private set; }
        public DateTime TradeDateTime { get; private set; }
        public DateTime TradeDate { get; private set; }
        public string SettleDateTarget { get; private set; }
        public string TransactionType { get; private set; }
        public string Exchange { get; private set; }
        public decimal Quantity { get; private set; }
        public decimal TradePrice { get; private set; }
        public decimal TradeMoney { get; private set; }
        public decimal Proceeds { get; private set; }
        public decimal Taxes { get; private set; }
        public decimal IbCommission { get; private set; }
        public string IbCommissionCurrency { get; private set; }
        public decimal NetCash { get; private set; }
        public decimal NetCashInBase { get; private set; }
        public decimal ClosePrice { get; private set; }
        public string OpenCloseIndicator { get; private set; }
        public string Notes { get; private set; }
        public decimal Cost { get; private set; }
        public decimal FifoPnlRealized { get; private set; }
        public decimal CapitalGainsPnl { get; private set; }
        public decimal FxPnl { get; private set; }
        public decimal MtmPnl { get; private set; }
        public string BuySell { get; private set; }
        public string OrderType { get; private set; }
        public string LevelOfDetail { get; private set; }
        public string IbOrderId { get; private set; }
        public string TransactionId { get; private set; }
        public string IbExecId { get; private set; }
        public string BrokerageOrderId { get; private set; }
        public string ExchOrderId { get; private set; }
        public string ExtExecId { get; private set; }
        public string OrderTime { get; private set; }
        public bool IsApiOrder { get; private set; }

        public IbkrTrade(XElement trade)
        {
            AccountId = (string?)trade.Attribute("accountId") ?? "";
            Currency = (string?)trade.Attribute("currency") ?? "";
            FxRateToBase = DataHelper.ParseDecimal((string?)trade.Attribute("fxRateToBase") ?? "0");
            AssetCategory = (string?)trade.Attribute("assetCategory") ?? "";
            SubCategory = (string?)trade.Attribute("subCategory") ?? "";
            Symbol = (string?)trade.Attribute("symbol") ?? "";
            Description = (string?)trade.Attribute("description") ?? "";
            Conid = (string?)trade.Attribute("conid") ?? "";
            SecurityId = (string?)trade.Attribute("securityID") ?? "";
            SecurityIdType = (string?)trade.Attribute("securityIDType") ?? "";
            Cusip = (string?)trade.Attribute("cusip") ?? "";
            Isin = (string?)trade.Attribute("isin") ?? "";
            Figi = (string?)trade.Attribute("figi") ?? "";
            ListingExchange = (string?)trade.Attribute("listingExchange") ?? "";
            IssuerCountryCode = (string?)trade.Attribute("issuerCountryCode") ?? "";
            TradeId = (string?)trade.Attribute("tradeID") ?? "";
            Multiplier = int.Parse((string?)trade.Attribute("multiplier") ?? "1");
            ReportDate = DateTime.Parse((string?)trade.Attribute("reportDate") ?? DateTime.Now.ToString());
            TradeDateTime = ParseTradeDateTime((string?)trade.Attribute("dateTime") ?? "");
            TradeDate = DateTime.Parse((string?)trade.Attribute("tradeDate") ?? DateTime.Now.ToString());
            SettleDateTarget = (string?)trade.Attribute("settleDateTarget") ?? "";
            TransactionType = (string?)trade.Attribute("transactionType") ?? "";
            Exchange = (string?)trade.Attribute("exchange") ?? "";
            Quantity = DataHelper.ParseDecimal((string?)trade.Attribute("quantity") ?? "0");
            TradePrice = DataHelper.ParseDecimal((string?)trade.Attribute("tradePrice") ?? "0");
            TradeMoney = DataHelper.ParseDecimal((string?)trade.Attribute("tradeMoney") ?? "0");
            Proceeds = DataHelper.ParseDecimal((string?)trade.Attribute("proceeds") ?? "0");
            Taxes = DataHelper.ParseDecimal((string?)trade.Attribute("taxes") ?? "0");
            IbCommission = DataHelper.ParseDecimal((string?)trade.Attribute("ibCommission") ?? "0");
            IbCommissionCurrency = (string?)trade.Attribute("ibCommissionCurrency") ?? "";
            NetCash = DataHelper.ParseDecimal((string?)trade.Attribute("netCash") ?? "0");
            NetCashInBase = DataHelper.ParseDecimal((string?)trade.Attribute("netCashInBase") ?? "0");
            ClosePrice = DataHelper.ParseDecimal((string?)trade.Attribute("closePrice") ?? "0");
            OpenCloseIndicator = (string?)trade.Attribute("openCloseIndicator") ?? "";
            Notes = (string?)trade.Attribute("notes") ?? "";
            Cost = DataHelper.ParseDecimal((string?)trade.Attribute("cost") ?? "0");
            FifoPnlRealized = DataHelper.ParseDecimal((string?)trade.Attribute("fifoPnlRealized") ?? "0");
            CapitalGainsPnl = DataHelper.ParseDecimal((string?)trade.Attribute("capitalGainsPnl") ?? "0");
            FxPnl = DataHelper.ParseDecimal((string?)trade.Attribute("fxPnl") ?? "0");
            MtmPnl = DataHelper.ParseDecimal((string?)trade.Attribute("mtmPnl") ?? "0");
            BuySell = (string?)trade.Attribute("buySell") ?? "";
            OrderType = (string?)trade.Attribute("orderType") ?? "";
            LevelOfDetail = (string?)trade.Attribute("levelOfDetail") ?? "";
            IbOrderId = (string?)trade.Attribute("ibOrderID") ?? "";
            TransactionId = (string?)trade.Attribute("transactionID") ?? "";
            IbExecId = (string?)trade.Attribute("ibExecID") ?? "";
            BrokerageOrderId = (string?)trade.Attribute("brokerageOrderID") ?? "";
            ExchOrderId = (string?)trade.Attribute("exchOrderId") ?? "";
            ExtExecId = (string?)trade.Attribute("extExecID") ?? "";
            OrderTime = (string?)trade.Attribute("orderTime") ?? "";
            IsApiOrder = (string?)trade.Attribute("isAPIOrder") == "Y";
        }

        private static DateTime ParseTradeDateTime(string dateTime)
        {
            // IBKR format: "2025-01-07;09:56:16"
            if (string.IsNullOrEmpty(dateTime))
                return DateTime.MinValue;

            try
            {
                var parts = dateTime.Split(';');
                if (parts.Length == 2)
                {
                    var datePart = parts[0];
                    var timePart = parts[1];
                    return DateTime.Parse($"{datePart} {timePart}");
                }
                return DateTime.Parse(dateTime);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }
    }
}
