using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
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

namespace IbkrToEtax
{
    public class PdfBarcodeGenerator
    {
        private const int MAX_PDF417_SIZE = 700; // Maximum bytes per PDF417 code segment (conservative for 13 cols, 35 rows, EC level 4)
        
        // PDF417 specifications as per eCH-0196
        private const int PDF417_COLUMNS = 13;
        private const int PDF417_ROWS = 35;
        private const int PDF417_ERROR_CORRECTION = 4; // EC-Level 4
        private const double ELEMENT_WIDTH_CM = 0.041; // 0.04-0.042 cm
        private const double ELEMENT_HEIGHT_CM = 0.08;
        
        // Convert cm to pixels (assuming 300 DPI)
        private const int ELEMENT_WIDTH_PX = (int)(ELEMENT_WIDTH_CM * 300 / 2.54);
        private const int ELEMENT_HEIGHT_PX = (int)(ELEMENT_HEIGHT_CM * 300 / 2.54);
        
        // 1D CODE128C barcode specifications
        private const string FORM_NUMBER = "196"; // eCH-0196
        private const string VERSION_NUMBER = "22"; // Version 2.2
        private const string ORGANIZATION_NUMBER = "00000"; // 5-digit clearing number (placeholder)
        private const int BARCODE_HEIGHT_MM = 7;
        private const int BARCODE_WIDTH_MM = 38;
        private const int BARCODE_MARGIN_MM = 5;
        private const int BARCODE_TOP_MARGIN_MM = 10;

        public static void GeneratePdfWithBarcodes(string xmlFilePath, string outputPdfPath)
        {
            Console.WriteLine($"Generating PDF with PDF417 barcodes (eCH-0196 format) from {xmlFilePath}...");

            // Read and compress XML content using ZLIB (GZIP = ZLIB + headers)
            string xmlContent = File.ReadAllText(xmlFilePath, Encoding.UTF8);
            byte[] compressedData = CompressData(Encoding.UTF8.GetBytes(xmlContent));
            Console.WriteLine($"Compressed XML from {xmlContent.Length} to {compressedData.Length} bytes (ZLIB)");

            // Generate unique barcode ID (UUID format as per eCH-0196)
            string barcodeId = Guid.NewGuid().ToString("D"); // Format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx

            // Split into chunks
            var chunks = SplitIntoChunks(compressedData);
            Console.WriteLine($"Split into {chunks.Count} PDF417 barcode segment(s)");
            Console.WriteLine($"Barcode ID: {barcodeId}");

            // Generate PDF417 codes with Structured Append
            var barcodeImages = GeneratePdf417Codes(chunks, barcodeId);

            // Create PDF
            CreatePdf(outputPdfPath, barcodeImages, chunks.Count, barcodeId);

            Console.WriteLine($"✓ Generated PDF with PDF417 barcodes: {outputPdfPath}");
        }

        private static byte[] CompressData(byte[] data)
        {
            using var outputStream = new MemoryStream();
            using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
            {
                gzipStream.Write(data, 0, data.Length);
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
                // Create header with barcode ID and chunk information as per eCH-0196
                // Format: ID|ChunkNumber|TotalChunks|CompressedData
                string header = $"{barcodeId}|{i + 1}|{chunks.Count}|";
                byte[] headerBytes = Encoding.UTF8.GetBytes(header);

                // Combine header with chunk data
                byte[] barcodeData = new byte[headerBytes.Length + chunks[i].Length];
                Array.Copy(headerBytes, 0, barcodeData, 0, headerBytes.Length);
                Array.Copy(chunks[i], 0, barcodeData, headerBytes.Length, chunks[i].Length);

                Console.WriteLine($"  PDF417 segment {i + 1}/{chunks.Count}: {barcodeData.Length} bytes");

                // Generate PDF417 code using ZXing with eCH-0196 specifications
                var writer = new ZXing.SkiaSharp.BarcodeWriter
                {
                    Format = BarcodeFormat.PDF_417,
                    Options = new ZXing.PDF417.PDF417EncodingOptions
                    {
                        Height = PDF417_ROWS * ELEMENT_HEIGHT_PX,
                        Width = PDF417_COLUMNS * ELEMENT_WIDTH_PX * 6, // 6 blocks
                        Margin = 2,
                        ErrorCorrection = ZXing.PDF417.Internal.PDF417ErrorCorrectionLevel.L4, // EC-Level 4
                        Compaction = ZXing.PDF417.Internal.Compaction.BYTE
                        // Note: ZXing doesn't expose all PDF417 parameters
                        // Using 13 columns, 35 rows via dimension calculations
                        // Structured Append is handled via header format: ID|segment|total|data
                    }
                };

                // Encode binary data as Base64 for PDF417
                string base64Data = Convert.ToBase64String(barcodeData);
                using var bitmap = writer.Write(base64Data);
                
                // Convert SKBitmap to byte array (PNG)
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                barcodeImages.Add(data.ToArray());
            }

            return barcodeImages;
        }

