# eCH-0196 PDF Validator

This validator checks if a PDF conforms to the Swiss eCH-0196 standard for electronic tax statements.

## Features

The validator performs the following checks:

1. **CODE128C Barcode Validation**
   - Verifies presence of CODE128C barcode on each page
   - Validates barcode format (16 digits)
   - Checks form number (196), version (22), and other fields

2. **PDF417 Barcode Extraction**
   - Extracts PDF417 barcodes from all pages
   - Validates barcode ID consistency
   - Reconstructs chunked data

3. **Data Decompression**
   - Decompresses GZIP/ZLIB compressed XML data
   - Extracts embedded tax statement XML

4. **XML Validation**
   - Validates XML structure and root element
   - Checks for eCH-0196 namespace
   - Optional XSD schema validation

## Usage

### Basic Validation

```csharp
using IbkrToEtax;

// Simple validation
var result = PdfValidator.ValidatePdf("tax-statement.pdf");

if (result.IsValid)
{
    Console.WriteLine("PDF is valid!");
}
else
{
    Console.WriteLine($"Validation failed with {result.Errors.Count} error(s)");
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"  - {error}");
    }
}
```

### Validation with XSD Schema

```csharp
// Validate against eCH-0196 XSD schema
var result = PdfValidator.ValidatePdf(
    "tax-statement.pdf", 
    "eCH-0196-2-2.xsd"
);
```

### Extract Embedded XML

```csharp
var result = PdfValidator.ValidatePdf("tax-statement.pdf");

if (!string.IsNullOrEmpty(result.ExtractedXml))
{
    // Save extracted XML
    File.WriteAllText("extracted.xml", result.ExtractedXml);
    
    // Or process it directly
    var xmlDoc = XDocument.Parse(result.ExtractedXml);
    // ... process XML
}
```

### Access Metadata

```csharp
var result = PdfValidator.ValidatePdf("tax-statement.pdf");

// Get metadata
int pageCount = (int)result.Metadata["PageCount"];
int totalChunks = (int)result.Metadata["TotalChunks"];
string barcodeId = (string)result.Metadata["BarcodeId"];
string minorVersion = (string)result.Metadata["MinorVersion"];

Console.WriteLine($"PDF has {pageCount} pages with {totalChunks} data chunks");
Console.WriteLine($"Barcode ID: {barcodeId}");
```

### Using the Example Class

```csharp
using IbkrToEtax;

// Quick validation
bool isValid = ValidatorExample.QuickValidate("tax-statement.pdf");

// Detailed validation with output
ValidatorExample.ValidatePdfExample("tax-statement.pdf", "eCH-0196-2-2.xsd");

// Batch validation
string[] pdfs = { "file1.pdf", "file2.pdf", "file3.pdf" };
ValidatorExample.ValidateMultiplePdfs(pdfs, "eCH-0196-2-2.xsd");
```

## ValidationResult Structure

The `ValidationResult` object contains:

- **`IsValid`** (bool): Overall validation status
- **`Errors`** (List<string>): List of validation errors
- **`Warnings`** (List<string>): List of validation warnings
- **`ExtractedXml`** (string?): The decompressed XML content from the PDF
- **`Metadata`** (Dictionary<string, object>): Additional metadata:
  - `PageCount`: Number of pages in the PDF
  - `BarcodeId`: Unique barcode identifier (UUID)
  - `TotalChunks`: Number of PDF417 data chunks
  - `MinorVersion`: eCH-0196 minor version from XML
  - `XmlLength`: Length of extracted XML

## eCH-0196 Standard Requirements

The validator checks compliance with:

1. **CODE128C Barcode** (on each page):
   - Format: `196` (form) + `22` (version) + `00000` (org) + `001` (page) + `1` (has2D) + `1` (orient) + `1` (direction)
   - Total length: 16 digits
   - Position: Top-left corner with specified margins

2. **PDF417 Barcode**:
   - Specifications: 13 columns, 35 rows, EC-Level 4
   - Element dimensions: 0.041 cm width, 0.08 cm height
   - Data format: `{UUID}|{ChunkNumber}|{TotalChunks}|{CompressedData}`
   - Compression: GZIP/ZLIB
   - Encoding: Base64 in barcode

3. **XML Structure**:
   - Root element: `taxStatement`
   - Namespace: `http://www.ech.ch/xmlns/eCH-0196/2`
   - Schema validation: eCH-0196-2-2.xsd

## Dependencies

The validator requires the following NuGet packages (already included in the project):

- `itext7` - PDF reading and processing
- `ZXing.Net.Bindings.SkiaSharp` - Barcode reading
- `SkiaSharp` - Image processing

## Error Handling

The validator handles various error conditions:

- Missing or corrupted barcodes
- Invalid barcode formats
- Inconsistent chunk numbering
- Missing data chunks
- Decompression failures
- Invalid XML structure
- Schema validation errors

All errors are collected and returned in the `ValidationResult` object, allowing you to understand exactly what went wrong.

## Example Output

```
Validating PDF against eCH-0196 standard: tax-statement.pdf
  PDF has 2 page(s)
  Validating CODE128C barcodes...
    Page 1: CODE128 barcode valid (Form: 196, Version: 22, Page: 001)
    Page 2: CODE128 barcode valid (Form: 196, Version: 22, Page: 002)
  Extracting PDF417 barcodes...
    Page 1: PDF417 chunk 1/2 (650 bytes)
    Page 2: PDF417 chunk 2/2 (420 bytes)
  Reconstructing compressed data from 2 chunk(s)...
  ✓ Reconstructed 1070 bytes of compressed data
  Decompressing XML data...
  ✓ Successfully extracted XML (8543 chars)
  Validating XML structure...
  ✓ XML structure is valid
  Validating against XSD schema: eCH-0196-2-2.xsd
  ✓ XML validates against eCH-0196 schema
  ✓ PDF conforms to eCH-0196 standard
```
