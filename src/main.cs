using System;
using System.Linq;
using System.Xml.Linq;
using CommandLine;

namespace IbkrToEtax
{
    class Program
    {
        // Command-line option classes
        [Verb("convert", HelpText = "Convert IBKR XML export to eCH-0196 format (XML + PDF)")]
        class ConvertOptions
        {
            [Value(0, Required = true, MetaName = "file", HelpText = "IBKR XML export file to convert")]
            public string InputFile { get; set; } = "";
        }

        [Verb("validate", HelpText = "Validate eCH-0196 PDF and extract embedded XML")]
        class ValidateOptions
        {
            [Value(0, Required = true, MetaName = "file", HelpText = "eCH-0196 PDF file to validate")]
            public string InputFile { get; set; } = "";

            [Option('s', "schema", Required = false, HelpText = "XSD schema file for validation (auto-detects eCH-0196-2-2.xsd if not specified)")]
            public string? SchemaFile { get; set; }
        }

        static int Main(string[] args)
        {
            // Parse with CommandLineParser
            return Parser.Default.ParseArguments<ConvertOptions, ValidateOptions>(args)
                .MapResult(
                    (ConvertOptions opts) => RunConvert(opts),
                    (ValidateOptions opts) => RunValidate(opts),
                    errs => 1);
        }

        static int RunConvert(ConvertOptions opts)
        {
            if (!File.Exists(opts.InputFile))
            {
                Console.WriteLine($"Error: File not found: {opts.InputFile}");
                return 2;
            }
            return ConvertIbkrToEch(opts.InputFile, opts.InputFile.Replace(".xml", ".output.xml"), opts.InputFile.Replace(".xml", ".output.pdf"));
        }

        static int RunValidate(ValidateOptions opts)
        {
            if (!File.Exists(opts.InputFile))
            {
                Console.WriteLine($"Error: File not found: {opts.InputFile}");
                return 2;
            }
            return ValidateEchPdf(opts.InputFile, opts.SchemaFile);
        }

