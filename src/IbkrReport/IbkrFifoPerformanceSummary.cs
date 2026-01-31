using System.Xml.Linq;

namespace IbkrToEtax.IbkrReport
{
    public class IbkrFifoPerformanceSummary
    {
        public string AccountId { get; private set; } = "";
        public string AcctAlias { get; private set; } = "";
        public string Model { get; private set; } = "";
        public string AssetCategory { get; private set; } = "";
        public string SubCategory { get; private set; } = "";
        public string Symbol { get; private set; } = "";
        public string Description { get; private set; } = "";
        public string Conid { get; private set; } = "";
        public string SecurityId { get; private set; } = "";
        public string SecurityIdType { get; private set; } = "";
        public string Cusip { get; private set; } = "";
        public string Isin { get; private set; } = "";
        public string Figi { get; private set; } = "";
        public string ListingExchange { get; private set; } = "";
        public string UnderlyingConid { get; private set; } = "";
        public string UnderlyingSymbol { get; private set; } = "";
        public string UnderlyingSecurityId { get; private set; } = "";
        public string UnderlyingListingExchange { get; private set; } = "";
        public string Issuer { get; private set; } = "";
        public string IssuerCountryCode { get; private set; } = "";
        public int Multiplier { get; private set; }
        public string Strike { get; private set; } = "";
        public string Expiry { get; private set; } = "";
        public string PutCall { get; private set; } = "";
        public string PrincipalAdjustFactor { get; private set; } = "";
        public string ReportDate { get; private set; } = "";
        
        // Cost Adjustment
        public decimal CostAdj { get; private set; }
        
        // Realized Short-term
        public decimal RealizedSTProfit { get; private set; }
        public decimal RealizedSTLoss { get; private set; }
        
        // Realized Long-term
        public decimal RealizedLTProfit { get; private set; }
        public decimal RealizedLTLoss { get; private set; }
        
        // Total Realized
        public decimal TotalRealizedPnl { get; private set; }
        public decimal TotalRealizedCapitalGainsPnl { get; private set; }
        public decimal TotalRealizedFxPnl { get; private set; }
        
        // Unrealized
        public decimal UnrealizedProfit { get; private set; }
        public decimal UnrealizedLoss { get; private set; }
        
        // Unrealized Short-term
        public decimal UnrealizedSTProfit { get; private set; }
        public decimal UnrealizedSTLoss { get; private set; }
        
        // Unrealized Long-term
        public decimal UnrealizedLTProfit { get; private set; }
        public decimal UnrealizedLTLoss { get; private set; }
        
        // Total Unrealized
        public decimal TotalUnrealizedPnl { get; private set; }
        public decimal TotalUnrealizedCapitalGainsPnl { get; private set; }
        public decimal TotalUnrealizedFxPnl { get; private set; }
        
        // Total FIFO
        public decimal TotalFifoPnl { get; private set; }
        public decimal TotalCapitalGainsPnl { get; private set; }
        public decimal TotalFxPnl { get; private set; }
        
        // Transferred
        public decimal TransferredPnl { get; private set; }
        public decimal TransferredCapitalGainsPnl { get; private set; }
        public decimal TransferredFxPnl { get; private set; }
        
        // Additional fields
        public string Code { get; private set; } = "";
        public string SerialNumber { get; private set; } = "";
        public string DeliveryType { get; private set; } = "";
        public string CommodityType { get; private set; } = "";
        public decimal Fineness { get; private set; }
        public decimal Weight { get; private set; }

