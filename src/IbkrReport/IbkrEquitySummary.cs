using System.Xml.Linq;

namespace IbkrToEtax.IbkrReport
{
    public class IbkrEquitySummary
    {
        public string AccountId { get; private set; } = "";
        public string AcctAlias { get; private set; } = "";
        public string Model { get; private set; } = "";
        public string Currency { get; private set; } = "";
        public DateTime ReportDate { get; private set; }
        
        // Cash
        public decimal Cash { get; private set; }
        public decimal CashLong { get; private set; }
        public decimal CashShort { get; private set; }
        
        // Broker Cash Component
        public decimal BrokerCashComponent { get; private set; }
        public decimal BrokerCashComponentLong { get; private set; }
        public decimal BrokerCashComponentShort { get; private set; }
        
        // FDIC Insured Bank Sweep Account Cash Component
        public decimal FdicInsuredBankSweepAccountCashComponent { get; private set; }
        public decimal FdicInsuredBankSweepAccountCashComponentLong { get; private set; }
        public decimal FdicInsuredBankSweepAccountCashComponentShort { get; private set; }
        
        // Insured Bank Deposit Redemption Cash Component
        public decimal InsuredBankDepositRedemptionCashComponent { get; private set; }
        public decimal InsuredBankDepositRedemptionCashComponentLong { get; private set; }
        public decimal InsuredBankDepositRedemptionCashComponentShort { get; private set; }
        
        // SLB Cash Collateral
        public decimal SlbCashCollateral { get; private set; }
        public decimal SlbCashCollateralLong { get; private set; }
        public decimal SlbCashCollateralShort { get; private set; }
        
        // Stock
        public decimal Stock { get; private set; }
        public decimal StockLong { get; private set; }
        public decimal StockShort { get; private set; }
        
        // IPO Subscription
        public decimal IpoSubscription { get; private set; }
        public decimal IpoSubscriptionLong { get; private set; }
        public decimal IpoSubscriptionShort { get; private set; }
        
        // SLB Direct Securities Borrowed
        public decimal SlbDirectSecuritiesBorrowed { get; private set; }
        public decimal SlbDirectSecuritiesBorrowedLong { get; private set; }
        public decimal SlbDirectSecuritiesBorrowedShort { get; private set; }
        
        // SLB Direct Securities Lent
        public decimal SlbDirectSecuritiesLent { get; private set; }
        public decimal SlbDirectSecuritiesLentLong { get; private set; }
        public decimal SlbDirectSecuritiesLentShort { get; private set; }
        
        // Options
        public decimal Options { get; private set; }
        public decimal OptionsLong { get; private set; }
        public decimal OptionsShort { get; private set; }
        
        // Bonds
        public decimal Bonds { get; private set; }
        public decimal BondsLong { get; private set; }
        public decimal BondsShort { get; private set; }
        
        // Commodities
        public decimal Commodities { get; private set; }
        public decimal CommoditiesLong { get; private set; }
        public decimal CommoditiesShort { get; private set; }
        
        // Notes
        public decimal Notes { get; private set; }
        public decimal NotesLong { get; private set; }
        public decimal NotesShort { get; private set; }
        
        // Funds
        public decimal Funds { get; private set; }
        public decimal FundsLong { get; private set; }
        public decimal FundsShort { get; private set; }
        
        // Dividend Accruals
        public decimal DividendAccruals { get; private set; }
        public decimal DividendAccrualsLong { get; private set; }
        public decimal DividendAccrualsShort { get; private set; }
        
        // Lite Surcharge Accruals
        public decimal LiteSurchargeAccruals { get; private set; }
        public decimal LiteSurchargeAccrualsLong { get; private set; }
        public decimal LiteSurchargeAccrualsShort { get; private set; }
        
        // CGT Withholding Accruals
        public decimal CgtWithholdingAccruals { get; private set; }
        public decimal CgtWithholdingAccrualsLong { get; private set; }
        public decimal CgtWithholdingAccrualsShort { get; private set; }
        
        // Interest Accruals
        public decimal InterestAccruals { get; private set; }
        public decimal InterestAccrualsLong { get; private set; }
        public decimal InterestAccrualsShort { get; private set; }
        
        // Incentive Coupon Accruals
        public decimal IncentiveCouponAccruals { get; private set; }
        public decimal IncentiveCouponAccrualsLong { get; private set; }
        public decimal IncentiveCouponAccrualsShort { get; private set; }
        