        static int ConvertIbkrToEch(string inputFilePath, string outputXmlPath, string outputPdfPath)
        {
            Console.WriteLine("=== IBKR to eCH-0196 Converter ===");
            Console.WriteLine();

            if (!File.Exists(inputFilePath))
            {
                Console.WriteLine($"Error: File not found: {inputFilePath}");
                return 2;
            }

            try
            {
                Console.WriteLine($"Loading {inputFilePath}...");
                var doc = XDocument.Load(inputFilePath);

                // Extract account ID from XML
                var accountInfo = doc.Descendants("AccountInformation").FirstOrDefault();
                string accountId = accountInfo?.Attribute("accountId")?.Value ?? "";

                // Extract canton from state attribute (format: "CH-ZH")
                string state = accountInfo?.Attribute("state")?.Value ?? "";
                string canton = "ZH"; // Default canton
                if (!string.IsNullOrEmpty(state) && state.StartsWith("CH-"))
                {
                    canton = state[3..]; // Extract "ZH" from "CH-ZH"
                }

                // check that there is a account id in the xml
                if (string.IsNullOrEmpty(accountId))
                {
                    Console.WriteLine("Error: Account ID not found in XML.");
                    return 4;
                }

                // Extract date range from FlexStatements
                var flexStatements = doc.Descendants("FlexStatement").ToList();
                var (periodFrom, periodTo, taxYear) = IbkrDataParser.ExtractDateRange(flexStatements);

                // Parse and display IBKR data
                var (openPositions, trades, dividends, withholdingTax) = IbkrDataParser.ParseIbkrData(doc, accountId);
                IbkrDataParser.PrintDataLoadSummary(openPositions, trades, dividends, withholdingTax, accountInfo);

                // Build eCH tax statement
                var echStatement = EchStatementBuilder.BuildEchTaxStatement(openPositions, trades, dividends, withholdingTax, accountId, taxYear, periodFrom, periodTo, canton);

                // Display financial summary
                FinancialSummaryPrinter.PrintFinancialSummary(doc, dividends, withholdingTax, trades, accountId);

                // Generate output
                EchXmlGenerator.SaveAndDisplayOutput(echStatement, outputXmlPath, outputPdfPath);

                Console.WriteLine();
                Console.WriteLine("✓ Conversion completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 3;
            }
        }

        static int ValidateEchPdf(string pdfPath, string? xsdPath)
        {
            Console.WriteLine("=== eCH-0196 PDF Validator ===");
            Console.WriteLine();

            if (!File.Exists(pdfPath))
            {
                Console.WriteLine($"Error: File not found: {pdfPath}");
                return 2;
            }

            // Auto-detect XSD if not provided
            if (string.IsNullOrEmpty(xsdPath))
            {
                string[] possibleXsdPaths = {
                    "eCH-0196-2-2.xsd",
                    Path.Combine(Path.GetDirectoryName(pdfPath) ?? "", "eCH-0196-2-2.xsd")
                };

                foreach (var path in possibleXsdPaths)
                {
                    if (File.Exists(path))
                    {
                        xsdPath = path;
                        break;
                    }
                }
            }

            try
            {
                var result = PdfValidator.ValidatePdf(pdfPath, xsdPath);

                Console.WriteLine();
                Console.WriteLine("=== Validation Summary ===");
                Console.WriteLine($"PDF: {Path.GetFileName(pdfPath)}");
                Console.WriteLine($"Status: {(result.IsValid ? "✓ VALID" : "✗ INVALID")}");
                Console.WriteLine();

                // Display metadata
                if (result.Metadata.Count > 0)
                {
                    Console.WriteLine("Metadata:");
                    foreach (var kvp in result.Metadata)
                    {
                        Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                    }
                    Console.WriteLine();
                }

                // Display errors
                if (result.Errors.Count > 0)
                {
                    Console.WriteLine($"Errors ({result.Errors.Count}):");
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine($"  ✗ {error}");
                    }
                    Console.WriteLine();
                }

                // Display warnings
                if (result.Warnings.Count > 0)
                {
                    Console.WriteLine($"Warnings ({result.Warnings.Count}):");
                    foreach (var warning in result.Warnings)
                    {
                        Console.WriteLine($"  ⚠ {warning}");
                    }
                    Console.WriteLine();
                }

                // Display extracted XML summary
                if (!string.IsNullOrEmpty(result.ExtractedXml))
                {
                    Console.WriteLine("Extracted XML Summary:");

                    try
                    {
                        var xmlDoc = XDocument.Parse(result.ExtractedXml);
                        var root = xmlDoc.Root;

                        if (root != null)
                        {
                            var ns = root.Name.Namespace;

                            // Extract key information
                            var taxPeriod = root.Attribute("taxPeriod")?.Value;
                            var periodFrom = root.Attribute("periodFrom")?.Value;
                            var periodTo = root.Attribute("periodTo")?.Value;
                            var canton = root.Attribute("canton")?.Value;
                            var totalTaxValue = root.Attribute("totalTaxValue")?.Value;
                            var totalGrossRevenueB = root.Attribute("totalGrossRevenueB")?.Value;
                            var totalWithHoldingTaxClaim = root.Attribute("totalWithHoldingTaxClaim")?.Value;

                            Console.WriteLine($"  Tax Period: {taxPeriod}");
                            Console.WriteLine($"  Period: {periodFrom} to {periodTo}");
                            Console.WriteLine($"  Canton: {canton}");
                            Console.WriteLine($"  Total Tax Value: {totalTaxValue} CHF");
                            Console.WriteLine($"  Total Gross Revenue: {totalGrossRevenueB} CHF");
                            Console.WriteLine($"  Total Withholding Tax Claim: {totalWithHoldingTaxClaim} CHF");

                            // Count securities
                            var securities = xmlDoc.Descendants(ns + "security").Count();
                            var payments = xmlDoc.Descendants(ns + "payment").Count();
                            var stocks = xmlDoc.Descendants(ns + "stock").Count();

                            Console.WriteLine($"  Securities: {securities}");
                            Console.WriteLine($"  Payments: {payments}");
                            Console.WriteLine($"  Stock Mutations: {stocks}");
                        }
                    }
                    catch
                    {
                        Console.WriteLine($"  XML Length: {result.ExtractedXml.Length} characters");
                    }

                    // Optionally save the extracted XML
                    string extractedXmlPath = pdfPath.Replace(".pdf", "-extracted.xml");
                    File.WriteAllText(extractedXmlPath, result.ExtractedXml);
                    Console.WriteLine();
                    Console.WriteLine($"✓ Extracted XML saved to: {extractedXmlPath}");
                }

                Console.WriteLine();
                Console.WriteLine("=========================");

                return result.IsValid ? 0 : 3;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 3;
            }
        }
    }
}
