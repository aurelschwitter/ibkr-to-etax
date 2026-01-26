using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using ZXing;
using ZXing.Common;
using ZXing.SkiaSharp;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Font;
using iText.IO.Font.Constants;
using SkiaSharp;
using SharpCompress.Compressors.Deflate;
using ZXing.PDF417.Internal;

namespace IbkrToEtax
{
    public class PdfBarcodeGenerator
    {
        // eCH-0270 Requirements:
        // [MUSS] DEFLATE compression for XML data (zlib format)
        // [MUSS] EC-Level 4 (ISO/IEC 24728:2006)
        // [MUSS] 6 blocks, 13 columns, 35 rows per barcode (including last segment)
        // [MUSS] Element size: width 0.04-0.042 cm, height 0.08 cm
        // [MUSS] Image dimensions: 290 x 35 pixels → 12.18 cm x 2.8 cm
        // [MUSS] Print scaling: 97% → actual size = dimensions / 0.97
        // [MUSS] Margins: Top 5cm, Left/Right/Bottom 2cm
        // [MUSS] Spacing: min 1cm between segments (larger between 3-4 for fold)

        private const int MAX_PDF417_SIZE = 470; // Maximum bytes per PDF417 code segment (conservative for 13 cols, 35 rows, EC level 4)

        // PDF417 specifications as per eCH-0270
        private const int PDF417_COLUMNS = 13;
        private const int PDF417_ROWS = 35; // Must be 35 for ALL segments including the last
        private const PDF417ErrorCorrectionLevel PDF417_ERROR_CORRECTION = PDF417ErrorCorrectionLevel.L4; // EC-Level 4

        // Image dimensions per eCH-0270
        // Generate at high resolution, width stays same but height 1.5x
        private const int PDF417_IMAGE_WIDTH_PIXELS = 290;
        private const int PDF417_IMAGE_HEIGHT_PIXELS = 35;

        // Physical dimensions - TARGET size in PDF
        private const double ELEMENT_WIDTH_CM = 0.042; // 0.04-0.042 cm
        private const double ELEMENT_HEIGHT_CM = 0.08;
        private const double PDF417_TARGET_WIDTH_CM = PDF417_IMAGE_WIDTH_PIXELS * ELEMENT_WIDTH_CM; // Original width
        private const double PDF417_TARGET_HEIGHT_CM = PDF417_IMAGE_HEIGHT_PIXELS * ELEMENT_HEIGHT_CM; // Original height

        // Convert cm to points (1 inch = 2.54 cm = 72 points)
        private const double CM_TO_POINTS = 72.0 / 2.54;
        private const float PDF417_WIDTH_POINTS = (float)(PDF417_TARGET_WIDTH_CM * CM_TO_POINTS);
        private const float PDF417_HEIGHT_POINTS = (float)(PDF417_TARGET_HEIGHT_CM * CM_TO_POINTS);

        // Margins per eCH-0270 (landscape)
        private const float MARGIN_TOP_CM = 5.0f;
        private const float MARGIN_LEFT_CM = 2.0f;
        private const float MARGIN_RIGHT_CM = 3.0f;
        private const float MARGIN_BOTTOM_CM = 2.0f;

        // Spacing per eCH-0270
        private const float SPACING_SEGMENTS_CM = 1.0f; // Minimum 1cm between segments
        private const float ADDITIONAL_SPACING_FOLD_CM = 1f; // Larger spacing between segments 3-4 (fold line)
        private const float EXCLUSION_ZONE_CM = 1.0f; // 1cm exclusion zone around barcodes


        // 1D CODE128C barcode specifications
        private const string FORM_NUMBER_SUMMARY = "197"; // Summary pages (no eCH-0196 data)
        private const string FORM_NUMBER_DATA = "196"; // Data pages (with eCH-0196 XML)
        private const string VERSION_NUMBER = "21"; // Version 2.1
        private const string ORGANIZATION_NUMBER = "00000"; // 5-digit clearing number (placeholder)
        private const int BARCODE_HEIGHT_MM = 7;
        private const int BARCODE_WIDTH_MM = 38;
        private const int BARCODE_MARGIN_MM = 5;
        private const int BARCODE_TOP_MARGIN_MM = 10;