        public IbkrFifoPerformanceSummary(XElement element)
        {
            AccountId = (string?)element.Attribute("accountId") ?? "";
            AcctAlias = (string?)element.Attribute("acctAlias") ?? "";
            Model = (string?)element.Attribute("model") ?? "";
            AssetCategory = (string?)element.Attribute("assetCategory") ?? "";
            SubCategory = (string?)element.Attribute("subCategory") ?? "";
            Symbol = (string?)element.Attribute("symbol") ?? "";
            Description = (string?)element.Attribute("description") ?? "";
            Conid = (string?)element.Attribute("conid") ?? "";
            SecurityId = (string?)element.Attribute("securityID") ?? "";
            SecurityIdType = (string?)element.Attribute("securityIDType") ?? "";
            Cusip = (string?)element.Attribute("cusip") ?? "";
            Isin = (string?)element.Attribute("isin") ?? "";
            Figi = (string?)element.Attribute("figi") ?? "";
            ListingExchange = (string?)element.Attribute("listingExchange") ?? "";
            UnderlyingConid = (string?)element.Attribute("underlyingConid") ?? "";
            UnderlyingSymbol = (string?)element.Attribute("underlyingSymbol") ?? "";
            UnderlyingSecurityId = (string?)element.Attribute("underlyingSecurityID") ?? "";
            UnderlyingListingExchange = (string?)element.Attribute("underlyingListingExchange") ?? "";
            Issuer = (string?)element.Attribute("issuer") ?? "";
            IssuerCountryCode = (string?)element.Attribute("issuerCountryCode") ?? "";
            Multiplier = int.TryParse((string?)element.Attribute("multiplier"), out var mult) ? mult : 1;
            Strike = (string?)element.Attribute("strike") ?? "";
            Expiry = (string?)element.Attribute("expiry") ?? "";
            PutCall = (string?)element.Attribute("putCall") ?? "";
            PrincipalAdjustFactor = (string?)element.Attribute("principalAdjustFactor") ?? "";
            ReportDate = (string?)element.Attribute("reportDate") ?? "";
            
            // Cost Adjustment
            CostAdj = DataHelper.ParseDecimal((string?)element.Attribute("costAdj") ?? "0");
            
            // Realized Short-term
            RealizedSTProfit = DataHelper.ParseDecimal((string?)element.Attribute("realizedSTProfit") ?? "0");
            RealizedSTLoss = DataHelper.ParseDecimal((string?)element.Attribute("realizedSTLoss") ?? "0");
            
            // Realized Long-term
            RealizedLTProfit = DataHelper.ParseDecimal((string?)element.Attribute("realizedLTProfit") ?? "0");
            RealizedLTLoss = DataHelper.ParseDecimal((string?)element.Attribute("realizedLTLoss") ?? "0");
            
            // Total Realized
            TotalRealizedPnl = DataHelper.ParseDecimal((string?)element.Attribute("totalRealizedPnl") ?? "0");
            TotalRealizedCapitalGainsPnl = DataHelper.ParseDecimal((string?)element.Attribute("totalRealizedCapitalGainsPnl") ?? "0");
            TotalRealizedFxPnl = DataHelper.ParseDecimal((string?)element.Attribute("totalRealizedFxPnl") ?? "0");
            
            // Unrealized
            UnrealizedProfit = DataHelper.ParseDecimal((string?)element.Attribute("unrealizedProfit") ?? "0");
            UnrealizedLoss = DataHelper.ParseDecimal((string?)element.Attribute("unrealizedLoss") ?? "0");
            
            // Unrealized Short-term
            UnrealizedSTProfit = DataHelper.ParseDecimal((string?)element.Attribute("unrealizedSTProfit") ?? "0");
            UnrealizedSTLoss = DataHelper.ParseDecimal((string?)element.Attribute("unrealizedSTLoss") ?? "0");
            
            // Unrealized Long-term
            UnrealizedLTProfit = DataHelper.ParseDecimal((string?)element.Attribute("unrealizedLTProfit") ?? "0");
            UnrealizedLTLoss = DataHelper.ParseDecimal((string?)element.Attribute("unrealizedLTLoss") ?? "0");
            
            // Total Unrealized
            TotalUnrealizedPnl = DataHelper.ParseDecimal((string?)element.Attribute("totalUnrealizedPnl") ?? "0");
            TotalUnrealizedCapitalGainsPnl = DataHelper.ParseDecimal((string?)element.Attribute("totalUnrealizedCapitalGainsPnl") ?? "0");
            TotalUnrealizedFxPnl = DataHelper.ParseDecimal((string?)element.Attribute("totalUnrealizedFxPnl") ?? "0");
            
            // Total FIFO
            TotalFifoPnl = DataHelper.ParseDecimal((string?)element.Attribute("totalFifoPnl") ?? "0");
            TotalCapitalGainsPnl = DataHelper.ParseDecimal((string?)element.Attribute("totalCapitalGainsPnl") ?? "0");
            TotalFxPnl = DataHelper.ParseDecimal((string?)element.Attribute("totalFxPnl") ?? "0");
            
            // Transferred
            TransferredPnl = DataHelper.ParseDecimal((string?)element.Attribute("transferredPnl") ?? "0");
            TransferredCapitalGainsPnl = DataHelper.ParseDecimal((string?)element.Attribute("transferredCapitalGainsPnl") ?? "0");
            TransferredFxPnl = DataHelper.ParseDecimal((string?)element.Attribute("transferredFxPnl") ?? "0");
            
            // Additional fields
            Code = (string?)element.Attribute("code") ?? "";
            SerialNumber = (string?)element.Attribute("serialNumber") ?? "";
            DeliveryType = (string?)element.Attribute("deliveryType") ?? "";
            CommodityType = (string?)element.Attribute("commodityType") ?? "";
            Fineness = DataHelper.ParseDecimal((string?)element.Attribute("fineness") ?? "0");
            Weight = DataHelper.ParseDecimal((string?)element.Attribute("weight") ?? "0");
        }
    }
}
