using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using IIASA.WorldCereal.Rdm.Dtos;

namespace IIASA.WorldCereal.Rdm.Jobs;

public static class CodeMapHelper
{
    public static CodeMap[] GetMap(string fileName)
    {
        CodeMap[] items;

        using (var reader = new StreamReader(fileName))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            var records = csv.GetRecords<CodeMap>();
            items = records.ToArray();
        }

        return items;
    }
    
    public static async Task WriteMaps(string fileName, CodeMapWithAttribute[] maps)
    {
        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine($"Label,Code");
        
        foreach (var item in maps)
        {
            csvBuilder.AppendLine($"{item.Label},{item.Code}");
        }

        await File.WriteAllTextAsync(fileName, csvBuilder.ToString());
    }

    public static string[] GetUnmappedCodes(string datasetFileNameWithExt, string userMappedFileNameWithExt)
    {
        var codeFromDataset = GetMap(datasetFileNameWithExt);
        var userCodeMaps = GetMap(userMappedFileNameWithExt);
        var unmappedLabels = new List<string>();
        foreach (var codeMap in codeFromDataset)
        {
            if (userCodeMaps.Any(x => x.Label == codeMap.Label) == false)
            {
                unmappedLabels.Add(codeMap.Label);
            }
        }

        return unmappedLabels.ToArray();
    }
}