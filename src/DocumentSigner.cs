using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace IbkrToEtax
{
    /// <summary>
    /// Provides digital signature functionality for XML documents.
    /// Note: PDF signing requires complex iText BouncyCastle integration and is not currently implemented.
    /// For PDF signing, consider using external tools like Adobe Acrobat or pdfsignaturepad.
    /// </summary>
    public static class DocumentSigner
    {
        /// <summary>
        /// Signs an XML document with XMLDSig signature
        /// </summary>
        /// <param name="xmlPath">Path to the XML file to sign</param>
        /// <param name="certificate">X509 certificate with private key for signing</param>
        /// <param name="outputPath">Optional output path (overwrites input if not specified)</param>
        public static void SignXml(string xmlPath, X509Certificate2 certificate, string? outputPath = null)
        {
            if (!File.Exists(xmlPath))
            {
                throw new FileNotFoundException($"XML file not found: {xmlPath}");
            }

            if (certificate == null || !certificate.HasPrivateKey)
            {
                throw new ArgumentException("Certificate must have a private key for signing");
            }

            Console.WriteLine($"Signing XML: {xmlPath}");

            // Load the XML document
            var doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            doc.Load(xmlPath);

            // Create a SignedXml object
            SignedXml signedXml = new SignedXml(doc);
            signedXml.SigningKey = certificate.GetRSAPrivateKey();

            // Add reference to the document (sign the entire document)
            Reference reference = new Reference();
            reference.Uri = ""; // Empty URI means sign the entire document

            // Add enveloped signature transform
            XmlDsigEnvelopedSignatureTransform env = new XmlDsigEnvelopedSignatureTransform();
            reference.AddTransform(env);

            // Add C14N transform (canonicalization)
            XmlDsigC14NTransform c14n = new XmlDsigC14NTransform();
            reference.AddTransform(c14n);

            signedXml.AddReference(reference);

            // Add the certificate's public key to the signature
            KeyInfo keyInfo = new KeyInfo();
            keyInfo.AddClause(new KeyInfoX509Data(certificate));
            signedXml.KeyInfo = keyInfo;

            // Compute the signature
            signedXml.ComputeSignature();

            // Get the XML representation of the signature
            XmlElement xmlDigitalSignature = signedXml.GetXml();

            // Append the signature to the document
            doc.DocumentElement!.AppendChild(doc.ImportNode(xmlDigitalSignature, true));

            // Save the signed document
            string output = outputPath ?? xmlPath;
            doc.Save(output);

            Console.WriteLine($"✓ XML signed successfully: {output}");
        }

        /// <summary>
        /// Verifies the XML digital signature
        /// </summary>
        /// <param name="xmlPath">Path to the signed XML file</param>
        /// <returns>True if signature is valid</returns>
        public static bool VerifyXmlSignature(string xmlPath)
        {
            var doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            doc.Load(xmlPath);

            // Find the Signature element
            XmlNodeList? nodeList = doc.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl);
            
            if (nodeList == null || nodeList.Count == 0)
            {
                Console.WriteLine("No signature found in XML");
                return false;
            }

            // Load the signature
            SignedXml signedXml = new SignedXml(doc);
            signedXml.LoadXml((XmlElement)nodeList[0]!);

            // Verify the signature
            bool isValid = signedXml.CheckSignature();
            
            if (isValid)
            {
                Console.WriteLine("✓ XML signature is valid");
            }
            else
            {
                Console.WriteLine("✗ XML signature is invalid");
            }
            
            return isValid;
        }
    }
}