        public static void GeneratePdfWithBarcodes(string xmlFilePath, string outputPdfPath)
        {
            Console.WriteLine($"Generating PDF with PDF417 barcodes (eCH-0196 format) from {xmlFilePath}...");

            // Read and compress XML content using zlib/DEFLATE
            string xmlContent = File.ReadAllText(xmlFilePath, Encoding.UTF8);
            byte[] compressedData = CompressData(Encoding.UTF8.GetBytes(xmlContent));
            Console.WriteLine($"Compressed XML from {xmlContent.Length} to {compressedData.Length} bytes (zlib/DEFLATE)");

            // Generate unique barcode ID (UUID format as per eCH-0196)
            string barcodeId = Guid.NewGuid().ToString("D"); // Format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx

            // Split into chunks
            var chunks = SplitIntoChunks(compressedData);
            Console.WriteLine($"Split into {chunks.Count} PDF417 barcode segment(s)");
            Console.WriteLine($"Barcode ID: {barcodeId}");

            // Generate PDF417 codes with Structured Append
            var barcodeImages = GeneratePdf417Codes(chunks, barcodeId);

            // Create PDF with summary page
            CreatePdf(outputPdfPath, barcodeImages, chunks.Count, barcodeId, xmlContent);

            Console.WriteLine($"✓ Generated PDF with PDF417 barcodes: {outputPdfPath}");
        }

        private static byte[] CompressData(byte[] data)
        {
            using var outputStream = new MemoryStream();
            using (var zlibStream = new ZlibStream(outputStream, SharpCompress.Compressors.CompressionMode.Compress, SharpCompress.Compressors.Deflate.CompressionLevel.BestCompression))
            {
                zlibStream.Write(data, 0, data.Length);
            }
            return outputStream.ToArray();
        }

        private static List<byte[]> SplitIntoChunks(byte[] data)
        {
            var chunks = new List<byte[]>();
            int offset = 0;

            while (offset < data.Length)
            {
                int chunkSize = Math.Min(MAX_PDF417_SIZE, data.Length - offset);

                // Store actual data size for the last chunk
                byte[] chunk = new byte[chunkSize];
                Array.Copy(data, offset, chunk, 0, chunkSize);
                chunks.Add(chunk);

                offset += chunkSize;
            }

            return chunks;
        }

        private static List<byte[]> GeneratePdf417Codes(List<byte[]> chunks, string barcodeId)
        {
            var barcodeImages = new List<byte[]>();

            for (int i = 0; i < chunks.Count; i++)
            {
                // Use raw compressed data directly without headers
                // First barcode contains zlib header, subsequent barcodes contain raw DEFLATE continuation
                byte[] barcodeData = chunks[i];

                Console.WriteLine($"  PDF417 segment {i + 1}/{chunks.Count}: {barcodeData.Length} bytes");

                // Generate PDF417 code using ZXing with eCH-0270 specifications
                // [MUSS] 13 columns, 35 rows for ALL segments (including last)
                // [MUSS] EC-Level 4
                // [MUSS] Image dimensions: 290 x 35 pixels
                
                // Pad smaller chunks with null bytes to ensure 35 rows are generated
                // This is required by eCH-0270 - ALL barcodes must have exactly 35 rows
                byte[] paddedData = barcodeData;
                if (barcodeData.Length < MAX_PDF417_SIZE)
                {
                    paddedData = new byte[MAX_PDF417_SIZE];
                    Array.Copy(barcodeData, paddedData, barcodeData.Length);
                    // Remaining bytes are already zero (null padding)
                }
                
                var writer = new BarcodeWriter
                {
                    Format = BarcodeFormat.PDF_417,
                    Options = new ZXing.PDF417.PDF417EncodingOptions
                    {
                        Height = PDF417_IMAGE_HEIGHT_PIXELS,
                        Width = PDF417_IMAGE_WIDTH_PIXELS,
                        Dimensions = new Dimensions(PDF417_COLUMNS, PDF417_COLUMNS, PDF417_ROWS, PDF417_ROWS),
                        Margin = 0, // No margin - eCH-0270 specifies 1cm exclusion zone in PDF layout
                        ErrorCorrection = PDF417_ERROR_CORRECTION, // EC-Level 4
                        Compaction = Compaction.BYTE,
                        PureBarcode = true, // Ensure proper PDF417 rendering with start/stop patterns
                        NoPadding = false // Allow padding to fill 35 rows
                    },
                };

                // Encode raw binary data directly using Latin1 to preserve byte values
                string binaryString = Encoding.Latin1.GetString(paddedData);
                using var bitmap = writer.Write(binaryString);

                // Convert SKBitmap to byte array (PNG)
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                barcodeImages.Add(data.ToArray());
            }

            return barcodeImages;
        }

