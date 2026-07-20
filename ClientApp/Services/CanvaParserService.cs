using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SkiaSharp;
using PDFtoImage;
using UglyToad.PdfPig;
using static ClientApp.CustomTemplateDesignerWindow;

namespace ClientApp.Services
{
    /// <summary>
    /// Parses a Canva-exported PDF by rendering it as a pixel-perfect background image.
    /// Erases placeholders on the background image and places corresponding WPF dynamic blocks instead.
    /// </summary>
    public static class CanvaParserService
    {
        private class DetectedPlaceholder
        {
            public string Name { get; set; } = "";
            public double PdfLeft { get; set; }
            public double PdfRight { get; set; }
            public double PdfBottom { get; set; }
            public double PdfTop { get; set; }
            public double PdfFontSize { get; set; }
            public string FontFamily { get; set; } = "Inter";
            public bool IsBold { get; set; }
            public bool IsItalic { get; set; }
            public string ColorHex { get; set; } = "#000000";
        }

        private static string ResolvePlaceholderSynonym(string name)
        {
            name = name.Trim().ToLowerInvariant();
            return name switch
            {
                "company_name" => "name",
                "company_address" => "address",
                "company_phone" => "phone",
                "description" => "issue",
                "issue_description" => "issue",
                "order_id" => "memo_id",
                "order_number" => "memo_id",
                "id" => "memo_id",
                "order_date" => "date",
                "memo_date" => "date",
                "customer" => "customer_name",
                "phone_number" => "customer_phone",
                "contact" => "customer_phone",
                "client_address" => "customer_address",
                "device_brand" => "brand",
                "device_model" => "model",
                "device" => "model",
                "product_name" => "product_name",
                "device_name" => "product_name",
                "technician" => "technician_name",
                "tech" => "technician_name",
                "estimated_cost" => "cost",
                "price" => "cost",
                "amount" => "cost",
                _ => name
            };
        }

        private static double GetIdealWidthForPlaceholder(string name)
        {
            return name switch
            {
                "memo_id" => 150.0,
                "date" => 150.0,
                "customer_name" => 250.0,
                "customer_phone" => 180.0,
                "customer_address" => 350.0,
                "brand" => 150.0,
                "model" => 200.0,
                "product_name" => 200.0,
                "serial_number" => 200.0,
                "accessories" => 350.0,
                "issue" => 450.0,
                "diagnostics" => 450.0,
                "cost" => 150.0,
                "technician_name" => 220.0,
                "name" => 300.0,
                "address" => 400.0,
                "phone" => 220.0,
                "terms" => 600.0,
                _ => 200.0
            };
        }

        /// <summary>
        /// Renders the first page of the PDF as a high-resolution PNG background image
        /// and returns a list of DesignerBlocks (background image + dynamic fields).
        /// </summary>
        public static List<DesignerBlock> ParseCanvaPdf(
            string pdfPath,
            bool isHalf,
            out double wpfWidth,
            out double wpfHeight,
            string? templateName = null)
        {
            wpfWidth  = 794.0;
            wpfHeight = isHalf ? 561.0 : 1123.0;

            var blocks = new List<DesignerBlock>();
            var detectedPlaceholders = new List<DetectedPlaceholder>();

            var placeholders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "memo_id", "date", "customer_name", "customer_phone", "customer_address",
                "brand", "model", "product_name", "serial_number", "accessories", "issue", "diagnostics",
                "cost", "technician_name", "name", "address", "phone", "terms"
            };

            double pdfW = wpfWidth;
            double pdfH = wpfHeight;

