using System.Collections.Generic;

namespace IbkrToEtax
{
    class EchSecurity
    {
        public int PositionId { get; set; }
        public string Isin { get; set; }
        public string Country { get; set; }
        public string Currency { get; set; }
        public string SecurityCategory { get; set; }
        public string SecurityName { get; set; }
        public EchTaxValue TaxValue { get; set; }
        public List<EchPayment> Payments { get; set; } = new();
        public List<EchStock> Stocks { get; set; } = new();
    }
}
