using System;

namespace IbkrToEtax
{
    public class EchPayment
    {
        public DateTime PaymentDate { get; set; }
        public DateTime? ExDate { get; set; }
        public decimal Quantity { get; set; }
        public decimal Amount { get; set; }
        public decimal GrossRevenueA { get; set; }
        public decimal GrossRevenueB { get; set; }
        public decimal WithHoldingTaxClaim { get; set; }
    }
}