        // Broker Interest Accruals Component
        public decimal BrokerInterestAccrualsComponent { get; private set; }
        public decimal BrokerInterestAccrualsComponentLong { get; private set; }
        public decimal BrokerInterestAccrualsComponentShort { get; private set; }
        
        // FDIC Insured Account Interest Accruals Component
        public decimal FdicInsuredAccountInterestAccrualsComponent { get; private set; }
        public decimal FdicInsuredAccountInterestAccrualsComponentLong { get; private set; }
        public decimal FdicInsuredAccountInterestAccrualsComponentShort { get; private set; }
        
        // Bond Interest Accruals Component
        public decimal BondInterestAccrualsComponent { get; private set; }
        public decimal BondInterestAccrualsComponentLong { get; private set; }
        public decimal BondInterestAccrualsComponentShort { get; private set; }
        
        // Broker Fees Accruals Component
        public decimal BrokerFeesAccrualsComponent { get; private set; }
        public decimal BrokerFeesAccrualsComponentLong { get; private set; }
        public decimal BrokerFeesAccrualsComponentShort { get; private set; }
        
        // Event Contract Interest Accruals
        public decimal EventContractInterestAccruals { get; private set; }
        public decimal EventContractInterestAccrualsLong { get; private set; }
        public decimal EventContractInterestAccrualsShort { get; private set; }
        
        // Margin Financing Charge Accruals
        public decimal MarginFinancingChargeAccruals { get; private set; }
        public decimal MarginFinancingChargeAccrualsLong { get; private set; }
        public decimal MarginFinancingChargeAccrualsShort { get; private set; }
        
        // Soft Dollars
        public decimal SoftDollars { get; private set; }
        public decimal SoftDollarsLong { get; private set; }
        public decimal SoftDollarsShort { get; private set; }
        
        // Forex CFD Unrealized PL
        public decimal ForexCfdUnrealizedPl { get; private set; }
        public decimal ForexCfdUnrealizedPlLong { get; private set; }
        public decimal ForexCfdUnrealizedPlShort { get; private set; }
        
        // CFD Unrealized PL
        public decimal CfdUnrealizedPl { get; private set; }
        public decimal CfdUnrealizedPlLong { get; private set; }
        public decimal CfdUnrealizedPlShort { get; private set; }
        
        // Physical Delivery
        public decimal PhysDel { get; private set; }
        public decimal PhysDelLong { get; private set; }
        public decimal PhysDelShort { get; private set; }
        
        // Crypto
        public decimal Crypto { get; private set; }
        public decimal CryptoLong { get; private set; }
        public decimal CryptoShort { get; private set; }
        
        // Total
        public decimal Total { get; private set; }
        public decimal TotalLong { get; private set; }
        public decimal TotalShort { get; private set; }

