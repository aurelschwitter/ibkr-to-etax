using System;
using IbkrToEtax;

namespace IbkrToEtax
{
    /// <summary>
    /// Example usage of PdfValidator to validate eCH-0196 compliant PDFs
    /// </summary>
    public class ValidatorExample
    {
        public static void ValidatePdfExample(string pdfPath, string? xsdPath = null)
        {
            Console.WriteLine("=== eCH-0196 PDF Validation Example ===\n");

            // Validate the PDF
            var result = PdfValidator.ValidatePdf(pdfPath, xsdPath);

            // Display validation results
            Console.WriteLine("\n=== Validation Results ===");
            Console.WriteLine($"PDF Path: {pdfPath}");
            Console.WriteLine($"Is Valid: {result.IsValid}");

            // Display metadata
            if (result.Metadata.Count > 0)
            {
                Console.WriteLine("\nMetadata:");
                foreach (var kvp in result.Metadata)
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
            }

            // Display errors
            if (result.Errors.Count > 0)
            {
                Console.WriteLine($"\nErrors ({result.Errors.Count}):");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"  ✗ {error}");
                }
            }

            // Display warnings
            if (result.Warnings.Count > 0)
            {
                Console.WriteLine($"\nWarnings ({result.Warnings.Count}):");
                foreach (var warning in result.Warnings)
                {
                    Console.WriteLine($"  ⚠ {warning}");
                }
            }

            // Display extracted XML preview
            if (!string.IsNullOrEmpty(result.ExtractedXml))
            {
                Console.WriteLine("\nExtracted XML (first 500 chars):");
                string preview = result.ExtractedXml.Length > 500 
                    ? result.ExtractedXml.Substring(0, 500) + "..." 
                    : result.ExtractedXml;
                Console.WriteLine(preview);

                // Optionally save the extracted XML
                string extractedXmlPath = pdfPath.Replace(".pdf", "-extracted.xml");
                System.IO.File.WriteAllText(extractedXmlPath, result.ExtractedXml);
                Console.WriteLine($"\n✓ Extracted XML saved to: {extractedXmlPath}");
            }

            Console.WriteLine("\n=========================");
        }

        /// <summary>
        /// Example: Validate a PDF and return simple boolean result
        /// </summary>
        public static bool QuickValidate(string pdfPath)
        {
            var result = PdfValidator.ValidatePdf(pdfPath);
            return result.IsValid;
        }

        /// <summary>
        /// Example: Validate a PDF with XSD schema validation
        /// </summary>
        public static bool ValidateWithSchema(string pdfPath, string xsdPath)
        {
            if (!System.IO.File.Exists(xsdPath))
            {
                Console.WriteLine($"Warning: XSD file not found at {xsdPath}");
                Console.WriteLine("Validation will proceed without schema validation.");
            }

            var result = PdfValidator.ValidatePdf(pdfPath, xsdPath);
            
            if (result.IsValid)
            {
                Console.WriteLine("✓ PDF is valid and conforms to eCH-0196 standard");
                return true;
            }
            else
            {
                Console.WriteLine($"✗ PDF validation failed with {result.Errors.Count} error(s)");
                return false;
            }
        }

        /// <summary>
        /// Example: Batch validate multiple PDFs
        /// </summary>
        public static void ValidateMultiplePdfs(string[] pdfPaths, string? xsdPath = null)
        {
            Console.WriteLine($"Validating {pdfPaths.Length} PDF(s)...\n");

            int validCount = 0;
            int invalidCount = 0;

            foreach (var pdfPath in pdfPaths)
            {
                Console.WriteLine($"Validating: {System.IO.Path.GetFileName(pdfPath)}");
                
                if (!System.IO.File.Exists(pdfPath))
                {
                    Console.WriteLine($"  ✗ File not found\n");
                    invalidCount++;
                    continue;
                }

                var result = PdfValidator.ValidatePdf(pdfPath, xsdPath);
                
                if (result.IsValid)
                {
                    Console.WriteLine($"  ✓ Valid\n");
                    validCount++;
                }
                else
                {
                    Console.WriteLine($"  ✗ Invalid ({result.Errors.Count} error(s))\n");
                    invalidCount++;
                }
            }

            Console.WriteLine($"Summary: {validCount} valid, {invalidCount} invalid");
        }
    }
}
