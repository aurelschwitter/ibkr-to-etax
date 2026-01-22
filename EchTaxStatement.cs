using System;
using System.Collections.Generic;

namespace IbkrToEtax
{
    class EchTaxStatement
    {
        public string Id { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;
        public int TaxPeriod { get; set; }
        public DateTime PeriodFrom { get; set; }
        public DateTime PeriodTo { get; set; }
        public string Canton { get; set; }
        public string Institution { get; set; } = "Interactive Brokers";
        public string ClientNumber { get; set; }
        public List<EchSecurityDepot> Depots { get; set; } = new();
    }
}
