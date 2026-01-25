using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using ZXing;
using ZXing.SkiaSharp;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Kernel.Pdf.Xobject;
using SkiaSharp;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors;
using SysCompress = System.IO.Compression;

namespace IbkrToEtax
{
    public class PdfValidator
    {
        private const string ECH_0196_FORM_NUMBER = "196";
        private const string ECH_0196_VERSION = "22";
        private const string ECH_0196_NAMESPACE = "http://www.ech.ch/xmlns/eCH-0196/2";
        
        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
            public List<string> Warnings { get; set; } = new List<string>();
            public string? ExtractedXml { get; set; }
            public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        }

        /// <summary>
        /// Validates if a PDF conforms to the eCH-0196 standard
        /// </summary>
        /// <param name="pdfPath">Path to the PDF file</param>
        /// <param name="xsdPath">Optional path to the eCH-0196 XSD schema for validation</param>
        /// <returns>ValidationResult containing validation status and details</returns>
        public static ValidationResult ValidatePdf(string pdfPath, string? xsdPath = null)
        {
            var result = new ValidationResult { IsValid = true };

            try
            {
                if (!File.Exists(pdfPath))
                {
                    result.IsValid = false;
                    result.Errors.Add($"PDF file not found: {pdfPath}");
                    return result;
                }

                Console.WriteLine($"Validating PDF against eCH-0196 standard: {pdfPath}");

                using (var pdfReader = new PdfReader(pdfPath))
                using (var pdfDocument = new PdfDocument(pdfReader))
                {
                    int pageCount = pdfDocument.GetNumberOfPages();
                    result.Metadata["PageCount"] = pageCount;
                    Console.WriteLine($"  PDF has {pageCount} page(s)");

                    // Step 1: Validate CODE128C barcodes on each page
                    bool code128ValidationResult = ValidateCode128Barcodes(pdfDocument, result);
                    if (!code128ValidationResult)
                    {
                        result.IsValid = false;
                    }

                    // Step 2: Extract and validate PDF417 barcodes
                    var pdf417Data = ExtractPdf417Data(pdfDocument, result);
                    
                    string? xmlContent = null;
                    
                    // Check if we have direct XML format (zlib/DEFLATE)
                    if (result.Metadata.ContainsKey("DirectXmlBarcodes"))
                    {
                        var directBarcodes = (List<string>)result.Metadata["DirectXmlBarcodes"];
                        if (directBarcodes.Count > 0)
                        {
                            Console.WriteLine($"  ✓ Found {directBarcodes.Count} direct zlib/DEFLATE compressed barcode(s)");
                            
                            // Pass all barcodes to decompress - they'll be concatenated
                            xmlContent = DecompressDirectXmlBarcode(directBarcodes, result);
                        }
                    }
                    // Otherwise try chunked format (Base64 + GZIP)
                    else if (pdf417Data != null && pdf417Data.Count > 0)
                    {
                        // Step 3: Reconstruct compressed data from chunks
                        var reconstructedData = ReconstructCompressedData(pdf417Data, result);
                        if (reconstructedData != null)
                        {
                            // Step 4: Decompress and extract XML
                            xmlContent = DecompressXmlData(reconstructedData, result);
                        }
                    }
                    
                    if (xmlContent != null)
                    {
                        result.ExtractedXml = xmlContent;
                        result.Metadata["XmlLength"] = xmlContent.Length;
                        Console.WriteLine($"  ✓ Successfully extracted XML ({xmlContent.Length} chars)");

                        // Step 5: Validate XML structure
                        if (!ValidateXmlStructure(xmlContent, result))
                        {
                            result.IsValid = false;
                        }

                        // Step 6: Validate against XSD schema if provided
                        if (!string.IsNullOrEmpty(xsdPath) && File.Exists(xsdPath))
                        {
                            if (!ValidateAgainstSchema(xmlContent, xsdPath, result))
                            {
                                result.IsValid = false;
                            }
                        }
                        else if (!string.IsNullOrEmpty(xsdPath))
                        {
                            result.Warnings.Add($"XSD schema file not found: {xsdPath}");
                        }
                    }
                    else
                    {
                        result.Warnings.Add("No valid PDF417 barcodes with extractable data found (may use alternative eCH-0196 encoding)");
                        Console.WriteLine("  ⚠ No extractable PDF417 data (alternative implementation or no embedded XML)");
                    }
                }

                if (result.IsValid)
                {
                    Console.WriteLine("  ✓ PDF conforms to eCH-0196 standard");
                }
                else
                {
                    Console.WriteLine($"  ✗ PDF validation failed with {result.Errors.Count} error(s)");
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Validation exception: {ex.Message}");
                Console.WriteLine($"  ✗ Validation error: {ex.Message}");
            }

            return result;
        }

        private static bool ValidateCode128Barcodes(PdfDocument pdfDocument, ValidationResult result)
        {
            bool allValid = true;
            int pageCount = pdfDocument.GetNumberOfPages();

            Console.WriteLine("  Validating CODE128C barcodes...");

            for (int pageNum = 1; pageNum <= pageCount; pageNum++)
            {
                var page = pdfDocument.GetPage(pageNum);
                var barcodes = ExtractBarcodesFromPage(page);

                var code128Barcode = barcodes.FirstOrDefault(b => b.BarcodeFormat == BarcodeFormat.CODE_128);
                if (code128Barcode == null)
                {
                    result.Errors.Add($"Page {pageNum}: No CODE128 barcode found");
                    allValid = false;
                    continue;
                }

                // Validate CODE128 format: 196 (form) + 22 (version) + 00000 (org) + 001 (page) + 1 (has2D) + 1 (orient) + 1 (direction)
                string barcodeText = code128Barcode.Text;
                if (barcodeText.Length != 16)
                {
                    result.Errors.Add($"Page {pageNum}: CODE128 barcode has invalid length ({barcodeText.Length}, expected 16)");
                    allValid = false;
                    continue;
                }

                string formNumber = barcodeText.Substring(0, 3);
                string version = barcodeText.Substring(3, 2);
                string orgNumber = barcodeText.Substring(5, 5);
                string pageNumber = barcodeText.Substring(10, 3);
                string has2DBarcode = barcodeText.Substring(13, 1);
                string orientation = barcodeText.Substring(14, 1);
                string readingDirection = barcodeText.Substring(15, 1);

                // Accept both form 196 and 197 (eCH-0196 standard forms)
                if (formNumber != "196" && formNumber != "197")
                {
                    result.Errors.Add($"Page {pageNum}: Invalid form number '{formNumber}' (expected '196' or '197')");
                    allValid = false;
                }

                // Accept versions 21 and 22
                if (version != "21" && version != "22")
                {
                    result.Warnings.Add($"Page {pageNum}: Unexpected version '{version}' (expected '21' or '22')");
                }

                // Only warn about missing 2D barcodes on pages after page 1 (summary page typically has no PDF417)
                if (has2DBarcode != "1" && pageNum > 1)
                {
                    result.Warnings.Add($"Page {pageNum}: CODE128 indicates no 2D barcode present");
                }

                Console.WriteLine($"    Page {pageNum}: CODE128 barcode valid (Form: {formNumber}, Version: {version}, Page: {pageNumber}, Has2D: {has2DBarcode})");
            }

            return allValid;
        }

        private static List<Result> ExtractBarcodesFromPage(iText.Kernel.Pdf.PdfPage page)
        {
            var results = new List<Result>();
            
            // Extract all images from the page
            var images = ExtractImagesFromPage(page);
            
            if (images.Count == 0)
            {
                return results;
            }

            var reader = new ZXing.SkiaSharp.BarcodeReader
            {
                AutoRotate = true,
                Options = new ZXing.Common.DecodingOptions
                {
                    TryHarder = true,
                    TryInverted = true,
                    PureBarcode = false,
                    PossibleFormats = new[] { BarcodeFormat.CODE_128, BarcodeFormat.PDF_417 },
                    // Add more lenient detection options
                    UseCode39ExtendedMode = false,
                    UseCode39RelaxedExtendedMode = false,
                    AssumeGS1 = false
                }
            };

            int imageIndex = 0;
            foreach (var imageBytes in images)
            {
                imageIndex++;
                try
                {
                    using var stream = new MemoryStream(imageBytes);
                    using var bitmap = SKBitmap.Decode(stream);
                    if (bitmap != null)
                    {
                        // Try reading with original image
                        var result = TryReadBarcode(reader, bitmap);
                        if (result != null && result.Count > 0)
                        {
                            results.AddRange(result);
                        }
                        else
                        {
                            // Try with image preprocessing
                            var processedResults = TryReadBarcodeWithPreprocessing(reader, bitmap);
                            if (processedResults != null && processedResults.Count > 0)
                            {
                                results.AddRange(processedResults);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Skip invalid images
                }
            }

            return results;
        }

        private static List<Result>? TryReadBarcode(BarcodeReader reader, SKBitmap bitmap)
        {
            var results = new List<Result>();
            
            // Try reading single barcode first
            var result = reader.Decode(bitmap);
            if (result != null)
            {
                results.Add(result);
            }
            
            // Also try reading multiple barcodes
            var multiResults = reader.DecodeMultiple(bitmap);
            if (multiResults != null && multiResults.Length > 0)
            {
                results.AddRange(multiResults);
            }
            
            return results.Count > 0 ? results : null;
        }

        private static List<Result>? TryReadBarcodeWithPreprocessing(BarcodeReader reader, SKBitmap original)
        {
            // Try different preprocessing approaches
            var preprocessingMethods = new List<Func<SKBitmap, SKBitmap?>>
            {
                // 1. Convert to grayscale and increase contrast
                bmp => ConvertToGrayscaleWithContrast(bmp),
                // 2. Scale up small images
                bmp => (bmp.Width < 200 || bmp.Height < 100) ? ScaleImage(bmp, 3.0) : null,
                // 3. Binarize (black and white only)
                bmp => BinarizeImage(bmp),
            };

            foreach (var method in preprocessingMethods)
            {
                try
                {
                    using var processed = method(original);
                    if (processed != null)
                    {
                        var results = TryReadBarcode(reader, processed);
                        if (results != null && results.Count > 0)
                        {
                            return results;
                        }
                    }
                }
                catch
                {
                    // Continue to next preprocessing method
                }
            }

            return null;
        }

        private static SKBitmap? ConvertToGrayscaleWithContrast(SKBitmap source)
        {
            var result = new SKBitmap(source.Width, source.Height);
            using var canvas = new SKCanvas(result);
            
            var paint = new SKPaint
            {
                ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
                {
                    0.21f, 0.72f, 0.07f, 0, 0,
                    0.21f, 0.72f, 0.07f, 0, 0,
                    0.21f, 0.72f, 0.07f, 0, 0,
                    0,     0,     0,     1, 0
                })
            };
            
            canvas.DrawBitmap(source, 0, 0, paint);
            return result;
        }

        private static SKBitmap? ScaleImage(SKBitmap source, double scale)
        {
            int newWidth = (int)(source.Width * scale);
            int newHeight = (int)(source.Height * scale);
            
            var result = new SKBitmap(newWidth, newHeight);
            using var canvas = new SKCanvas(result);
            
            canvas.SetMatrix(SKMatrix.CreateScale((float)scale, (float)scale));
            canvas.DrawBitmap(source, 0, 0);
            
            return result;
        }

        private static SKBitmap? BinarizeImage(SKBitmap source)
        {
            var result = new SKBitmap(source.Width, source.Height);
            
            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    var pixel = source.GetPixel(x, y);
                    var gray = (pixel.Red + pixel.Green + pixel.Blue) / 3;
                    var color = gray > 128 ? SKColors.White : SKColors.Black;
                    result.SetPixel(x, y, color);
                }
            }
            
            return result;
        }

        private static List<byte[]> ExtractImagesFromPage(iText.Kernel.Pdf.PdfPage page)
        {
            var images = new List<byte[]>();
            var resources = page.GetResources();
            
            try
            {
                var xObjectNames = resources.GetResourceNames();
                
                foreach (var name in xObjectNames)
                {
                    try
                    {
                        var obj = resources.GetResourceObject(iText.Kernel.Pdf.PdfName.XObject, name);
                        if (obj != null && obj.IsStream())
                        {
                            var stream = (PdfStream)obj;
                            var subtype = stream.GetAsName(iText.Kernel.Pdf.PdfName.Subtype);
                            
                            // Check if it's an Image XObject
                            if (subtype != null && subtype.Equals(iText.Kernel.Pdf.PdfName.Image))
                            {
                                try
                                {
                                    var imageXObject = new PdfImageXObject(stream);
                                    byte[] imageBytes = imageXObject.GetImageBytes();
                                    
                                    if (imageBytes != null && imageBytes.Length > 0)
                                    {
                                        images.Add(imageBytes);
                                    }
                                }
                                catch
                                {
                                    // Try alternative method - get raw stream bytes
                                    try
                                    {
                                        byte[] rawBytes = stream.GetBytes();
                                        if (rawBytes != null && rawBytes.Length > 0)
                                        {
                                            images.Add(rawBytes);
                                        }
                                    }
                                    catch
                                    {
                                        // Skip this image
                                    }
                                }
                            }
                            // Also check for Form XObjects (in case images are embedded as forms)
                            else if (subtype != null && subtype.Equals(iText.Kernel.Pdf.PdfName.Form))
                            {
                                try
                                {
                                    var formXObject = new PdfFormXObject(stream);
                                    var formResources = formXObject.GetResources();
                                    
                                    if (formResources != null)
                                    {
                                        var formXObjectNames = formResources.GetResourceNames();
                                        foreach (var formName in formXObjectNames)
                                        {
                                            try
                                            {
                                                var formObj = formResources.GetResource(formName);
                                                if (formObj != null && formObj.IsStream())
                                                {
                                                    var formStream = (PdfStream)formObj;
                                                    var formSubtype = formStream.GetAsName(iText.Kernel.Pdf.PdfName.Subtype);
                                                    
                                                    if (formSubtype != null && formSubtype.Equals(iText.Kernel.Pdf.PdfName.Image))
                                                    {
                                                        var imageXObject = new PdfImageXObject(formStream);
                                                        byte[] imageBytes = imageXObject.GetImageBytes();
                                                        
                                                        if (imageBytes != null && imageBytes.Length > 0)
                                                        {
                                                            images.Add(imageBytes);
                                                        }
                                                    }
                                                }
                                            }
                                            catch
                                            {
                                                // Skip invalid form resources
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                    // Skip invalid form
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Skip invalid resources
                    }
                }
            }
            catch
            {
                // Skip if resource extraction fails
            }

            return images;
        }

        private static void AnalyzeBarcodeFormat(string text, byte[]? rawBytes, int pageNum)
        {
            Console.WriteLine($"      Text length: {text.Length} chars");
            Console.WriteLine($"      Raw bytes: {(rawBytes != null ? $"{rawBytes.Length} bytes" : "NULL")}");
            
            // The text is actually the barcode content - convert to bytes to analyze
            byte[] textAsBytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(text);
            Console.WriteLine($"      Text as ISO-8859-1 bytes: {textAsBytes.Length} bytes");
            
            // Check for zlib/DEFLATE signature (78 DA, 78 9C, 78 01)
            if (textAsBytes.Length >= 2 && textAsBytes[0] == 0x78 && (textAsBytes[1] == 0xDA || textAsBytes[1] == 0x9C || textAsBytes[1] == 0x01))
            {
                Console.WriteLine("      ✓ DATA FORMAT: Direct zlib/DEFLATE-compressed data!");
                TryDecompressZlib(textAsBytes);
                return;
            }
            
            // Check if it's Base64
            try
            {
                byte[] base64Decoded = Convert.FromBase64String(text);
                Console.WriteLine($"      Format: Base64-encoded ({base64Decoded.Length} bytes decoded)");
                
                // Check if base64-decoded data is zlib
                if (base64Decoded.Length >= 2 && base64Decoded[0] == 0x78)
                {
                    Console.WriteLine("      ✓ DATA FORMAT: Base64-encoded zlib/DEFLATE data!");
                    TryDecompressZlib(base64Decoded);
                    return;
                }
            }
            catch
            {
                Console.WriteLine("      Format: NOT Base64");
            }
            
            // Show first bytes as hex
            Console.WriteLine($"      First 20 bytes (hex): {BitConverter.ToString(textAsBytes.Take(20).ToArray())}");
            
            // Check for pipe delimiters (chunked format like this tool uses)
            if (text.Contains("|"))
            {
                var parts = text.Split('|');
                Console.WriteLine($"      Contains {parts.Length} pipe-delimited parts");
                
                if (parts.Length >= 3)
                {
                    Console.WriteLine($"        Might be chunked format: ID|ChunkNum|TotalChunks|Data");
                    // Try to parse as chunked format
                    if (int.TryParse(parts[1], out int chunkNum) && int.TryParse(parts[2], out int totalChunks))
                    {
                        Console.WriteLine($"        ✓ Parsed as chunk {chunkNum}/{totalChunks}, ID: '{parts[0]}'");
                    }
                }
            }
        }

        private static void TryDecompressZlib(byte[] zlibData)
        {
            try
            {
                using var inputStream = new MemoryStream(zlibData);
                using var zlibStream = new ZlibStream(inputStream, SharpCompress.Compressors.CompressionMode.Decompress);
                using var outputStream = new MemoryStream();
                zlibStream.CopyTo(outputStream);
                
                string decompressed = Encoding.UTF8.GetString(outputStream.ToArray());
                Console.WriteLine($"      ✓ Successfully decompressed to {decompressed.Length} chars");
                
                // Show preview
                string preview = decompressed.Length > 150 ? decompressed.Substring(0, 150) + "..." : decompressed;
                Console.WriteLine($"      XML Preview: {preview}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ✗ Decompression failed: {ex.Message}");
            }
        }


        private static Dictionary<int, byte[]> ExtractPdf417Data(PdfDocument pdfDocument, ValidationResult result)
        {
            var pdf417Chunks = new Dictionary<int, byte[]>();
            var directXmlBarcodes = new List<string>(); // For non-chunked format
            string? barcodeId = null;
            int? totalChunks = null;

            Console.WriteLine("  Extracting PDF417 barcodes...");

            int pageCount = pdfDocument.GetNumberOfPages();
            for (int pageNum = 1; pageNum <= pageCount; pageNum++)
            {
                var page = pdfDocument.GetPage(pageNum);
                var barcodes = ExtractBarcodesFromPage(page);

                // Get ALL PDF417 barcodes from this page (not just the first)
                var pdf417Barcodes = barcodes.Where(b => b.BarcodeFormat == BarcodeFormat.PDF_417).ToList();
                
                foreach (var pdf417Barcode in pdf417Barcodes)
                {
                    try
                    {
                        string barcodeText = pdf417Barcode.Text;
                        byte[] barcodeData;
                        
                        // Detect format: check if it's zlib/DEFLATE compressed (external format)
                        byte[] textAsBytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(barcodeText);
                        bool isZlibHeader = textAsBytes.Length >= 2 && textAsBytes[0] == 0x78 && 
                                               (textAsBytes[1] == 0xDA || textAsBytes[1] == 0x9C || textAsBytes[1] == 0x01);
                        
                        if (isZlibHeader)
                        {
                            // External format: zlib/DEFLATE compressed XML (first chunk with header)
                            // Deduplicate - ZXing might detect same barcode multiple times
                            if (!directXmlBarcodes.Contains(barcodeText))
                            {
                                Console.WriteLine($"    Page {pageNum}: PDF417 barcode (zlib/DEFLATE header)");
                                directXmlBarcodes.Add(barcodeText);
                            }
                            result.Metadata["BarcodeFormat"] = "zlib/DEFLATE (direct)";
                            continue;
                        }
                        
                        // Try to decode as Base64 (for PDFs generated by this tool)
                        try
                        {
                            barcodeData = Convert.FromBase64String(barcodeText);
                        }
                        catch (System.FormatException)
                        {
                            // Not base64 - might be raw DEFLATE continuation (external format)
                            // If we already found zlib header, treat this as continuation chunk
                            if (directXmlBarcodes.Count > 0)
                            {
                                // Deduplicate - ZXing might detect same barcode multiple times
                                if (!directXmlBarcodes.Contains(barcodeText))
                                {
                                    Console.WriteLine($"    Page {pageNum}: PDF417 barcode (raw DEFLATE continuation)");
                                    directXmlBarcodes.Add(barcodeText);
                                }
                            }
                            else
                            {
                                // Unknown format without zlib header first
                                if (textAsBytes.Length > 10)
                                {
                                    string hexDebug = BitConverter.ToString(textAsBytes.Take(10).ToArray());
                                    result.Warnings.Add($"Page {pageNum}: Unknown PDF417 format, first bytes: {hexDebug}");
                                }
                                else
                                {
                                    result.Warnings.Add($"Page {pageNum}: PDF417 barcode uses unknown encoding");
                                }
                            }
                            continue;
                        }
                        
                        result.Metadata["BarcodeFormat"] = "Base64 + GZIP (chunked)";
                        
                        // Parse header: ID|ChunkNumber|TotalChunks|CompressedData
                        string headerText = Encoding.UTF8.GetString(barcodeData);
                        var parts = headerText.Split('|');
                        
                        if (parts.Length >= 3)
                        {
                            string currentBarcodeId = parts[0];
                            int chunkNumber = int.Parse(parts[1]);
                            int currentTotalChunks = int.Parse(parts[2]);

                            // Find where the header ends (after third |)
                            int headerEndIndex = 0;
                            int pipeCount = 0;
                            for (int i = 0; i < barcodeData.Length && pipeCount < 3; i++)
                            {
                                if (barcodeData[i] == (byte)'|')
                                {
                                    pipeCount++;
                                    if (pipeCount == 3)
                                    {
                                        headerEndIndex = i + 1;
                                        break;
                                    }
                                }
                            }

                            // Extract chunk data (after header)
                            byte[] chunkData = new byte[barcodeData.Length - headerEndIndex];
                            Array.Copy(barcodeData, headerEndIndex, chunkData, 0, chunkData.Length);

                            // Validate consistency
                            if (barcodeId == null)
                            {
                                barcodeId = currentBarcodeId;
                                result.Metadata["BarcodeId"] = barcodeId;
                            }
                            else if (barcodeId != currentBarcodeId)
                            {
                                result.Errors.Add($"Page {pageNum}: Barcode ID mismatch (expected {barcodeId}, found {currentBarcodeId})");
                                continue;
                            }

                            if (totalChunks == null)
                            {
                                totalChunks = currentTotalChunks;
                                result.Metadata["TotalChunks"] = totalChunks;
                            }
                            else if (totalChunks != currentTotalChunks)
                            {
                                result.Errors.Add($"Page {pageNum}: Total chunks mismatch (expected {totalChunks}, found {currentTotalChunks})");
                                continue;
                            }

                            if (pdf417Chunks.ContainsKey(chunkNumber))
                            {
                                result.Warnings.Add($"Page {pageNum}: Duplicate chunk {chunkNumber} found");
                            }
                            else
                            {
                                pdf417Chunks[chunkNumber] = chunkData;
                                Console.WriteLine($"    Page {pageNum}: PDF417 chunk {chunkNumber}/{totalChunks} ({chunkData.Length} bytes)");
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Don't add to errors list - this is expected for alternative implementations
                        // Just skip this barcode silently
                        continue;
                    }
                }
            }

            // Validate all chunks are present (for chunked format)
            if (totalChunks.HasValue)
            {
                for (int i = 1; i <= totalChunks.Value; i++)
                {
                    if (!pdf417Chunks.ContainsKey(i))
                    {
                        result.Errors.Add($"Missing PDF417 chunk {i} of {totalChunks}");
                    }
                }
            }
            
            // If we found direct XML barcodes, store them in metadata
            if (directXmlBarcodes.Count > 0)
            {
                result.Metadata["DirectXmlBarcodes"] = directXmlBarcodes;
                Console.WriteLine($"  ✓ Found {directXmlBarcodes.Count} direct zlib/DEFLATE compressed barcode(s)");
            }

            return pdf417Chunks;
        }

        private static byte[]? ReconstructCompressedData(Dictionary<int, byte[]> chunks, ValidationResult result)
        {
            if (chunks.Count == 0)
            {
                return null;
            }

            Console.WriteLine($"  Reconstructing compressed data from {chunks.Count} chunk(s)...");

            // Calculate total size
            int totalSize = chunks.Values.Sum(c => c.Length);
            byte[] reconstructed = new byte[totalSize];
            int offset = 0;

            // Combine chunks in order
            for (int i = 1; i <= chunks.Count; i++)
            {
                if (chunks.ContainsKey(i))
                {
                    Array.Copy(chunks[i], 0, reconstructed, offset, chunks[i].Length);
                    offset += chunks[i].Length;
                }
            }

            Console.WriteLine($"  ✓ Reconstructed {reconstructed.Length} bytes of compressed data");
            return reconstructed;
        }

        private static string? DecompressXmlData(byte[] compressedData, ValidationResult result)
        {
            try
            {
                Console.WriteLine("  Decompressing XML data (GZIP)...");
                
                // Strip trailing zero padding from last chunk (eCH-0196 requirement: all segments must be 35 rows)
                int dataLength = compressedData.Length;
                while (dataLength > 0 && compressedData[dataLength - 1] == 0)
                {
                    dataLength--;
                }
                
                // Only use the non-padded portion for decompression
                byte[] actualData = new byte[dataLength];
                Array.Copy(compressedData, 0, actualData, 0, dataLength);
                
                // Try zlib first (most common), then raw DEFLATE
                try
                {
                    using var inputStream = new MemoryStream(actualData);
                    using var zlibStream = new ZlibStream(inputStream, SharpCompress.Compressors.CompressionMode.Decompress);
                    using var outputStream = new MemoryStream();
                    
                    zlibStream.CopyTo(outputStream);
                    string xmlContent = Encoding.UTF8.GetString(outputStream.ToArray());
                    
                    Console.WriteLine($"  ✓ Decompressed to {xmlContent.Length} chars");
                    return xmlContent;
                }
                catch
                {
                    // Fallback to raw DEFLATE
                    using var inputStream = new MemoryStream(actualData);
                    using var deflateStream = new SysCompress.DeflateStream(inputStream, SysCompress.CompressionMode.Decompress);
                    using var outputStream = new MemoryStream();
                    
                    deflateStream.CopyTo(outputStream);
                    string xmlContent = Encoding.UTF8.GetString(outputStream.ToArray());
                    
                    Console.WriteLine($"  ✓ Decompressed to {xmlContent.Length} chars (raw DEFLATE)");
                    return xmlContent;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to decompress XML data: {ex.Message}");
                return null;
            }
        }

        private static string? DecompressDirectXmlBarcode(List<string> barcodeTexts, ValidationResult result)
        {
            try
            {
                Console.WriteLine($"  Decompressing XML data (zlib/DEFLATE) from {barcodeTexts.Count} barcode(s)...");
                
                // Concatenate all barcode data as ISO-8859-1 bytes
                using var concatenatedStream = new MemoryStream();
                
                for (int i = 0; i < barcodeTexts.Count; i++)
                {
                    byte[] barcodeBytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(barcodeTexts[i]);
                    concatenatedStream.Write(barcodeBytes, 0, barcodeBytes.Length);
                    
                    if (i == 0)
                    {
                        Console.WriteLine($"    Barcode 1: {barcodeBytes.Length} bytes (with zlib header)");
                    }
                    else
                    {
                        Console.WriteLine($"    Barcode {i + 1}: {barcodeBytes.Length} bytes (raw DEFLATE continuation)");
                    }
                }
                
                byte[] zlibData = concatenatedStream.ToArray();
                Console.WriteLine($"  Total concatenated: {zlibData.Length} bytes");
                Console.WriteLine($"  First 4 bytes: {BitConverter.ToString(zlibData.Take(4).ToArray())}");
                
                // Verify zlib header
                if (zlibData.Length < 2 || zlibData[0] != 0x78)
                {
                    result.Errors.Add($"Invalid zlib header: expected 0x78, got 0x{zlibData[0]:X2}");
                    return null;
                }
                
                // Use SharpCompress ZlibStream for proper zlib decompression (handles dictionaries)
                using var inputStream = new MemoryStream(zlibData);
                using var zlibStream = new ZlibStream(inputStream, SharpCompress.Compressors.CompressionMode.Decompress);
                using var outputStream = new MemoryStream();
                
                zlibStream.CopyTo(outputStream);
                string xmlContent = Encoding.UTF8.GetString(outputStream.ToArray());
                
                Console.WriteLine($"  ✓ Decompressed to {xmlContent.Length} chars");
                return xmlContent;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to decompress zlib barcode: {ex.Message}");
                return null;
            }
        }

        private static bool ValidateXmlStructure(string xmlContent, ValidationResult result)
        {
            try
            {
                Console.WriteLine("  Validating XML structure...");
                
                var doc = XDocument.Parse(xmlContent);
                var root = doc.Root;

                if (root == null)
                {
                    result.Errors.Add("XML has no root element");
                    return false;
                }

                // Check if root element is taxStatement with eCH-0196 namespace
                if (root.Name.LocalName != "taxStatement")
                {
                    result.Errors.Add($"XML root element is '{root.Name.LocalName}', expected 'taxStatement'");
                    return false;
                }

                if (root.Name.NamespaceName != ECH_0196_NAMESPACE)
                {
                    result.Warnings.Add($"XML namespace is '{root.Name.NamespaceName}', expected '{ECH_0196_NAMESPACE}'");
                }

                // Extract metadata
                var minorVersionAttr = root.Attribute("minorVersion");
                if (minorVersionAttr != null)
                {
                    result.Metadata["MinorVersion"] = minorVersionAttr.Value;
                }

                Console.WriteLine("  ✓ XML structure is valid");
                return true;
            }
            catch (XmlException ex)
            {
                result.Errors.Add($"Invalid XML structure: {ex.Message}");
                return false;
            }
        }

        private static bool ValidateAgainstSchema(string xmlContent, string xsdPath, ValidationResult result)
        {
            try
            {
                Console.WriteLine($"  Validating against XSD schema: {xsdPath}");
                
                var schemas = new XmlSchemaSet();
                schemas.Add(ECH_0196_NAMESPACE, xsdPath);

                var doc = XDocument.Parse(xmlContent);
                bool isValid = true;
                var validationErrors = new List<string>();

                doc.Validate(schemas, (sender, e) =>
                {
                    isValid = false;
                    validationErrors.Add($"{e.Severity}: {e.Message}");
                });

                if (!isValid)
                {
                    result.Errors.AddRange(validationErrors);
                    Console.WriteLine($"  ✗ XML validation failed with {validationErrors.Count} error(s)");
                    return false;
                }

                Console.WriteLine("  ✓ XML validates against eCH-0196 schema");
                return true;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Schema validation error: {ex.Message}");
                return false;
            }
        }
    }
}
