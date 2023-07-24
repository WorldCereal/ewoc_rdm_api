using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace IIASA.WorldCereal.Rdm.ExcelOps
{
    public static class ExcelOps
    {
        public static List<ExcelMetadata> ExtractCollectionMetadata(Stream stream)
        {
            var metadatas = new List<ExcelMetadata>();
            ISheet sheet;
            var xssWorkbook = new XSSFWorkbook(stream);
            sheet = xssWorkbook.GetSheetAt(0);
            var lastRowNum = sheet.LastRowNum;
            for (int i = 1; i < lastRowNum; i++)
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
                    if (ExcelMarkers.AllowedAttributes.Contains(cellValue))
                    {
                        var metadataCell = row.GetCell(colIndex + 1);
                        var cellMetadataValue = GetCellValue(metadataCell);
                        metadatas.Add(new ExcelMetadata {Name = cellValue, Value = cellMetadataValue});
                    }
                }
            }

            return metadatas;
        }

        public static string GetCellValue(ICell cell)
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
            var template = ExcelMarkers.GetFile("MetaDataTemplate.xlsx");
            var xssWorkbook = new XSSFWorkbook(template);
            WriteMetadata(excelData, xssWorkbook);
            xssWorkbook.Write(stream, true);
            stream.Position = 0;
        }
        
        public static void WriteMetadata(string templatePath, Stream stream, ExcelMetadata[] excelData)
        {
            var xssWorkbook = new XSSFWorkbook(templatePath);
            WriteMetadata(excelData, xssWorkbook);
            xssWorkbook.Write(stream);
        }

        private static void WriteMetadata(ExcelMetadata[] excelData, XSSFWorkbook xssWorkbook)
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
                    if (ExcelMarkers.AllowedAttributes.Contains(cellValue))
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