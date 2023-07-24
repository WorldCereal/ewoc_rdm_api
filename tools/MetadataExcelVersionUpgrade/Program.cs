using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
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
            Console.WriteLine("3. Map codes.");
            var read = Console.ReadLine();
            switch (read)
            {
                case "1":
                    UpgradeMetadataExcelVersions();
                    break;
                case "2":
                    GenerateConfCsv();
                    break;
                case "3":
                    Console.WriteLine("Enter From csv");
                    var fromCSv = Console.ReadLine();
                    Console.WriteLine($"Enter to csv");
                    var toCsv = Console.ReadLine();
                    GenerateMap(fromCSv, toCsv);
                    break;
                default:
                    Console.WriteLine("Press Any key to exit");
                    Console.Read();
                    Environment.Exit(0);
                    break;
            }
        }

        private static void GenerateMap(string fromCsv, string toCsv)
        {
            var fromList = new List<MapData>();
            var encoding = Encoding.GetEncoding("iso-8859-1");//Encoding.UTF8;
            using (CsvReader reader = new CsvReader(new StreamReader(fromCsv, encoding),
                new CsvConfiguration(CultureInfo.InvariantCulture) {Delimiter = ",", BadDataFound = BadDataFunc}))
            {
                var i = 0;
                reader.Read();
                
                while (reader.Read())
                {
                    Console.WriteLine($"Reading record- {i}");
                    var data = new MapData
                    {
                        DbId = reader.GetField<int>(0),
                        Code = reader.GetField<string>(1),
                        Text = reader.GetField<string>(2),
                        EngText = reader.GetField<string>(3),
                        Lc = reader.GetField<int>(4),
                        Ct = reader.GetField<int>(5)
                    };
                    fromList.Add(data);
                }
            }
            
            var toList = new List<MapData>();
            using (CsvReader reader = new CsvReader(new StreamReader(toCsv, encoding),
                new CsvConfiguration(CultureInfo.InvariantCulture) {Delimiter = ",", BadDataFound = BadDataFunc}))
            {
                var i = 0;
                reader.Read();
                
                while (reader.Read())
                {
                    Console.WriteLine($"Reading record- {i}");
                    var data = new MapData
                    {
                        Code = reader.GetField<string>(0),
                        Text = reader.GetField<string>(1),
                        EngText = reader.GetField<string>(2),
                      
                    };
                    toList.Add(data);
                }
            }

            var results = new List<MapData>();
            foreach (var map in toList)    
            {
                if (fromList.Any(x => x.Code == map.Code))
                {
                    var fromData = fromList.First(x => x.Code == map.Code);
                    map.Lc = fromData.Lc;
                    map.Ct = fromData.Ct;
                    map.DbId = fromData.DbId;
                    map.EngText = fromData.EngText;
                }
                
                results.Add(map);
            }

            var missed = fromList.Where(x => results.Any(y => y.Code == x.Code) == false);
            results.AddRange(missed);

            using (TextWriter file = new StreamWriter(".\\mapped.csv", false, Encoding.UTF8))
            {
                CsvSerializer csv = new CsvSerializer(file, CultureInfo.InvariantCulture);
                csv.Write(new[] {"dbID,Shp Code,French,English,LC,CT,IRR"});
                csv.WriteLine();
                foreach (var result in results)
                {
                    csv.Write(new []{result.DbId.ToString(),result.Code,result.Text,result.EngText,result.Lc.ToString(),result.Ct.ToString(),});
                    csv.WriteLine();
                }
            }
        }

        private static void BadDataFunc(ReadingContext obj)
        {
            Console.WriteLine($"Error-{obj.RawRecord}");
        }


        class MapData
        {
            public string Code { get; set; }
            public string Text { get; set; }
            public string EngText { get; set; }

            public int DbId { get; set; }
            public int Lc { get; set; }
            public int Ct { get; set; }
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

            File.WriteAllText("scores.csv", strBuilder.ToString());
        }

        private static string GetValue(IEnumerable<ExcelMetadata> cells, string name)
        {
            return cells.First(x => x.Name.Contains(name)).Value;
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

            ExcelOps.WriteMetadata(template, File.OpenWrite(outPutFile), excelMetadatas);
        }
    }
}