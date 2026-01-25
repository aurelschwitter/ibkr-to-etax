using System;
using System.Linq;
using System.Xml.Linq;

namespace IbkrToEtax
{
    public static class EchXmlGenerator
    {
        public static XDocument GenerateEchXml(EchTaxStatement statement)
        {
            // Define namespaces as per eCH-0196 specification
            XNamespace ech0196 = "http://www.ech.ch/xmlns/eCH-0196/2";
            XNamespace ech0097 = "http://www.ech.ch/xmlns/eCH-0097/4";
            XNamespace ech0010 = "http://www.ech.ch/xmlns/eCH-0010/7";
            XNamespace ech0008 = "http://www.ech.ch/xmlns/eCH-0008/3";
            XNamespace ech0007 = "http://www.ech.ch/xmlns/eCH-0007/6";
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

            // Calculate totals (individual values remain unrounded, only totals are rounded per eCH-0196)
            decimal totalTaxValue = statement.Depots.Sum(d => d.Securities.Sum(s => s.TaxValue?.Value ?? 0));
            decimal totalGrossRevenueA = statement.Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.GrossRevenueA)));
            decimal totalGrossRevenueB = statement.Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.GrossRevenueB)));
            decimal totalWithHoldingTaxClaim = statement.Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.WithHoldingTaxClaim)));
            decimal totalAdditionalWithHoldingTaxUSA = statement.Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.AdditionalWithHoldingTaxUSA)));

            var root = new XElement(ech0196 + "taxStatement",
                // Namespace declarations
                new XAttribute("xmlns", ech0007),
                new XAttribute(XNamespace.Xmlns + "eCH-0010", ech0010),
                new XAttribute(XNamespace.Xmlns + "eCH-0008", ech0008),
                new XAttribute(XNamespace.Xmlns + "eCH-0097", ech0097),
                new XAttribute(XNamespace.Xmlns + "eCH-0196", ech0196),
                new XAttribute(XNamespace.Xmlns + "xsi", xsi),
                new XAttribute(xsi + "schemaLocation", "http://www.ech.ch/xmlns/eCH-0196/2"),

                // Attributes
                new XAttribute("id", statement.Id),
                new XAttribute("creationDate", statement.CreationDate.ToString("yyyy-MM-ddTHH:mm:ss.fff")),
                new XAttribute("taxPeriod", statement.TaxPeriod),
                new XAttribute("periodFrom", statement.PeriodFrom.ToString("yyyy-MM-dd")),
                new XAttribute("periodTo", statement.PeriodTo.ToString("yyyy-MM-dd")),
                new XAttribute("country", "CH"),
                new XAttribute("canton", statement.Canton),
                new XAttribute("totalTaxValue", DataHelper.FormatTotal(totalTaxValue)),
                new XAttribute("totalGrossRevenueA", DataHelper.FormatTotal(totalGrossRevenueA)),
                new XAttribute("totalGrossRevenueB", DataHelper.FormatTotal(totalGrossRevenueB)),
                new XAttribute("totalGrossRevenueBCanton", DataHelper.FormatTotal(totalGrossRevenueB)),
                new XAttribute("totalGrossRevenueACanton", DataHelper.FormatTotal(totalGrossRevenueA)),
                new XAttribute("totalWithHoldingTaxClaim", DataHelper.FormatTotal(totalWithHoldingTaxClaim)),
                new XAttribute("minorVersion", "21"),

                new XElement(ech0196 + "institution",
                    new XAttribute(XNamespace.Xmlns + "eCH-0196", ech0196),
                    new XAttribute("name", statement.Institution),
                    // Add UID element for Swiss institutions or foreign institutions with Swiss registration
                    // For Interactive Brokers, we add a placeholder since they operate in Switzerland
                    new XElement(ech0196 + "uid",
                        new XAttribute(XNamespace.Xmlns + "eCH-0196", ech0196),
                        new XAttribute(XNamespace.Xmlns + "eCH-0097", ech0097),
                        new XElement(ech0097 + "uidOrganisationIdCategorie", "CHE"),
                        new XElement(ech0097 + "uidOrganisationId", "999999999") // Placeholder for foreign institution
                    )
                ),

                new XElement(ech0196 + "client",
                    new XAttribute(XNamespace.Xmlns + "eCH-0196", ech0196),
                    new XAttribute("clientNumber", statement.ClientNumber)),

                new XElement(ech0196 + "listOfSecurities",
                    new XAttribute(XNamespace.Xmlns + "eCH-0196", ech0196),
                    new XAttribute("totalTaxValue", DataHelper.FormatTotal(totalTaxValue)),
                    new XAttribute("totalGrossRevenueA", DataHelper.FormatTotal(totalGrossRevenueA)),
                    new XAttribute("totalGrossRevenueB", DataHelper.FormatTotal(totalGrossRevenueB)),
                    new XAttribute("totalGrossRevenueBCanton", DataHelper.FormatTotal(totalGrossRevenueB)),
                    new XAttribute("totalGrossRevenueACanton", DataHelper.FormatTotal(totalGrossRevenueA)),
                    new XAttribute("totalWithHoldingTaxClaim", DataHelper.FormatTotal(totalWithHoldingTaxClaim)),
                    new XAttribute("totalLumpSumTaxCredit", "0.00"),
                    new XAttribute("totalNonRecoverableTax", "0.00"),
                    new XAttribute("totalAdditionalWithHoldingTaxUSA", DataHelper.FormatTotal(totalAdditionalWithHoldingTaxUSA)),
                    new XAttribute("totalGrossRevenueIUP", "0.00"),
                    new XAttribute("totalGrossRevenueConversion", "0.00"),

                    from depot in statement.Depots
                    select new XElement(ech0196 + "depot",
                        new XAttribute(XNamespace.Xmlns + "eCH-0196", ech0196),
                        new XAttribute("depotNumber", depot.DepotNumber),

                        from sec in depot.Securities
                        select new XElement(ech0196 + "security",
                            new XAttribute(XNamespace.Xmlns + "eCH-0196", ech0196),
                            new XAttribute("positionId", sec.PositionId),
                            string.IsNullOrEmpty(sec.Isin) ? null : new XAttribute("isin", sec.Isin),
                            new XAttribute("country", sec.Country),
                            new XAttribute("currency", sec.Currency),
                            new XAttribute("quotationType", "PIECE"),
                            new XAttribute("securityCategory", sec.SecurityCategory),
                            new XAttribute("securityName", sec.SecurityName),

                            sec.TaxValue == null ? null : new XElement(ech0196 + "taxValue",
                                new XAttribute(XNamespace.Xmlns + "eCH-0196", ech0196),
                                new XAttribute("referenceDate", sec.TaxValue.ReferenceDate.ToString("yyyy-MM-dd")),
                                new XAttribute("quotationType", "PIECE"),
                                new XAttribute("quantity", sec.TaxValue.Quantity),
                                new XAttribute("balanceCurrency", sec.Currency),
                                new XAttribute("unitPrice", sec.TaxValue.UnitPrice),
                                new XAttribute("value", sec.TaxValue.Value)),

                            from payment in sec.Payments
                            select new XElement(ech0196 + "payment",
                                new XAttribute(XNamespace.Xmlns + "eCH-0196", ech0196),
                                payment.Name != null ? new XAttribute("name", payment.Name) : null,
                                new XAttribute("paymentDate", payment.PaymentDate.ToString("yyyy-MM-dd")),
                                payment.ExDate.HasValue ? new XAttribute("exDate", payment.ExDate.Value.ToString("yyyy-MM-dd")) : null,
                                new XAttribute("quotationType", "PIECE"),
                                new XAttribute("quantity", payment.Quantity),
                                new XAttribute("amountCurrency", "CHF"),
                                new XAttribute("amount", payment.Amount),
                                new XAttribute("grossRevenueA", payment.GrossRevenueA),
                                new XAttribute("grossRevenueB", payment.GrossRevenueB),
                                new XAttribute("withHoldingTaxClaim", payment.WithHoldingTaxClaim),
                                payment.AdditionalWithHoldingTaxUSA > 0 ? new XAttribute("additionalWithHoldingTaxUSA", payment.AdditionalWithHoldingTaxUSA) : null),

                            from stock in sec.Stocks
                            select new XElement(ech0196 + "stock",
                                new XAttribute(XNamespace.Xmlns + "eCH-0196", ech0196),
                                stock.Name != null ? new XAttribute("name", stock.Name) : null,
                                new XAttribute("referenceDate", stock.ReferenceDate.ToString("yyyy-MM-dd")),
                                new XAttribute("mutation", stock.IsMutation ? "1" : "0"),
                                new XAttribute("quotationType", "PIECE"),
                                new XAttribute("quantity", stock.Quantity),
                                new XAttribute("balanceCurrency", sec.Currency),
                                new XAttribute("unitPrice", stock.UnitPrice),
                                new XAttribute("value", stock.Value))
                        )
                    )
                )
            );

            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                root
            );
        }

        public static void SaveAndDisplayOutput(EchTaxStatement statement, string outputXmlPath, string outputPdfPath)
        {
            var echXml = GenerateEchXml(statement);
            echXml.Save(outputXmlPath);

            var depot = statement.Depots.First();
            Console.WriteLine();
            Console.WriteLine($"✓ Generated eCH-0196 tax statement: {outputXmlPath}");
            Console.WriteLine($"  - {depot.Securities.Count} securities");
            Console.WriteLine($"  - {depot.Securities.Sum(s => s.Stocks.Count)} stock mutations");
            Console.WriteLine($"  - {depot.Securities.Sum(s => s.Payments.Count)} dividend payments");

            // Generate PDF with barcodes
            try
            {
                PdfBarcodeGenerator.GeneratePdfWithBarcodes(outputXmlPath, outputPdfPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not generate PDF with barcodes: {ex.Message}");
            }
        }
    }
}