        private static byte[] GenerateCode128Barcode(int pageNumber, bool isDataPage, int orientation)
        {
            // Build 16-digit CODE128C barcode for eCH-0196
            // Format: 197/196 (form) + 21 (version) + 00000 (org) + 001 (page) + 0 (has2D) + 2 (orient) + 1 (direction)
            // Note: Has2D flag is ALWAYS 0 even when PDF417 barcodes are present (per eCH-0196 spec)
            string formNumber = isDataPage ? FORM_NUMBER_DATA : FORM_NUMBER_SUMMARY;
            int twoDBarcode = 0; // Always 0 per eCH-0196 specification
            int posId = 3;
            string barcodeData = $"{formNumber}{VERSION_NUMBER}{ORGANIZATION_NUMBER}{pageNumber:D3}{twoDBarcode}{orientation}{posId}";

            // Generate CODE128C barcode using ZXing
            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Height = BARCODE_HEIGHT_MM * 4, // Convert mm to pixels at 300 DPI
                    Width = BARCODE_WIDTH_MM,
                    Margin = 0,
                    PureBarcode = false, // Show text below barcode
                    GS1Format = false
                }
            };

            using var bitmap = writer.Write(barcodeData);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        private static void CreatePdf(string outputPath, List<byte[]> barcodeImages, int totalChunks, string barcodeId, string xmlContent)
        {
            using var writer = new PdfWriter(outputPath);
            using var pdf = new PdfDocument(writer);
            // Use landscape orientation (A4 rotated) per eCH-0270
            using var document = new Document(pdf, PageSize.A4.Rotate());

            const int barcodesPerPage = 6;
            int barcodePageCount = (int)Math.Ceiling(barcodeImages.Count / (double)barcodesPerPage);
            int totalPages = barcodePageCount + 1; // +1 for summary page

            // Add summary page first
            pdf.AddNewPage();
            float pageWidth = PageSize.A4.Rotate().GetWidth();
            float pageHeight = PageSize.A4.Rotate().GetHeight();

            // Parse XML to extract summary information
            var xmlDoc = new System.Xml.XmlDocument();
            xmlDoc.LoadXml(xmlContent);
            var nsmgr = new System.Xml.XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("ech", "http://www.ech.ch/xmlns/eCH-0196/2");

            // Extract key information from root element attributes
            var root = xmlDoc.SelectSingleNode("//ech:taxStatement", nsmgr);
            string taxPeriod = root?.Attributes?["taxPeriod"]?.Value ?? "N/A";
            string canton = root?.Attributes?["canton"]?.Value ?? "N/A";
            string periodFrom = root?.Attributes?["periodFrom"]?.Value ?? "N/A";
            string periodTo = root?.Attributes?["periodTo"]?.Value ?? "N/A";
            string totalTaxValue = root?.Attributes?["totalTaxValue"]?.Value ?? "0";
            string totalGrossRevenue = root?.Attributes?["totalGrossRevenueB"]?.Value ?? "0";
            string totalWithholdingTax = root?.Attributes?["totalWithHoldingTaxClaim"]?.Value ?? "0";

            // Client and institution information
            string clientNumber = xmlDoc.SelectSingleNode("//ech:client", nsmgr)?.Attributes?["clientNumber"]?.Value ?? "N/A";
            string institutionName = xmlDoc.SelectSingleNode("//ech:institution", nsmgr)?.Attributes?["name"]?.Value ?? "N/A";
            string depotNumber = xmlDoc.SelectSingleNode("//ech:depot", nsmgr)?.Attributes?["depotNumber"]?.Value ?? "N/A";

            int securityCount = xmlDoc.SelectNodes("//ech:security", nsmgr)?.Count ?? 0;
            int paymentCount = xmlDoc.SelectNodes("//ech:payment", nsmgr)?.Count ?? 0;
            int mutationCount = xmlDoc.SelectNodes("//ech:stock", nsmgr)?.Count ?? 0;

            // Add title
            var title = new Paragraph("eCH-0196 Tax Statement Summary")
                .SetFontSize(24)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                .SetMarginTop(50);
            document.Add(title);

            // Create two-column layout using Table
            var table = new iText.Layout.Element.Table(2)
                .UseAllAvailableWidth()
                .SetMarginTop(30)
                .SetMarginLeft(60)
                .SetMarginRight(60);

            // Left column content
            var leftColumn = new Paragraph()
                .SetFontSize(12);

            leftColumn.Add(new Text("Tax Period:\n").SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
            leftColumn.Add($"{taxPeriod} ({periodFrom} to {periodTo})\n\n");

            leftColumn.Add(new Text("Canton:\n").SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
            leftColumn.Add($"{canton}\n\n");

            leftColumn.Add(new Text("Client Number:\n").SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
            leftColumn.Add($"{clientNumber}\n\n");

            leftColumn.Add(new Text("Financial Institution:\n").SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
            leftColumn.Add($"{institutionName}\n\n");

            leftColumn.Add(new Text("Depot Number:\n").SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
            leftColumn.Add($"{depotNumber}\n\n");

            leftColumn.Add(new Text("Securities: ").SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
            leftColumn.Add($"{securityCount}\n");
            leftColumn.Add(new Text("Payments: ").SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
            leftColumn.Add($"{paymentCount}\n");
            leftColumn.Add(new Text("Stock Mutations: ").SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
            leftColumn.Add($"{mutationCount}\n");

            // Right column content
            var rightColumn = new Paragraph()
                .SetFontSize(12);

            rightColumn.Add(new Text("Total Tax Value:\n").SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
            rightColumn.Add($"CHF {decimal.Parse(totalTaxValue):N2}\n\n");

            rightColumn.Add(new Text("Total Gross Revenue:\n").SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
            rightColumn.Add($"CHF {decimal.Parse(totalGrossRevenue):N2}\n\n");

            rightColumn.Add(new Text("Total Withholding Tax:\n").SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
            rightColumn.Add($"CHF {decimal.Parse(totalWithholdingTax):N2}\n\n");

            // Add position breakdown by category
            var securities = xmlDoc.SelectNodes("//ech:security", nsmgr);
            if (securities != null && securities.Count > 0)
            {
                rightColumn.Add(new Text("Position Summary:\n").SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                decimal totalPositionValue = 0;
                var positionsByCategory = new Dictionary<string, (int count, decimal value)>
                {
                    ["SHARE"] = (0, 0),
                    ["OTHER"] = (0, 0),
                    ["BOND"] = (0, 0),
                    ["OPTION"] = (0, 0)
                };

                foreach (XmlNode security in securities)
                {
                    var taxValueNode = security.SelectSingleNode("ech:taxValue", nsmgr);
                    if (taxValueNode != null)
                    {
                        string category = security.Attributes?["securityCategory"]?.Value ?? "OTHER";
                        decimal value = decimal.Parse(taxValueNode.Attributes?["value"]?.Value ?? "0");
                        totalPositionValue += value;

                        // Update category totals
                        if (positionsByCategory.ContainsKey(category))
                        {
                            var (count, catValue) = positionsByCategory[category];
                            positionsByCategory[category] = (count + 1, catValue + value);
                        }
                        else
                        {
                            positionsByCategory[category] = (1, value);
                        }
                    }
                }

                foreach (var kvp in positionsByCategory.OrderByDescending(x => x.Value.value))
                {
                    if (kvp.Value.count > 0)
                    {
                        string categoryName = kvp.Key switch
                        {
                            "SHARE" => "Stocks/ETFs",
                            "OTHER" => "Cash & Other",
                            "BOND" => "Bonds",
                            "OPTION" => "Options",
                            _ => kvp.Key
                        };
                        rightColumn.Add($"{categoryName}: {kvp.Value.count} pos, CHF {kvp.Value.value:N2}\n");
                    }
                }
                rightColumn.Add(new Text($"Total: CHF {totalPositionValue:N2}\n").SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
            }

            // Add columns to table
            table.AddCell(new iText.Layout.Element.Cell().Add(leftColumn).SetBorder(iText.Layout.Borders.Border.NO_BORDER));
            table.AddCell(new iText.Layout.Element.Cell().Add(rightColumn).SetBorder(iText.Layout.Borders.Border.NO_BORDER));

            document.Add(table);

            // Add barcode info at the bottom
            var barcodeInfo = new Paragraph()
                .SetFontSize(9)
                .SetMarginTop(20)
                .SetMarginLeft(60);
            barcodeInfo.Add($"Barcode ID: {barcodeId}  |  ");
            barcodeInfo.Add($"Barcode Segments: {totalChunks}  |  ");
            barcodeInfo.Add($"Barcode Pages: {barcodePageCount}");
            document.Add(barcodeInfo);

            // Add CODE128C barcode to summary page (Form 197, no eCH-0196 data)
            var summaryCode128Image = GenerateCode128Barcode(1, false, 0);
            var summaryCode128ImageData = ImageDataFactory.Create(summaryCode128Image);
            var summaryPdfCode128 = new Image(summaryCode128ImageData);
            summaryPdfCode128.ScaleToFit(BARCODE_WIDTH_MM * 5, BARCODE_HEIGHT_MM * 5);
            summaryPdfCode128.SetRotationAngle(3 * Math.PI / 2); // Rotate 90° clockwise
            float code128X = 0.5f * (float)CM_TO_POINTS; // 5mm
            float code128Y = pageHeight - 1.0f * (float)CM_TO_POINTS; // 10mm to points
            summaryPdfCode128.SetFixedPosition(1, code128X, code128Y);
            document.Add(summaryPdfCode128);

            // Add page number to summary page
            var pageNumberText = new Paragraph($"Page 1 of {totalPages}")
                .SetFontSize(10)
                .SetFixedPosition(1, pageWidth - 100, 20, 80)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT);
            document.Add(pageNumberText);

            // Add PDF417 barcode pages
            for (int pageIdx = 0; pageIdx < barcodePageCount; pageIdx++)
            {
                pdf.AddNewPage(); // Landscape orientation
                int currentPageNumber = pageIdx + 2; // +2 because page 1 is summary

                pageWidth = PageSize.A4.Rotate().GetWidth();
                pageHeight = PageSize.A4.Rotate().GetHeight();

                // eCH-0270 margins (landscape orientation)
                float marginRight = MARGIN_RIGHT_CM * (float)CM_TO_POINTS;

                // eCH-0270 spacing in points
                float spacingNormal = SPACING_SEGMENTS_CM * (float)CM_TO_POINTS;
                float spacingLarge = ADDITIONAL_SPACING_FOLD_CM * (float)CM_TO_POINTS;

                // Add CODE128C barcode for this page (Form 196 with eCH-0196 data)
                // Has2D flag is always 0 per eCH-0196 spec, Orientation: 0 (landscape), Reading direction: 3
                var code128Image = GenerateCode128Barcode(currentPageNumber, isDataPage: true, 0);
                var code128ImageData = ImageDataFactory.Create(code128Image);
                var pdfCode128 = new Image(code128ImageData);

                // Scale to 40% to reduce barcode size while keeping text readable
                pdfCode128.ScaleToFit(BARCODE_WIDTH_MM * 5, BARCODE_HEIGHT_MM * 5);
                pdfCode128.SetRotationAngle(3 * Math.PI / 2); // Rotate 90° clockwise

                pdfCode128.SetFixedPosition(currentPageNumber, code128X, code128Y);
                document.Add(pdfCode128);

                // Add page number
                var barcodePageNumber = new Paragraph($"Page {currentPageNumber} of {totalPages}")
                    .SetFontSize(10)
                    .SetFixedPosition(currentPageNumber, pageWidth - 100, 20, 80)
                    .SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT);
                document.Add(barcodePageNumber);

                // Calculate available space for PDF417 barcodes
                // Layout: 3 columns × 2 rows, left to right
                // Segments: 1 2 3 (top row), 4 5 6 (bottom row)
                // Larger spacing between segments 3-4 (fold line)

                int segmentsOnThisPage = Math.Min(barcodesPerPage, barcodeImages.Count - pageIdx * barcodesPerPage);

                // Add PDF417 barcodes with precise eCH-0270 positioning
                // Barcodes are rotated 90° and placed in a single row from right to left
                for (int i = 0; i < segmentsOnThisPage; i++)
                {
                    int barcodeIdx = pageIdx * barcodesPerPage + i;
                    if (barcodeIdx >= barcodeImages.Count)
                        break;

                    var imageData = ImageDataFactory.Create(barcodeImages[barcodeIdx]);
                    var pdfImage = new iText.Layout.Element.Image(imageData);

                    // BEFORE rotation: swap dimensions because 90° rotation will swap them back
                    // We want visual result: width=12.18cm, height=4.2cm
                    // So before rotation set: width=4.2cm, height=12.18cm
                    pdfImage = pdfImage.ScaleAbsolute(PDF417_WIDTH_POINTS, PDF417_HEIGHT_POINTS);

                    // Rotate 90° clockwise
                    pdfImage = pdfImage.SetRotationAngle(3 * Math.PI / 2); // 90° in radians

                    // Position from RIGHT to LEFT in a single row
                    // X position: start from right edge, move left for each barcode
                    float spacing = i * spacingNormal;
                    // Add extra spacing after 3rd barcode for fold line
                    if (i >= 3) { spacing += spacingLarge; }
                    float xPosition = pageWidth - marginRight - (i + 1) * PDF417_HEIGHT_POINTS - spacing;

                    // Y position: ensure at least 5cm from top of barcode to page border
                    // The barcode extends upward by rotatedHeight from yPosition
                    float topMargin = MARGIN_TOP_CM * (float)CM_TO_POINTS; // 5cm minimum
                    float yPosition = pageHeight - topMargin;

                    pdfImage.SetFixedPosition(currentPageNumber, xPosition, yPosition);
                    document.Add(pdfImage);
                }
            }

            document.Close();

            Console.WriteLine($"✓ Generated PDF with PDF417 and CODE128C barcodes: {outputPath}");
            Console.WriteLine($"  Pages: {totalPages}");
            Console.WriteLine($"  Barcodes per page: {barcodesPerPage}");
            Console.WriteLine($"  Total barcodes: {barcodeImages.Count}");
            Console.WriteLine($"  Barcode ID: {barcodeId}");
            Console.WriteLine($"  Barcode dimensions: {PDF417_TARGET_WIDTH_CM:F2} × {PDF417_TARGET_HEIGHT_CM:F2} cm (scaled for 97% print)");
        }
    }
}
