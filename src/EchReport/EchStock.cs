using System;

namespace IbkrToEtax
{
    public class EchStock
    {
        public DateTime ReferenceDate { get; set; }
        public bool IsMutation { get; set; }
        public string? Name { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Value { get; set; }
    }
}