        private static byte[] GenerateCode128Barcode(int pageNumber, bool has2DBarcode, int orientation, int readingDirection)
        {
            // Build 16-digit CODE128C barcode for eCH-0196
            // Format: 196 (form) + 22 (version) + 00000 (org) + 001 (page) + 1 (has2D) + 1 (orient) + 1 (direction)
            string barcodeData = $"{FORM_NUMBER}{VERSION_NUMBER}{ORGANIZATION_NUMBER}{pageNumber:D3}{(has2DBarcode ? "1" : "0")}{orientation}{readingDirection}";
            
            // Generate CODE128C barcode using ZXing
            var writer = new ZXing.SkiaSharp.BarcodeWriter
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Height = (int)(BARCODE_HEIGHT_MM * 300 / 25.4), // Convert mm to pixels at 300 DPI
                    Width = (int)(BARCODE_WIDTH_MM * 300 / 25.4),
                    Margin = 0,
                    PureBarcode = false // Show text below barcode
                }
            };

            using var bitmap = writer.Write(barcodeData);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        private static void CreatePdf(string outputPath, List<byte[]> barcodeImages, int totalChunks, string barcodeId)
        {
            using var writer = new PdfWriter(outputPath);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, PageSize.A4);

            int pageNumber = 1;

            // Add PDF417 codes
            for (int i = 0; i < barcodeImages.Count; i++)
            {
                // Add new page for each barcode
                pdf.AddNewPage();
                
                float pageWidth = PageSize.A4.GetWidth();
                float pageHeight = PageSize.A4.GetHeight();

                // Add CODE128C barcode for this page (at top-left)
                // Orientation: 1 (portrait), Reading direction: 1
                var code128Image = GenerateCode128Barcode(pageNumber, true, 1, 1);
                var code128ImageData = ImageDataFactory.Create(code128Image);
                var pdfCode128 = new iText.Layout.Element.Image(code128ImageData);

                // Position CODE128C at top-left with 5mm left margin, 10mm top margin
                float leftMargin = 5 * 72 / 25.4f; // 5mm to points (72 points per inch)
                float topMargin = 10 * 72 / 25.4f; // 10mm to points
                
                pdfCode128.SetFixedPosition(i + 1, leftMargin, pageHeight - topMargin - pdfCode128.GetImageScaledHeight());
                document.Add(pdfCode128);

                // Add PDF417 barcode image (centered)
                var imageData = ImageDataFactory.Create(barcodeImages[i]);
                var pdfImage = new iText.Layout.Element.Image(imageData);
                
                // Scale if necessary
                float maxWidth = pageWidth - 100;
                float maxHeight = pageHeight - 200;
                
                if (pdfImage.GetImageWidth() > maxWidth || pdfImage.GetImageHeight() > maxHeight)
                {
                    float scale = Math.Min(maxWidth / pdfImage.GetImageWidth(), 
                                          maxHeight / pdfImage.GetImageHeight());
                    pdfImage.Scale(scale, scale);
                }

                // Center the PDF417 barcode on page with fixed position
                float xPosition = (pageWidth - pdfImage.GetImageScaledWidth()) / 2;
                float yPosition = (pageHeight - pdfImage.GetImageScaledHeight()) / 2;
                
                pdfImage.SetFixedPosition(i + 1, xPosition, yPosition);
                document.Add(pdfImage);

                pageNumber++;
            }

            document.Close();
            
            Console.WriteLine($"✓ Generated PDF with PDF417 and CODE128C barcodes: {outputPath}");
            Console.WriteLine($"  Pages: {barcodeImages.Count}");
            Console.WriteLine($"  Barcode ID: {barcodeId}");
        }
    }
}