            // ── Detect actual page size and parse placeholders from PdfPig ──────────────────────────────
            try
            {
                using (var doc = PdfDocument.Open(pdfPath))
                {
                    if (doc.NumberOfPages >= 1)
                    {
                        var page = doc.GetPage(1);
                        pdfW = (double)page.Width;
                        pdfH = (double)page.Height;
                        
                        // Keep WPF canvas at standard A4 width (794px); adjust height for aspect
                        double aspect = pdfH / pdfW;
                        wpfHeight = isHalf ? 561.0 : Math.Round(wpfWidth * aspect);

                        var regex = new Regex(@"\{\{?\s*([a-zA-Z0-9_]+)\s*\}?\}", RegexOptions.IgnoreCase);

                        // ── Layer 1: Word-level scanning (highly robust against scrambled PDF text) ───
                        var words = page.GetWords().ToList();
                        foreach (var word in words)
                        {
                            var wordMatches = regex.Matches(word.Text);
                            foreach (Match match in wordMatches)
                            {
                                string placeholderName = match.Groups[1].Value.ToLowerInvariant();
                                string resolvedName = ResolvePlaceholderSynonym(placeholderName);

                                if (placeholders.Contains(resolvedName))
                                {
                                    int matchStart = match.Index;
                                    int matchLength = match.Length;

                                    var wordLetters = word.Letters.ToList();
                                    var letterIndices = new List<int>();
                                    string wordStr = "";
                                    for (int i = 0; i < wordLetters.Count; i++)
                                    {
                                        string val = wordLetters[i].Value;
                                        for (int k = 0; k < val.Length; k++)
                                        {
                                            letterIndices.Add(i);
                                        }
                                        wordStr += val;
                                    }

                                    List<UglyToad.PdfPig.Content.Letter> matchedLetters;
                                    if (matchStart + matchLength <= letterIndices.Count)
                                    {
                                        int startL = letterIndices[matchStart];
                                        int endL = letterIndices[matchStart + matchLength - 1];
                                        matchedLetters = wordLetters.GetRange(startL, endL - startL + 1);
                                    }
                                    else
                                    {
                                        matchedLetters = wordLetters;
                                    }

                                    if (matchedLetters.Count > 0 && !detectedPlaceholders.Any(dp => dp.Name == resolvedName))
                                    {
                                        double minX = matchedLetters.Min(l => l.GlyphRectangle.Left);
                                        double maxX = matchedLetters.Max(l => l.GlyphRectangle.Right);
                                        double minY = matchedLetters.Min(l => l.GlyphRectangle.Bottom);
                                        double maxY = matchedLetters.Max(l => l.GlyphRectangle.Top);

                                        var firstLetter = matchedLetters.First();
                                        double pdfFontSize = firstLetter.FontSize;
                                        string fontName = firstLetter.FontName ?? "Inter";

                                        bool isBold = fontName.Contains("Bold", StringComparison.OrdinalIgnoreCase) ||
                                                     fontName.Contains("Heavy", StringComparison.OrdinalIgnoreCase) ||
                                                     fontName.Contains("Black", StringComparison.OrdinalIgnoreCase);

                                        bool isItalic = fontName.Contains("Italic", StringComparison.OrdinalIgnoreCase) ||
                                                       fontName.Contains("Oblique", StringComparison.OrdinalIgnoreCase);

                                        if (fontName.Contains("+")) fontName = fontName.Split('+')[1];
                                        if (fontName.Contains("-")) fontName = fontName.Split('-')[0];
                                        if (fontName.Contains(",")) fontName = fontName.Split(',')[0];

                                        detectedPlaceholders.Add(new DetectedPlaceholder
                                        {
                                            Name = resolvedName,
                                            PdfLeft = minX,
                                            PdfRight = maxX,
                                            PdfBottom = minY,
                                            PdfTop = maxY,
                                            PdfFontSize = pdfFontSize,
                                            FontFamily = fontName,
                                            IsBold = isBold,
                                            IsItalic = isItalic
                                        });
                                    }
                                }
                            }
                        }

                        // ── Layer 2: Page-level letter scanning (handles any text split across words) ──
                        var letters = page.Letters.ToList();
                        string fullText = "";
                        var allLetterIndices = new List<int>();

                        for (int i = 0; i < letters.Count; i++)
                        {
                            string val = letters[i].Value;
                            for (int k = 0; k < val.Length; k++)
                            {
                                allLetterIndices.Add(i);
                            }
                            fullText += val;
                        }

                        var pageMatches = regex.Matches(fullText);
                        foreach (Match match in pageMatches)
                        {
                            string placeholderName = match.Groups[1].Value.ToLowerInvariant();
                            string resolvedName = ResolvePlaceholderSynonym(placeholderName);

                            if (placeholders.Contains(resolvedName) && !detectedPlaceholders.Any(dp => dp.Name == resolvedName))
                            {
                                int startCharIdx = match.Index;
                                int length = match.Length;

                                int startLetterIdx = allLetterIndices[startCharIdx];
                                int endLetterIdx = allLetterIndices[startCharIdx + length - 1];

                                var matchedLetters = new List<UglyToad.PdfPig.Content.Letter>();
                                for (int i = startLetterIdx; i <= endLetterIdx; i++)
                                {
                                    matchedLetters.Add(letters[i]);
                                }

                                if (matchedLetters.Count > 0)
                                {
                                    double minX = matchedLetters.Min(l => l.GlyphRectangle.Left);
                                    double maxX = matchedLetters.Max(l => l.GlyphRectangle.Right);
                                    double minY = matchedLetters.Min(l => l.GlyphRectangle.Bottom);
                                    double maxY = matchedLetters.Max(l => l.GlyphRectangle.Top);

                                    var firstLetter = matchedLetters.First();
                                    double pdfFontSize = firstLetter.FontSize;
                                    string fontName = firstLetter.FontName ?? "Inter";

                                    bool isBold = fontName.Contains("Bold", StringComparison.OrdinalIgnoreCase) ||
                                                 fontName.Contains("Heavy", StringComparison.OrdinalIgnoreCase) ||
                                                 fontName.Contains("Black", StringComparison.OrdinalIgnoreCase);

                                    bool isItalic = fontName.Contains("Italic", StringComparison.OrdinalIgnoreCase) ||
                                                   fontName.Contains("Oblique", StringComparison.OrdinalIgnoreCase);

                                    if (fontName.Contains("+")) fontName = fontName.Split('+')[1];
                                    if (fontName.Contains("-")) fontName = fontName.Split('-')[0];
                                    if (fontName.Contains(",")) fontName = fontName.Split(',')[0];

                                    detectedPlaceholders.Add(new DetectedPlaceholder
                                    {
                                        Name = resolvedName,
                                        PdfLeft = minX,
                                        PdfRight = maxX,
                                        PdfBottom = minY,
                                        PdfTop = maxY,
                                        PdfFontSize = pdfFontSize,
                                        FontFamily = fontName,
                                        IsBold = isBold,
                                        IsItalic = isItalic
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CanvaParser] PDF structure analysis failed: {ex.Message}");
            }

            // ── Render the PDF page to a PNG & Erase detected placeholders ─────────────────────────────
            string? bgImagePath = null;
            try
            {
                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ServiceMemoApp", "templates");
                Directory.CreateDirectory(appDataPath);

                string safeName = string.IsNullOrWhiteSpace(templateName)
                    ? $"canva_{DateTime.Now:yyyyMMddHHmmss}"
                    : string.Concat(templateName.Split(Path.GetInvalidFileNameChars()));

                string bgPath = Path.Combine(appDataPath, safeName + "_bg.png");

                using (var pdfStream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read))
                {
                    var pages = Conversion.ToImages(pdfStream, options: new(Dpi: 200)).ToList();

                    if (pages.Count > 0)
                    {
                        using (var bitmap = pages[0])
                        {
                            using (var canvas = new SKCanvas(bitmap))
                            {
                                foreach (var dp in detectedPlaceholders)
                                {
                                    double imgLeft = (dp.PdfLeft / pdfW) * bitmap.Width;
                                    double imgRight = (dp.PdfRight / pdfW) * bitmap.Width;
                                    double imgTop = (1.0 - (dp.PdfTop / pdfH)) * bitmap.Height;
                                    double imgBottom = (1.0 - (dp.PdfBottom / pdfH)) * bitmap.Height;

                                    int sampleX = (int)Math.Clamp(imgLeft - 8, 0, bitmap.Width - 1);
                                    int sampleY = (int)Math.Clamp((imgTop + imgBottom) / 2, 0, bitmap.Height - 1);
                                    if (sampleX == 0) sampleX = (int)Math.Clamp(imgRight + 8, 0, bitmap.Width - 1);
                                    var sampleColor = bitmap.GetPixel(sampleX, sampleY);

                                    SKColor textColor = SKColors.Black;
                                    double maxDiff = 0;
                                    for (int y = (int)Math.Max(0, imgTop); y < (int)Math.Min(bitmap.Height, imgBottom); y++)
                                    {
                                        for (int x = (int)Math.Max(0, imgLeft); x < (int)Math.Min(bitmap.Width, imgRight); x++)
                                        {
                                            var px = bitmap.GetPixel(x, y);
                                            double diff = Math.Abs(px.Red - sampleColor.Red) +
                                                          Math.Abs(px.Green - sampleColor.Green) +
                                                          Math.Abs(px.Blue - sampleColor.Blue);
                                            if (diff > maxDiff)
                                            {
                                                maxDiff = diff;
                                                textColor = px;
                                            }
                                        }
                                    }

                                    dp.ColorHex = $"#{textColor.Red:X2}{textColor.Green:X2}{textColor.Blue:X2}";

                                    var eraseRect = new SKRect(
                                        (float)(imgLeft - 2),
                                        (float)(imgTop - 2),
                                        (float)(imgRight + 2),
                                        (float)(imgBottom + 2)
                                    );

                                    using (var paint = new SKPaint
                                    {
                                        Color = sampleColor,
                                        Style = SKPaintStyle.Fill
                                    })
                                    {
                                        canvas.DrawRect(eraseRect, paint);
                                    }
                                }
                            }

                            using (var modifiedImage = SKImage.FromBitmap(bitmap))
                            using (var pngData = modifiedImage.Encode(SKEncodedImageFormat.Png, 95))
                            using (var output = File.Create(bgPath))
                            {
                                pngData.SaveTo(output);
                                bgImagePath = bgPath;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CanvaParser] Render/Erase failed: {ex.Message}");
                throw new InvalidOperationException(
                    $"Could not render the PDF page as an image.\n\nDetails: {ex.Message}\n\n" +
                    "Make sure the PDF is a valid Canva export and is not password-protected.", ex);
            }

            if (string.IsNullOrEmpty(bgImagePath))
                throw new InvalidOperationException("PDF rendering produced no output. Please try exporting from Canva again.");

            blocks.Add(new DesignerBlock
            {
                Id        = "image",
                X         = 0,
                Y         = 0,
                Width     = wpfWidth,
                Height    = wpfHeight,
                ImagePath = bgImagePath,
                IsHalfA4  = isHalf,
                Opacity   = 1.0
            });

            foreach (var dp in detectedPlaceholders)
            {
                double wpfLeft = (dp.PdfLeft / pdfW) * wpfWidth;
                double wpfRight = (dp.PdfRight / pdfW) * wpfWidth;
                
                double wpfFontSize = dp.PdfFontSize * (wpfWidth / pdfW);

                double idealWidth = GetIdealWidthForPlaceholder(dp.Name);
                double wpfWidthVal = Math.Max(wpfRight - wpfLeft, idealWidth);

                if (wpfLeft + wpfWidthVal > wpfWidth)
                {
                    wpfWidthVal = wpfWidth - wpfLeft - 10;
                }

                double wpfTop = ((pdfH - dp.PdfTop) / pdfH) * wpfHeight;
                double wpfBottom = ((pdfH - dp.PdfBottom) / pdfH) * wpfHeight;
                
                double wpfHeightVal = Math.Max(wpfBottom - wpfTop, wpfFontSize * 1.5);

                blocks.Add(new DesignerBlock
                {
                    Id = dp.Name,
                    X = wpfLeft,
                    Y = wpfTop,
                    Width = Math.Max(wpfWidthVal, 10),
                    Height = Math.Max(wpfHeightVal, 10),
                    FontSize = Math.Max(wpfFontSize, 6),
                    FontFamily = dp.FontFamily,
                    IsBold = dp.IsBold,
                    IsItalic = dp.IsItalic,
                    ColorHex = dp.ColorHex,
                    IsHalfA4 = isHalf,
                    TextAlignment = "Left",
                    Opacity = 1.0
                });
            }

            return blocks;
        }
    }
}
