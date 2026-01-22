using System.Collections.Generic;

namespace IbkrToEtax
{
    class EchSecurityDepot
    {
        public string DepotNumber { get; set; } = "";
        public List<EchSecurity> Securities { get; set; } = new();
    }
}
