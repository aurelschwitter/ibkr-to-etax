using System;

namespace IbkrToEtax
{
    class EchTaxValue
    {
        public DateTime ReferenceDate { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Value { get; set; }
    }
}
