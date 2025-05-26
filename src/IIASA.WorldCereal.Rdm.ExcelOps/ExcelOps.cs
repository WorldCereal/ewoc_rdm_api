using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using iTextSharp.text;
using iTextSharp.text.pdf;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace IIASA.WorldCereal.Rdm.ExcelOps
{
    public static class ExcelOps
    {
        public static List<ExcelMetadata> ExtractCollectionMetadata(Stream stream)
        {
            return GetMetadataFrom(stream, ExcelMarkers.AllowedMetadataAttributes, string.Empty);
        }

        public static List<ExcelMetadata> ExtractHarmonizationCustomText(Stream stream, string dirPathForImages)
        {
            return GetMetadataFrom(stream,
            [
                ExcelMarkers.FilteringExplain, ExcelMarkers.ObsExplain, ExcelMarkers.ConfidenceScoreExplain,
                ExcelMarkers.GeometryTransformationExplain, ExcelMarkers.SpatialAccuracyExplain,
                ExcelMarkers.CropTypeExplain, ExcelMarkers.IrrigationStatusExplain
            ], dirPathForImages);
        }

        public const string ConfidenceCalcImage = "confidenceCalc";

        private static void SaveImages(ISheet sheet, string dirPath)
        {
            var drawings = sheet.CreateDrawingPatriarch();
            if (drawings is XSSFDrawing xssfDrawing)
            {
                foreach (var shape in xssfDrawing.GetShapes())
                {
                    if (shape is XSSFPicture picture)
                    {
                        var pictureData = picture.PictureData;
                        byte[] imageBytes = pictureData.Data;
                        string extension = pictureData.SuggestFileExtension();

                        // Save the image
                        File.WriteAllBytes(Path.Combine(dirPath, $"{ConfidenceCalcImage}.{extension}"), imageBytes);
                    }
                }
            }
        }

        private static List<ExcelMetadata> GetMetadataFrom(Stream stream, string[] allowedMetadataAttributes,
            string dirPathForImages)
        {
            var metadatas = new List<ExcelMetadata>();
            ISheet sheet;
            var xssWorkbook = new XSSFWorkbook(stream);
            sheet = xssWorkbook.GetSheetAt(0);
            var lastRowNum = sheet.LastRowNum;
            for (int i = 0; i < lastRowNum; i++)
            {
                IRow row = sheet.GetRow(i);

                try
                {
                    if (row == null || row.Cells.All(d => d.CellType == CellType.Blank))
                    {
                        continue;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }

                int cellCount = row.LastCellNum;
                for (int colIndex = 0; colIndex < cellCount; colIndex++)
                {
                    var cell = row.GetCell(colIndex);
                    var cellValue = GetCellValue(cell);
                    if (allowedMetadataAttributes.Contains(cellValue))
                    {
                        var metadataCell = row.GetCell(colIndex + 1);
                        var cellMetadataValue = GetCellValue(metadataCell);
                        metadatas.Add(new ExcelMetadata { Name = cellValue, Value = cellMetadataValue });
                    }
                }
            }

            if (string.IsNullOrEmpty(dirPathForImages) == false)
            {
                SaveImages(sheet, dirPathForImages);
            }

            return metadatas;
        }

        private static string GetCellValue(ICell cell)
        {
            if (cell != null)
            {
                try
                {
                    var cellValue = string.Empty;
                    if (cell.CellType == CellType.String)
                    {
                        cellValue = cell.StringCellValue;
                    }

                    if (cell.CellType == CellType.Numeric)
                    {
                        cellValue = cell.NumericCellValue.ToString(CultureInfo.InvariantCulture);
                    }

                    if (!string.IsNullOrEmpty(cellValue) && !string.IsNullOrWhiteSpace(cellValue))
                    {
                        return cellValue.Trim();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }

            return string.Empty;
        }

        public static void WriteMetadata(Stream stream, ExcelMetadata[] excelData)
        {
            WriteExcels(stream, excelData, "MetaDataTemplate.xlsx", ExcelMarkers.AllowedMetadataAttributes);
        }

        public static void WriteHarmonizationInfos(Stream stream, ExcelMetadata[] excelData,
            List<ExcelMetadata[]> excelTableData)
        {
            WriteExcelsWithTables(stream, excelData, excelTableData, "HarmonizationTemplate.xlsx",
                ExcelMarkers.AllowedHarmonizationAttributes);
        }

        private static string GetMetadataValue(ExcelMetadata[] excelData, string attrName)
        {
            var val = excelData.FirstOrDefault(x => x.Name == attrName);
            if (val == default)
            {
                return string.Empty;
            }

            return val.Value;
        }

        private static Font headingFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
        private static Font subheadingFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
        private static Font paragraphFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);
        private static Font paragraphBold = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);

        public static void GenerateHarmonizationPdf(Stream stream, ExcelMetadata[] excelData,
            List<ExcelMetadata[]> excelTableData, string imagePath)
        {
            using (Document pdfDoc = new Document())
            {
                PdfWriter.GetInstance(pdfDoc, stream);
                pdfDoc.Open();

                // add logo
                // Create an Image object from the specified image file
                Image image = Image.GetInstance("https://rdm.esa-worldcereal.org/ewoc.jpg");
                // Set the image alignment (optional)
                image.Alignment = Element.ALIGN_CENTER;
                image.ScaleToFit(100f, 100f);
                // Add the image to the document
                pdfDoc.Add(image);

                // Add heading
                Paragraph heading = new Paragraph("Harmonization Information", headingFont);
                heading.Alignment = Element.ALIGN_CENTER;
                pdfDoc.Add(heading);

                // Add subheading
                AddSubHeading(subheadingFont, pdfDoc, "General Data");
                // Add paragraph
                AddPara(pdfDoc,
                    "Dataset title:", $" {GetMetadataValue(excelData, ExcelMarkers.HarmonizationTitle)}");
                AddPara(pdfDoc,
                    "Collection ID:", $" {GetMetadataValue(excelData, ExcelMarkers.HarmonizationColId)}");
                AddPara(pdfDoc, "File name:", $" {GetMetadataValue(excelData, ExcelMarkers.FileName)}");
                var orgiName = GetMetadataValue(excelData, ExcelMarkers.OriginalDatasetName);
                if (string.IsNullOrEmpty(orgiName) == false)
                {
                    AddPara(pdfDoc, "Original Dataset Name:", $" {orgiName}");
                }

                AddPara(pdfDoc,
                    "Uploaded date:", $" {GetMetadataValue(excelData, ExcelMarkers.DateUploaded)}");
                AddPara(pdfDoc, "Uploaded by:", $" {GetMetadataValue(excelData, ExcelMarkers.ProviderUserName)}");
                AddPara(pdfDoc,
                    "Contact:", $" {GetMetadataValue(excelData, ExcelMarkers.ProviderContact)}");
                AddPara(pdfDoc,
                    "List of original dataset attribute names:",
                    $" {GetMetadataValue(excelData, ExcelMarkers.DatasetAttributes)}");

                CheckAndAddSubHeading(excelData, subheadingFont, pdfDoc,
                    "Filtering", ExcelMarkers.FilteringExplain);

                CheckAndAddSubHeading(excelData, subheadingFont, pdfDoc,
                    "Geometry Transformations", ExcelMarkers.GeometryTransformationExplain);

                CheckAndAddSubHeading(excelData, subheadingFont, pdfDoc,
                    "Spatial Accuracy", ExcelMarkers.SpatialAccuracyExplain);


                // Add subheading
                AddSubHeading(subheadingFont, pdfDoc, "Observation Date");
                var dateAttribute = GetMetadataValue(excelData, ExcelMarkers.ValDateAttribute);
                if (string.IsNullOrWhiteSpace(dateAttribute) == false)
                {
                    AddPara(pdfDoc,
                        "Attribute selected for observation date:",
                        $" {dateAttribute}");
                }
                else
                {
                    AddPara(pdfDoc,
                        "Selected date for all the observations:",
                        $" {GetMetadataValue(excelData, ExcelMarkers.ManualValDate)}");
                }

                AddPara(pdfDoc,
                    $"{GetMetadataValue(excelData, ExcelMarkers.ObsExplain)}");
                
                // Confidence Score
                //CheckAndAddSubHeading(excelData, subheadingFont, pdfDoc, "Confidence Score", ExcelMarkers.ConfidenceScoreExplain);

                AddSubHeading(subheadingFont, pdfDoc, "Confidence Score");
                if (string.IsNullOrEmpty(imagePath) == false)
                {
                    Image cImage = Image.GetInstance(imagePath);
                    // Set the image alignment (optional)
                    cImage.Alignment = Element.ALIGN_CENTER;
                    cImage.ScaleToFit(500f, 500f);
                    // Add the image to the document
                    pdfDoc.Add(cImage);
                }

                AddPara(pdfDoc, $" {GetMetadataValue(excelData, ExcelMarkers.ConfidenceScoreExplain)}");


                // Add subheading
                AddSubHeading(subheadingFont, pdfDoc, "Crop Type Mapping");
                AddPara(pdfDoc,
                    "Attribute selected for crop label:",
                    $" {GetMetadataValue(excelData, ExcelMarkers.CropTypeAttribute)}");

                var cropMapExplain = GetMetadataValue(excelData, ExcelMarkers.CropTypeExplain);
                if (string.IsNullOrEmpty(cropMapExplain))
                {
                    AddPara(pdfDoc, "Note:", $" {cropMapExplain}");
                }


                PdfPTable tableCrop = new PdfPTable(3); // Number of columns
                tableCrop.SpacingBefore = 10f;
                tableCrop.WidthPercentage = 100;

                tableCrop.AddCell(new PdfPCell(new Phrase("Original Dataset Attribute Value", paragraphBold))
                    { HorizontalAlignment = Element.ALIGN_CENTER });
                tableCrop.AddCell(new PdfPCell(new Phrase("Worldcereal Crop Code", paragraphBold))
                    { HorizontalAlignment = Element.ALIGN_CENTER });
                tableCrop.AddCell(new PdfPCell(new Phrase("Worldcereal Crop Description", paragraphBold))
                    { HorizontalAlignment = Element.ALIGN_CENTER });
                tableCrop.HeaderRows = 1;
                AddMapsToPdf(excelTableData, tableCrop, ExcelMarkers.CropTypeMappings);
                pdfDoc.Add(tableCrop);

                var irrAtrrValue = GetMetadataValue(excelData, ExcelMarkers.IrrStatusAttribute);
                if (string.IsNullOrWhiteSpace(irrAtrrValue) == false)
                {
                    AddSubHeading(subheadingFont, pdfDoc, "Irrigation Status Mapping");
                    AddPara(pdfDoc,
                        $"Attribute Selected for irrigation status mapping:", $"{irrAtrrValue}");
                    var explainIrrMap = GetMetadataValue(excelData, ExcelMarkers.IrrigationStatusExplain);
                    if (string.IsNullOrEmpty(explainIrrMap) == false)
                    {
                        AddPara(pdfDoc, "Note:", $" {explainIrrMap}");
                    }

                    PdfPTable table = new PdfPTable(3)
                    {
                        WidthPercentage = 100
                    }; // Number of columns
                    table.SpacingBefore = 10f;
                    table.WidthPercentage = 100;
                    table.AddCell(new PdfPCell(new Phrase("Original Dataset Attribute Value", paragraphBold))
                        { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase("Worldcereal Irrigation Status Code", paragraphBold))
                        { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(
                        new PdfPCell(new Phrase("Worldcereal Irrigation Status Description", paragraphBold))
                            { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.HeaderRows = 1;
                    AddMapsToPdf(excelTableData, table, ExcelMarkers.IrrStatusMappings);
                    pdfDoc.Add(table);
                }

                pdfDoc.Close();
            }
        }

        private static void AddMapsToPdf(List<ExcelMetadata[]> excelTableData, PdfPTable table,
            string cropTypeAttribute)
        {
            var cropMaps = excelTableData.SelectMany(x => x.Where(y => y.Name == cropTypeAttribute));
            foreach (var metadata in cropMaps)
            {
                var values = metadata.Value.Split(";");
                table.AddCell(values[0]);
                table.AddCell(values[1]);
                table.AddCell(values[2]);
            }
        }

        private static void CheckAndAddSubHeading(ExcelMetadata[] excelData, Font subHeadingFont, Document pdfDoc,
            string subHeadingText, string attributeName)
        {
            var value = GetMetadataValue(excelData, attributeName);
            if (string.IsNullOrEmpty(value) == false)
            {
                AddSubHeading(subHeadingFont, pdfDoc, subHeadingText);
                AddPara(pdfDoc, value);
            }
        }

        private static void AddPara(Document pdfDoc, string text)
        {
            Paragraph paragraph = new Paragraph(text, paragraphFont);
            paragraph.SpacingBefore = 5f; // Add spacing before the paragraph
            pdfDoc.Add(paragraph);
        }

        private static void AddPara(Document pdfDoc, string bold, string text)
        {
            Chunk boldText = new Chunk(bold, paragraphBold);
            // Create another normal chunk
            Chunk normalText = new Chunk(text, paragraphFont);

            Paragraph paragraph = new Paragraph
            {
                SpacingBefore = 5f // Add spacing before the paragraph
            };
            paragraph.Add(boldText);
            paragraph.Add(normalText);
            pdfDoc.Add(paragraph);
        }

        private static void AddSubHeading(Font subheadingFont, Document pdfDoc, string text)
        {
            Paragraph subheading = new Paragraph(text, subheadingFont);
            subheading.SpacingBefore = 20f;
            subheading.Alignment = Element.ALIGN_LEFT;
            pdfDoc.Add(subheading);

            String line =
                "____________________________________________________________________________________________________________________________________________________________________________________"; // or any other string representing a line
            Paragraph element = new Paragraph(line, new Font(Font.FontFamily.HELVETICA, 5));
            element.SpacingAfter = 5f;
            pdfDoc.Add(element);
        }

        private static void WriteExcels(Stream stream, ExcelMetadata[] excelData, string fileName,
            string[] allowedAttributes)
        {
            var template = ExcelMarkers.GetFile(fileName);
            var xssWorkbook = new XSSFWorkbook(template);
            WriteExcelData(excelData, xssWorkbook, allowedAttributes);
            xssWorkbook.Write(stream, true);
            stream.Position = 0;
        }

        private static void WriteExcelsWithTables(Stream stream, ExcelMetadata[] excelData,
            List<ExcelMetadata[]> excelTableData, string fileName, string[] allowedAttributes)
        {
            var template = ExcelMarkers.GetFile(fileName);
            var xssWorkbook = new XSSFWorkbook(template);
            WriteExcelData(excelData, xssWorkbook, allowedAttributes);
            foreach (var tableData in excelTableData)
            {
                WriteExcelDataTable(tableData, xssWorkbook);
            }

            xssWorkbook.Write(stream, true);
            stream.Position = 0;
        }

        private static void WriteExcelDataTable(ExcelMetadata[] tableData, XSSFWorkbook xssWorkbook)
        {
            if (tableData.Any() == false)
            {
                return;
            }

            var tableKey = tableData.First().Name;

            ISheet sheet;
            sheet = xssWorkbook.GetSheetAt(0);
            var lastRowNum = sheet.LastRowNum;
            for (int i = 0; i < lastRowNum; i++)
            {
                IRow row = sheet.GetRow(i);
                try
                {
                    if (row == null || row.Cells.All(d => d.CellType == CellType.Blank))
                    {
                        continue;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }

                int cellCount = row.LastCellNum;
                for (int j = 0; j < cellCount; j++)
                {
                    var cell = row.GetCell(j);
                    var cellValue = GetCellValue(cell);
                    if (tableKey == cellValue)
                    {
                        var increment = 1;
                        sheet.ShiftRows(i + increment, sheet.LastRowNum, tableData.Length);
                        // Set borders for all sides
                        ICellStyle style = xssWorkbook.CreateCellStyle();
                        style.BorderTop = BorderStyle.Thin;
                        style.BorderBottom = BorderStyle.Thin;
                        style.BorderLeft = BorderStyle.Thin;
                        style.BorderRight = BorderStyle.Thin;
                        foreach (var excelData in tableData)
                        {
                            var newRow = sheet.CreateRow(i + increment);
                            var values = excelData.Value.Split(";");

                            var cell1 = newRow.CreateCell(0);
                            SetCellValue(cell1, values[0]);
                            cell1.CellStyle = style;
                            var cell2 = newRow.CreateCell(2);
                            SetCellValue(cell2, values[1]);
                            cell2.CellStyle = style;
                            var cell3 = newRow.CreateCell(3);
                            SetCellValue(cell3, values[2]);
                            cell3.CellStyle = style;
                            increment++;
                        }

                        return;
                    }
                }
            }
        }

        private static void WriteExcelData(ExcelMetadata[] excelData, XSSFWorkbook xssWorkbook,
            string[] allowedAttributes)
        {
            ISheet sheet;
            sheet = xssWorkbook.GetSheetAt(0);
            var lastRowNum = sheet.LastRowNum;
            for (int i = 0; i < lastRowNum; i++)
            {
                IRow row = sheet.GetRow(i);
                try
                {
                    if (row == null || row.Cells.All(d => d.CellType == CellType.Blank))
                    {
                        continue;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }

                int cellCount = row.LastCellNum;
                for (int j = 0; j < cellCount; j++)
                {
                    var cell = row.GetCell(j);
                    var cellValue = GetCellValue(cell);
                    if (allowedAttributes.Contains(cellValue))
                    {
                        if (excelData.Any(x => x.Name == cellValue))
                        {
                            var metadataItem = excelData.First(x => x.Name == cellValue);
                            var metadataCell = row.GetCell(j + 1);
                            SetCellValue(metadataCell, metadataItem.Value);
                        }
                    }
                }
            }
        }

        private static void SetCellValue(ICell cell, string value)
        {
            if (cell != null)
            {
                try
                {
                    cell.SetCellValue(value);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }
    }
}