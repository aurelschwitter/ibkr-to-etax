using System;
using System.Collections.Generic;

namespace IbkrToEtax
{
    public class EchTaxStatement
    {
        public string Id { get; set; } = "";
        public DateTime CreationDate { get; set; } = DateTime.Now;
        public int TaxPeriod { get; set; }
        public DateTime PeriodFrom { get; set; }
        public DateTime PeriodTo { get; set; }
        public string Canton { get; set; } = "";
        public string Institution { get; set; } = "Interactive Brokers";
        public string ClientNumber { get; set; } = "";
        public List<EchSecurityDepot> Depots { get; set; } = new();


        public decimal GetTotalTaxValue() { return Depots.Sum(d => d.Securities.Sum(s => s.TaxValue?.Value ?? 0)); }
        public decimal GetTotalGrossRevenueA() { return Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.GrossRevenueA))); }
        public decimal GetTotalGrossRevenueB() { return Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.GrossRevenueB))); }
        public decimal GetTotalWithHoldingTaxClaim() { return Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.WithHoldingTaxClaim))); }
        public decimal GetTotalAdditionalWithHoldingTaxUSA() { return Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.AdditionalWithHoldingTaxUSA))); }
    }
}
