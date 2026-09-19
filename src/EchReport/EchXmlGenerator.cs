using System;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace IbkrToEtax
{
    public static class EchXmlGenerator
    {
        public static XDocument GenerateEchXml(EchTaxStatement statement)
        {
            // Define namespaces as per eCH-0196 specification. Real statements (see the
            // example in the eCH-0196 Technische Wegleitung) use eCH-0196 as the DEFAULT
            // namespace with an unprefixed root element.
            XNamespace ech0196 = "http://www.ech.ch/xmlns/eCH-0196/2";
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

            // Calculate totals (individual values remain unrounded, only totals are rounded per eCH-0196)
            string totalTaxValue = DataHelper.FormatTotal(statement.GetTotalTaxValue());
            string totalGrossRevenueA = DataHelper.FormatTotal(statement.GetTotalGrossRevenueA());
            string totalGrossRevenueB = DataHelper.FormatTotal(statement.GetTotalGrossRevenueB());
            string totalWithHoldingTaxClaim = DataHelper.FormatTotal(statement.GetTotalWithHoldingTaxClaim());
            string totalAdditionalWithHoldingTaxUSA = DataHelper.FormatTotal(statement.GetTotalAdditionalWithHoldingTaxUSA());

            var root = new XElement(ech0196 + "taxStatement",
                // Namespace declarations: eCH-0196 as default namespace, xsi:schemaLocation
                // with namespace/URL pairs, matching the official example statements
                new XAttribute("xmlns", ech0196),
                new XAttribute(XNamespace.Xmlns + "xsi", xsi),
                new XAttribute(xsi + "schemaLocation",
                    "http://www.ech.ch/xmlns/eCH-0196/2 http://www.ech.ch/xmlns/eCH-0196/2/eCH-0196-2-2.xsd"),

                // Attributes
                new XAttribute("id", statement.Id),
                new XAttribute("creationDate", statement.CreationDate.ToString("yyyy-MM-ddTHH:mm:ss")),
                new XAttribute("taxPeriod", statement.TaxPeriod),
                new XAttribute("periodFrom", statement.PeriodFrom.ToString("yyyy-MM-dd")),
                new XAttribute("periodTo", statement.PeriodTo.ToString("yyyy-MM-dd")),
                new XAttribute("country", "CH"),
                new XAttribute("canton", statement.Canton),
                new XAttribute("totalTaxValue", totalTaxValue),
                new XAttribute("totalGrossRevenueA", totalGrossRevenueA),
                new XAttribute("totalGrossRevenueB", totalGrossRevenueB),
                new XAttribute("totalGrossRevenueBCanton", totalGrossRevenueB),
                new XAttribute("totalGrossRevenueACanton", totalGrossRevenueA),
                new XAttribute("totalWithHoldingTaxClaim", totalWithHoldingTaxClaim),
                // Minor version of the eCH-0196-2-2 schema (its version attribute is "2")
                new XAttribute("minorVersion", "2"),

                // No uid element: it is optional per the schema and Interactive Brokers
                // has no Swiss UID - a placeholder would fail the UID checksum
                new XElement(ech0196 + "institution",
                    new XAttribute("name", statement.Institution)),

                new XElement(ech0196 + "client",
                    new XAttribute("clientNumber", statement.ClientNumber)),

                new XElement(ech0196 + "listOfSecurities",
                    new XAttribute("totalTaxValue", totalTaxValue),
                    new XAttribute("totalGrossRevenueA", totalGrossRevenueA),
                    new XAttribute("totalGrossRevenueB", totalGrossRevenueB),
                    new XAttribute("totalGrossRevenueBCanton", totalGrossRevenueB),
                    new XAttribute("totalGrossRevenueACanton", totalGrossRevenueA),
                    new XAttribute("totalWithHoldingTaxClaim", totalWithHoldingTaxClaim),
                    new XAttribute("totalLumpSumTaxCredit", "0.00"),
                    new XAttribute("totalNonRecoverableTax", "0.00"),
                    new XAttribute("totalAdditionalWithHoldingTaxUSA", totalAdditionalWithHoldingTaxUSA),
                    new XAttribute("totalGrossRevenueIUP", "0.00"),
                    new XAttribute("totalGrossRevenueConversion", "0.00"),

                    from depot in statement.Depots
                    select new XElement(ech0196 + "depot",
                        new XAttribute("depotNumber", depot.DepotNumber),

                        from sec in depot.Securities
                        select new XElement(ech0196 + "security",
                            new XAttribute("positionId", sec.PositionId),
                            string.IsNullOrEmpty(sec.Isin) ? null : new XAttribute("isin", sec.Isin),
                            new XAttribute("country", sec.Country),
                            new XAttribute("currency", sec.Currency),
                            new XAttribute("quotationType", "PIECE"),
                            new XAttribute("securityCategory", sec.SecurityCategory),
                            new XAttribute("securityName", sec.SecurityName),

                            sec.TaxValue == null ? null : new XElement(ech0196 + "taxValue",
                                new XAttribute("referenceDate", sec.TaxValue.ReferenceDate.ToString("yyyy-MM-dd")),
                                new XAttribute("quotationType", "PIECE"),
                                new XAttribute("quantity", sec.TaxValue.Quantity),
                                new XAttribute("balanceCurrency", sec.Currency),
                                new XAttribute("unitPrice", sec.TaxValue.UnitPrice),
                                new XAttribute("value", sec.TaxValue.Value)),

                            from payment in sec.Payments
                            select new XElement(ech0196 + "payment",
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

        public static void SaveAndDisplayOutput(ILogger logger, EchTaxStatement statement, string outputXmlPath, string outputPdfPath)
        {
            var echXml = GenerateEchXml(statement);
            echXml.Save(outputXmlPath);

            var depot = statement.Depots.First();
            logger?.LogInformation("âœ“ Generated eCH-0196 tax statement: {OutputXmlPath}", outputXmlPath);
            logger?.LogInformation("  - {SecurityCount} securities", depot.Securities.Count);
            logger?.LogInformation("  - {StockCount} stock mutations", depot.Securities.Sum(s => s.Stocks.Count));
            logger?.LogInformation("  - {PaymentCount} dividend payments", depot.Securities.Sum(s => s.Payments.Count));

            // Generate PDF with barcodes
            try
            {
                PdfBarcodeGenerator.GeneratePdfWithBarcodes(outputXmlPath, outputPdfPath);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Could not generate PDF with barcodes");
            }
        }
    }
}
