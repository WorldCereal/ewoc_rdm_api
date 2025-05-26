using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IIASA.WorldCereal.Rdm.ExcelOps;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace MetadataExcelVersionUpgrade
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to Metadata version Upgrade (MetaData_version_1_1.xlsx)");
            Console.WriteLine("1. Upgrade metadata excels");
            Console.WriteLine("2. Generate confidence csv from metadata excel.");
            var read = Console.ReadLine();
            switch (read)
            {
                case "1":
                    UpgradeMetadataExcelVersions();
                    break;
                case "2":
                    GenerateConfCsv();
                    break;
                default:
                    Console.WriteLine("Press Any key to exit");
                    Console.Read();
                    Environment.Exit(0);
                    break;
            }
        }

        private static void GenerateConfCsv()
        {
            Console.WriteLine("Enter the folder path which contains metadata excels");
            var dirPath = Console.ReadLine();
            var files = GetFiles(dirPath, out var outputDir);
            var strBuilder = new StringBuilder();
            strBuilder.AppendLine("filename, LC Conf, CT Conf, IRR Conf");
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                
                using (var fileStream = File.OpenRead(file))
                {
                    var cellValues = GetMetadataFromFile(fileStream, 7, 3);
                    var cells = cellValues.Where(x => x.Name.ToLowerInvariant().Contains("confidence")).ToList();
                    strBuilder.AppendLine(
                        $"{fileInfo.Name},{GetValue(cells, "LandCover")},{GetValue(cells, "CropType")},{GetValue(cells, "Irrigation")}");
                }
            }
            File.WriteAllText("scores.csv",strBuilder.ToString());
        }

        private static string GetValue(IEnumerable<ExcelMetadata> cells, string name)
        {
            return cells.First(x=>x.Name.Contains(name)).Value;
        }

        private static void UpgradeMetadataExcelVersions()
        {
            Console.WriteLine("Enter the folder path which contains old metadata excels");
            var dirPath = Console.ReadLine();

            if (Directory.Exists(dirPath) == false)
            {
                Console.WriteLine("Error- Directory Does not exits!");
            }

            var files = GetFiles(dirPath, out var outputDir);
            foreach (var file in files)
            {
                var outPutFile = Path.Combine(outputDir.FullName, new FileInfo(file).Name);
                ExcelMetadata[] metadataItems;
                using (var fileStream = File.OpenRead(file))
                {
                    metadataItems = ExcelOps.ExtractCollectionMetadata(fileStream).ToArray();
                }

                WriteToFile(outPutFile, metadataItems);
            }
        }

        private static string[] GetFiles(string dirPath, out DirectoryInfo outputDir)
        {
            var files = Directory.GetFiles(dirPath, "*.xlsx", SearchOption.AllDirectories);

            if (files.Length == 0)
            {
                Console.WriteLine("Error- No metadata excels found");
            }

            outputDir = Directory.CreateDirectory(Path.Combine(dirPath, "new_version"));
            Console.WriteLine($"Found {files.Length} excel workbooks");
            return files;
        }

        private static ExcelMetadata[] GetMetadataFromFile(FileStream fileStream, int valueCell, int lastIndexForMarker)
        {
            var metadatas = new List<ExcelMetadata>();
            ISheet sheet;
            var xssWorkbook = new XSSFWorkbook(fileStream);
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
                
                var markerText = GetMarkerText(row, lastIndexForMarker);
                if (string.IsNullOrEmpty(markerText))
                {
                   continue;
                }
                var cellValue = ExcelOps.GetCellValue(row.GetCell(valueCell));
                Console.WriteLine($"Reading marker- {markerText} and cellValue- {cellValue}");
                metadatas.Add(new ExcelMetadata {Name = markerText, Value = cellValue});
            }
            return metadatas.ToArray();
        }

        private static string GetMarkerText(IRow row, int startIndex)
        {
            var marker = string.Empty;
            for (int i = 0; i < 3; i++)
            {
                marker = ExcelOps.GetCellValue(row.GetCell(startIndex - i));
                if (string.IsNullOrEmpty(marker))
                {
                    continue;
                }
                break;
            }
            if (string.IsNullOrEmpty(marker))
            {
                return string.Empty;
            }
            return ExcelMarkers.AllowedAttributes.FirstOrDefault(x => x.Contains(marker));
        }


        private static void WriteToFile(string outPutFile, ExcelMetadata[] excelMetadatas)
        {
            var template = ExcelMarkers.GetFile("MetaDataTemplate.xlsx");
            if (File.Exists(outPutFile) == false)
            {
                var stream = File.Create(outPutFile);
                stream.Dispose();
            }
            
            ExcelOps.WriteMetadata(template,File.OpenWrite(outPutFile), excelMetadatas);
        }
    }
}