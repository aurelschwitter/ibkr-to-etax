using System;
using System.IO;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Xobject;

namespace IbkrToEtax
{
    public class PdfDiagnostic
    {
        public static void DiagnosePdf(string pdfPath)
        {
            Console.WriteLine($"Diagnosing PDF: {pdfPath}");
            Console.WriteLine();

            using var pdfReader = new PdfReader(pdfPath);
            using var pdfDocument = new PdfDocument(pdfReader);

            int pageCount = pdfDocument.GetNumberOfPages();
            Console.WriteLine($"Total pages: {pageCount}");
            Console.WriteLine();

            for (int pageNum = 1; pageNum <= pageCount; pageNum++)
            {
                Console.WriteLine($"Page {pageNum}:");
                var page = pdfDocument.GetPage(pageNum);
                var resources = page.GetResources();
                
                var resourceNames = resources.GetResourceNames();
                Console.WriteLine($"  Resource count: {resourceNames.Count}");

                int imageCount = 0;
                foreach (var name in resourceNames)
                {
                    try
                    {
                        var obj = resources.GetResourceObject(iText.Kernel.Pdf.PdfName.XObject, name);
                        Console.WriteLine($"  Resource {name}:");
                        Console.WriteLine($"    Type: {obj?.GetType().Name}");
                        
                        if (obj != null && obj.IsStream())
                        {
                            var stream = (PdfStream)obj;
                            var subtype = stream.GetAsName(iText.Kernel.Pdf.PdfName.Subtype);
                            Console.WriteLine($"    Subtype: {subtype}");
                            
                            if (subtype != null && subtype.Equals(iText.Kernel.Pdf.PdfName.Image))
                            {
                                imageCount++;
                                Console.WriteLine($"  Image {imageCount} ({name}):");
                                
                                try
                                {
                                    var imageXObject = new PdfImageXObject(stream);
                                    byte[] imageBytes = imageXObject.GetImageBytes();
                                    
                                    Console.WriteLine($"    Size: {imageBytes.Length} bytes");
                                    Console.WriteLine($"    Width: {imageXObject.GetWidth()}");
                                    Console.WriteLine($"    Height: {imageXObject.GetHeight()}");
                                    
                                    // Try to determine format
                                    var filter = stream.GetAsName(iText.Kernel.Pdf.PdfName.Filter);
                                    Console.WriteLine($"    Filter: {filter}");
                                    
                                    // Save image for inspection
                                    string outputPath = $"debug_page{pageNum}_img{imageCount}.raw";
                                    File.WriteAllBytes(outputPath, imageBytes);
                                    Console.WriteLine($"    Saved to: {outputPath}");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"    Error extracting: {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Error processing resource {name}: {ex.Message}");
                    }
                }
                
                if (imageCount == 0)
                {
                    Console.WriteLine("  No images found!");
                }
                
                Console.WriteLine();
            }
        }
    }
}
