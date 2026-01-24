using System;
using System.Linq;
using System.Xml.Linq;

namespace IbkrToEtax
{
    public static class EchXmlGenerator
    {
        public static XDocument GenerateEchXml(EchTaxStatement statement)
        {
            XNamespace ns = "http://www.ech.ch/xmlns/eCH-0196/2";

            var root = new XElement(ns + "taxStatement",
                new XAttribute("id", statement.Id),
                new XAttribute("creationDate", statement.CreationDate.ToString("yyyy-MM-ddTHH:mm:ss")),
                new XAttribute("taxPeriod", statement.TaxPeriod),
                new XAttribute("periodFrom", statement.PeriodFrom.ToString("yyyy-MM-dd")),
                new XAttribute("periodTo", statement.PeriodTo.ToString("yyyy-MM-dd")),
                new XAttribute("canton", statement.Canton),
                new XAttribute("totalTaxValue", statement.Depots.Sum(d => d.Securities.Sum(s => s.TaxValue?.Value ?? 0))),
                new XAttribute("totalGrossRevenueA", statement.Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.GrossRevenueA)))),
                new XAttribute("totalGrossRevenueB", statement.Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.GrossRevenueB)))),
                new XAttribute("totalWithHoldingTaxClaim", statement.Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.WithHoldingTaxClaim)))),
                new XAttribute("minorVersion", 0),

                new XElement(ns + "institution",
                    new XAttribute("name", statement.Institution)),

                new XElement(ns + "client",
                    new XAttribute("clientNumber", statement.ClientNumber)),

                new XElement(ns + "listOfSecurities",
                    new XAttribute("totalTaxValue", statement.Depots.Sum(d => d.Securities.Sum(s => s.TaxValue?.Value ?? 0))),
                    new XAttribute("totalGrossRevenueA", statement.Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.GrossRevenueA)))),
                    new XAttribute("totalGrossRevenueB", statement.Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.GrossRevenueB)))),
                    new XAttribute("totalWithHoldingTaxClaim", statement.Depots.Sum(d => d.Securities.Sum(s => s.Payments.Sum(p => p.WithHoldingTaxClaim)))),
                    new XAttribute("totalLumpSumTaxCredit", 0),
                    new XAttribute("totalNonRecoverableTax", 0),
                    new XAttribute("totalAdditionalWithHoldingTaxUSA", 0),
                    new XAttribute("totalGrossRevenueIUP", 0),
                    new XAttribute("totalGrossRevenueConversion", 0),

                    from depot in statement.Depots
                    select new XElement(ns + "depot",
                        new XAttribute("depotNumber", depot.DepotNumber),

                        from sec in depot.Securities
                        select new XElement(ns + "security",
                            new XAttribute("positionId", sec.PositionId),
                            string.IsNullOrEmpty(sec.Isin) ? null : new XAttribute("isin", sec.Isin),
                            new XAttribute("country", sec.Country),
                            new XAttribute("currency", sec.Currency),
                            new XAttribute("quotationType", "PIECE"),
                            new XAttribute("securityCategory", sec.SecurityCategory),
                            new XAttribute("securityName", sec.SecurityName),

                            sec.TaxValue == null ? null : new XElement(ns + "taxValue",
                                new XAttribute("referenceDate", sec.TaxValue.ReferenceDate.ToString("yyyy-MM-dd")),
                                new XAttribute("quotationType", "PIECE"),
                                new XAttribute("quantity", sec.TaxValue.Quantity),
                                new XAttribute("balanceCurrency", sec.Currency),
                                new XAttribute("unitPrice", sec.TaxValue.UnitPrice),
                                new XAttribute("value", sec.TaxValue.Value)),

                            from payment in sec.Payments
                            select new XElement(ns + "payment",
                                new XAttribute("paymentDate", payment.PaymentDate.ToString("yyyy-MM-dd")),
                                payment.ExDate.HasValue ? new XAttribute("exDate", payment.ExDate.Value.ToString("yyyy-MM-dd")) : null,
                                new XAttribute("quotationType", "PIECE"),
                                new XAttribute("quantity", payment.Quantity),
                                new XAttribute("amountCurrency", sec.Currency),
                                new XAttribute("amount", payment.Amount),
                                new XAttribute("grossRevenueA", payment.GrossRevenueA),
                                new XAttribute("grossRevenueB", payment.GrossRevenueB),
                                new XAttribute("withHoldingTaxClaim", payment.WithHoldingTaxClaim)),

                            from stock in sec.Stocks
                            select new XElement(ns + "stock",
                                new XAttribute("referenceDate", stock.ReferenceDate.ToString("yyyy-MM-dd")),
                                new XAttribute("mutation", stock.IsMutation ? "true" : "false"),
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
                new XDeclaration("1.0", "UTF-8", null),
                root
            );
        }

        public static void SaveAndDisplayOutput(EchTaxStatement statement)
        {
            var echXml = GenerateEchXml(statement);
            string outputPath = "eCH-0196-output.xml";
            echXml.Save(outputPath);

            var depot = statement.Depots.First();
            Console.WriteLine();
            Console.WriteLine($"✓ Generated eCH-0196 tax statement: {outputPath}");
            Console.WriteLine($"  - {depot.Securities.Count} securities");
            Console.WriteLine($"  - {depot.Securities.Sum(s => s.Stocks.Count)} stock mutations");
            Console.WriteLine($"  - {depot.Securities.Sum(s => s.Payments.Count)} dividend payments");

            // Generate PDF with barcodes
            try
            {
                string pdfPath = "eCH-0196-output.pdf";
                PdfBarcodeGenerator.GeneratePdfWithBarcodes(outputPath, pdfPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not generate PDF with barcodes: {ex.Message}");
            }
        }
    }
}
