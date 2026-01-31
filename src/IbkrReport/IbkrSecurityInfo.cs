using System.Xml.Linq;

public class IbkrSecurityInfo
{
    public string Currency { get; }
    public string AssetCategory { get; }
    public string SubCategory { get; }
    public string Symbol { get; }
    public string Description { get; }
    public string Isin { get; }
    public string IssuerCountryCode { get; }
    public IbkrSecurityInfo(XElement si)
    {
        Currency = (string?)si.Attribute("currency") ?? "";
        AssetCategory = (string?)si.Attribute("assetCategory") ?? "";
        SubCategory = (string?)si.Attribute("subCategory") ?? "";
        Symbol = (string?)si.Attribute("symbol") ?? "";
        Description = (string?)si.Attribute("description") ?? "";
        Isin = (string?)si.Attribute("isin") ?? "";
        IssuerCountryCode = (string?)si.Attribute("issuerCountryCode") ?? "";
    }
}