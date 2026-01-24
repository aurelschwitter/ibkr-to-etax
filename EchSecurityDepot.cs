using System.Collections.Generic;

namespace IbkrToEtax
{
    public class EchSecurityDepot
    {
        public string DepotNumber { get; set; } = "";
        public List<EchSecurity> Securities { get; set; } = new();
    }
}