        public IbkrEquitySummary(XElement element)
        {
            AccountId = (string?)element.Attribute("accountId") ?? "";
            AcctAlias = (string?)element.Attribute("acctAlias") ?? "";
            Model = (string?)element.Attribute("model") ?? "";
            Currency = (string?)element.Attribute("currency") ?? "";
            ReportDate = ParseDate((string?)element.Attribute("reportDate") ?? "");
            
            // Cash
            Cash = DataHelper.ParseDecimal((string?)element.Attribute("cash") ?? "0");
            CashLong = DataHelper.ParseDecimal((string?)element.Attribute("cashLong") ?? "0");
            CashShort = DataHelper.ParseDecimal((string?)element.Attribute("cashShort") ?? "0");
            
            // Broker Cash Component
            BrokerCashComponent = DataHelper.ParseDecimal((string?)element.Attribute("brokerCashComponent") ?? "0");
            BrokerCashComponentLong = DataHelper.ParseDecimal((string?)element.Attribute("brokerCashComponentLong") ?? "0");
            BrokerCashComponentShort = DataHelper.ParseDecimal((string?)element.Attribute("brokerCashComponentShort") ?? "0");
            
            // FDIC Insured Bank Sweep Account Cash Component
            FdicInsuredBankSweepAccountCashComponent = DataHelper.ParseDecimal((string?)element.Attribute("fdicInsuredBankSweepAccountCashComponent") ?? "0");
            FdicInsuredBankSweepAccountCashComponentLong = DataHelper.ParseDecimal((string?)element.Attribute("fdicInsuredBankSweepAccountCashComponentLong") ?? "0");
            FdicInsuredBankSweepAccountCashComponentShort = DataHelper.ParseDecimal((string?)element.Attribute("fdicInsuredBankSweepAccountCashComponentShort") ?? "0");
            
            // Insured Bank Deposit Redemption Cash Component
            InsuredBankDepositRedemptionCashComponent = DataHelper.ParseDecimal((string?)element.Attribute("insuredBankDepositRedemptionCashComponent") ?? "0");
            InsuredBankDepositRedemptionCashComponentLong = DataHelper.ParseDecimal((string?)element.Attribute("insuredBankDepositRedemptionCashComponentLong") ?? "0");
            InsuredBankDepositRedemptionCashComponentShort = DataHelper.ParseDecimal((string?)element.Attribute("insuredBankDepositRedemptionCashComponentShort") ?? "0");
            
            // SLB Cash Collateral
            SlbCashCollateral = DataHelper.ParseDecimal((string?)element.Attribute("slbCashCollateral") ?? "0");
            SlbCashCollateralLong = DataHelper.ParseDecimal((string?)element.Attribute("slbCashCollateralLong") ?? "0");
            SlbCashCollateralShort = DataHelper.ParseDecimal((string?)element.Attribute("slbCashCollateralShort") ?? "0");
            
            // Stock
            Stock = DataHelper.ParseDecimal((string?)element.Attribute("stock") ?? "0");
            StockLong = DataHelper.ParseDecimal((string?)element.Attribute("stockLong") ?? "0");
            StockShort = DataHelper.ParseDecimal((string?)element.Attribute("stockShort") ?? "0");
            
            // IPO Subscription
            IpoSubscription = DataHelper.ParseDecimal((string?)element.Attribute("ipoSubscription") ?? "0");
            IpoSubscriptionLong = DataHelper.ParseDecimal((string?)element.Attribute("ipoSubscriptionLong") ?? "0");
            IpoSubscriptionShort = DataHelper.ParseDecimal((string?)element.Attribute("ipoSubscriptionShort") ?? "0");
            
            // SLB Direct Securities Borrowed
            SlbDirectSecuritiesBorrowed = DataHelper.ParseDecimal((string?)element.Attribute("slbDirectSecuritiesBorrowed") ?? "0");
            SlbDirectSecuritiesBorrowedLong = DataHelper.ParseDecimal((string?)element.Attribute("slbDirectSecuritiesBorrowedLong") ?? "0");
            SlbDirectSecuritiesBorrowedShort = DataHelper.ParseDecimal((string?)element.Attribute("slbDirectSecuritiesBorrowedShort") ?? "0");
            
            // SLB Direct Securities Lent
            SlbDirectSecuritiesLent = DataHelper.ParseDecimal((string?)element.Attribute("slbDirectSecuritiesLent") ?? "0");
            SlbDirectSecuritiesLentLong = DataHelper.ParseDecimal((string?)element.Attribute("slbDirectSecuritiesLentLong") ?? "0");
            SlbDirectSecuritiesLentShort = DataHelper.ParseDecimal((string?)element.Attribute("slbDirectSecuritiesLentShort") ?? "0");
            
            // Options
            Options = DataHelper.ParseDecimal((string?)element.Attribute("options") ?? "0");
            OptionsLong = DataHelper.ParseDecimal((string?)element.Attribute("optionsLong") ?? "0");
            OptionsShort = DataHelper.ParseDecimal((string?)element.Attribute("optionsShort") ?? "0");
            
            // Bonds
            Bonds = DataHelper.ParseDecimal((string?)element.Attribute("bonds") ?? "0");
            BondsLong = DataHelper.ParseDecimal((string?)element.Attribute("bondsLong") ?? "0");
            BondsShort = DataHelper.ParseDecimal((string?)element.Attribute("bondsShort") ?? "0");
            
            // Commodities
            Commodities = DataHelper.ParseDecimal((string?)element.Attribute("commodities") ?? "0");
            CommoditiesLong = DataHelper.ParseDecimal((string?)element.Attribute("commoditiesLong") ?? "0");
            CommoditiesShort = DataHelper.ParseDecimal((string?)element.Attribute("commoditiesShort") ?? "0");
            
            // Notes
            Notes = DataHelper.ParseDecimal((string?)element.Attribute("notes") ?? "0");
            NotesLong = DataHelper.ParseDecimal((string?)element.Attribute("notesLong") ?? "0");
            NotesShort = DataHelper.ParseDecimal((string?)element.Attribute("notesShort") ?? "0");
            
            // Funds
            Funds = DataHelper.ParseDecimal((string?)element.Attribute("funds") ?? "0");
            FundsLong = DataHelper.ParseDecimal((string?)element.Attribute("fundsLong") ?? "0");
            FundsShort = DataHelper.ParseDecimal((string?)element.Attribute("fundsShort") ?? "0");
            
            // Dividend Accruals
            DividendAccruals = DataHelper.ParseDecimal((string?)element.Attribute("dividendAccruals") ?? "0");
            DividendAccrualsLong = DataHelper.ParseDecimal((string?)element.Attribute("dividendAccrualsLong") ?? "0");
            DividendAccrualsShort = DataHelper.ParseDecimal((string?)element.Attribute("dividendAccrualsShort") ?? "0");
            
            // Lite Surcharge Accruals
            LiteSurchargeAccruals = DataHelper.ParseDecimal((string?)element.Attribute("liteSurchargeAccruals") ?? "0");
            LiteSurchargeAccrualsLong = DataHelper.ParseDecimal((string?)element.Attribute("liteSurchargeAccrualsLong") ?? "0");
            LiteSurchargeAccrualsShort = DataHelper.ParseDecimal((string?)element.Attribute("liteSurchargeAccrualsShort") ?? "0");
            
            // CGT Withholding Accruals
            CgtWithholdingAccruals = DataHelper.ParseDecimal((string?)element.Attribute("cgtWithholdingAccruals") ?? "0");
            CgtWithholdingAccrualsLong = DataHelper.ParseDecimal((string?)element.Attribute("cgtWithholdingAccrualsLong") ?? "0");
            CgtWithholdingAccrualsShort = DataHelper.ParseDecimal((string?)element.Attribute("cgtWithholdingAccrualsShort") ?? "0");
            
            // Interest Accruals
            InterestAccruals = DataHelper.ParseDecimal((string?)element.Attribute("interestAccruals") ?? "0");
            InterestAccrualsLong = DataHelper.ParseDecimal((string?)element.Attribute("interestAccrualsLong") ?? "0");
            InterestAccrualsShort = DataHelper.ParseDecimal((string?)element.Attribute("interestAccrualsShort") ?? "0");
            
            // Incentive Coupon Accruals
            IncentiveCouponAccruals = DataHelper.ParseDecimal((string?)element.Attribute("incentiveCouponAccruals") ?? "0");
            IncentiveCouponAccrualsLong = DataHelper.ParseDecimal((string?)element.Attribute("incentiveCouponAccrualsLong") ?? "0");
            IncentiveCouponAccrualsShort = DataHelper.ParseDecimal((string?)element.Attribute("incentiveCouponAccrualsShort") ?? "0");
            
            // Broker Interest Accruals Component
            BrokerInterestAccrualsComponent = DataHelper.ParseDecimal((string?)element.Attribute("brokerInterestAccrualsComponent") ?? "0");
            BrokerInterestAccrualsComponentLong = DataHelper.ParseDecimal((string?)element.Attribute("brokerInterestAccrualsComponentLong") ?? "0");
            BrokerInterestAccrualsComponentShort = DataHelper.ParseDecimal((string?)element.Attribute("brokerInterestAccrualsComponentShort") ?? "0");
            
            // FDIC Insured Account Interest Accruals Component
            FdicInsuredAccountInterestAccrualsComponent = DataHelper.ParseDecimal((string?)element.Attribute("fdicInsuredAccountInterestAccrualsComponent") ?? "0");
            FdicInsuredAccountInterestAccrualsComponentLong = DataHelper.ParseDecimal((string?)element.Attribute("fdicInsuredAccountInterestAccrualsComponentLong") ?? "0");
            FdicInsuredAccountInterestAccrualsComponentShort = DataHelper.ParseDecimal((string?)element.Attribute("fdicInsuredAccountInterestAccrualsComponentShort") ?? "0");
            
            // Bond Interest Accruals Component
            BondInterestAccrualsComponent = DataHelper.ParseDecimal((string?)element.Attribute("bondInterestAccrualsComponent") ?? "0");
            BondInterestAccrualsComponentLong = DataHelper.ParseDecimal((string?)element.Attribute("bondInterestAccrualsComponentLong") ?? "0");
            BondInterestAccrualsComponentShort = DataHelper.ParseDecimal((string?)element.Attribute("bondInterestAccrualsComponentShort") ?? "0");
            
            // Broker Fees Accruals Component
            BrokerFeesAccrualsComponent = DataHelper.ParseDecimal((string?)element.Attribute("brokerFeesAccrualsComponent") ?? "0");
            BrokerFeesAccrualsComponentLong = DataHelper.ParseDecimal((string?)element.Attribute("brokerFeesAccrualsComponentLong") ?? "0");
            BrokerFeesAccrualsComponentShort = DataHelper.ParseDecimal((string?)element.Attribute("brokerFeesAccrualsComponentShort") ?? "0");
            
            // Event Contract Interest Accruals
            EventContractInterestAccruals = DataHelper.ParseDecimal((string?)element.Attribute("eventContractInterestAccruals") ?? "0");
            EventContractInterestAccrualsLong = DataHelper.ParseDecimal((string?)element.Attribute("eventContractInterestAccrualsLong") ?? "0");
            EventContractInterestAccrualsShort = DataHelper.ParseDecimal((string?)element.Attribute("eventContractInterestAccrualsShort") ?? "0");
            
            // Margin Financing Charge Accruals
            MarginFinancingChargeAccruals = DataHelper.ParseDecimal((string?)element.Attribute("marginFinancingChargeAccruals") ?? "0");
            MarginFinancingChargeAccrualsLong = DataHelper.ParseDecimal((string?)element.Attribute("marginFinancingChargeAccrualsLong") ?? "0");
            MarginFinancingChargeAccrualsShort = DataHelper.ParseDecimal((string?)element.Attribute("marginFinancingChargeAccrualsShort") ?? "0");
            
            // Soft Dollars
            SoftDollars = DataHelper.ParseDecimal((string?)element.Attribute("softDollars") ?? "0");
            SoftDollarsLong = DataHelper.ParseDecimal((string?)element.Attribute("softDollarsLong") ?? "0");
            SoftDollarsShort = DataHelper.ParseDecimal((string?)element.Attribute("softDollarsShort") ?? "0");
            
            // Forex CFD Unrealized PL
            ForexCfdUnrealizedPl = DataHelper.ParseDecimal((string?)element.Attribute("forexCfdUnrealizedPl") ?? "0");
            ForexCfdUnrealizedPlLong = DataHelper.ParseDecimal((string?)element.Attribute("forexCfdUnrealizedPlLong") ?? "0");
            ForexCfdUnrealizedPlShort = DataHelper.ParseDecimal((string?)element.Attribute("forexCfdUnrealizedPlShort") ?? "0");
            
            // CFD Unrealized PL
            CfdUnrealizedPl = DataHelper.ParseDecimal((string?)element.Attribute("cfdUnrealizedPl") ?? "0");
            CfdUnrealizedPlLong = DataHelper.ParseDecimal((string?)element.Attribute("cfdUnrealizedPlLong") ?? "0");
            CfdUnrealizedPlShort = DataHelper.ParseDecimal((string?)element.Attribute("cfdUnrealizedPlShort") ?? "0");
            
            // Physical Delivery
            PhysDel = DataHelper.ParseDecimal((string?)element.Attribute("physDel") ?? "0");
            PhysDelLong = DataHelper.ParseDecimal((string?)element.Attribute("physDelLong") ?? "0");
            PhysDelShort = DataHelper.ParseDecimal((string?)element.Attribute("physDelShort") ?? "0");
            
            // Crypto
            Crypto = DataHelper.ParseDecimal((string?)element.Attribute("crypto") ?? "0");
            CryptoLong = DataHelper.ParseDecimal((string?)element.Attribute("cryptoLong") ?? "0");
            CryptoShort = DataHelper.ParseDecimal((string?)element.Attribute("cryptoShort") ?? "0");
            
            // Total
            Total = DataHelper.ParseDecimal((string?)element.Attribute("total") ?? "0");
            TotalLong = DataHelper.ParseDecimal((string?)element.Attribute("totalLong") ?? "0");
            TotalShort = DataHelper.ParseDecimal((string?)element.Attribute("totalShort") ?? "0");
        }

        private static DateTime ParseDate(string date)
        {
            if (string.IsNullOrEmpty(date))
                return DateTime.MinValue;

            try
            {
                return DateTime.Parse(date);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }
    }
}
