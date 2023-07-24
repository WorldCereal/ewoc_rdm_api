using System;
using System.IO;
using System.Text.Json;
using IIASA.Db.UpdateTools.core;
using IIASA.Db.UpdateTools.jobs;
using IIASA.WorldCereal.Rdm.Core;

namespace IIASA.Db.UpdateTools
{
    class Program
    {
        static void Main(string[] args)
        {
            var logger = new Logger();
            logger.Log($"WorldCereal Database(DB) operations tool.");
            logger.Log($"Select operation to perform and press enter.");
            logger.Log($"1. Upload new collection to DB from shape file.");
            logger.Log($"2. Generate samples from database.json(From VITO)");
            logger.Log($"3. Upload version 1.0 samples to DB");
            logger.Log($"Press any other key to exit!");
            var choiceString = Console.ReadLine();
            if (int.TryParse(choiceString, out int choice) == false)
            {
                Exit();
            }

            var config = LoadConfig(logger);

            switch (choice)
            {
                case 1:
                    logger.Log($"Enter shape file path");
                    var filePath = Console.ReadLine();
                    logger.Log($"Enter collection ID");
                    config.CollectionId = Console.ReadLine();
                    new CollectionDbJobs().Upload(filePath, config);
                    break;
                case 2:
                    logger.Log("Enter database.json file path.");
                    new SampleJobs().GenerateSamples(Console.ReadLine());
                    break;
                case 3:
                    logger.Log("Enter patchSamples.geojson file path.");
                    var geoJsonPath = Console.ReadLine();
                    logger.Log("Enter sample version.");
                    var sampleVersion = double.Parse(Console.ReadLine() ?? "1.0");
                    new SampleJobs().UploadSamplesToDb(geoJsonPath, config, sampleVersion);
                    break;
                default:
                    Exit();
                    break;
            }
        }

        private static Configuration LoadConfig(Logger logger)
        {
            logger.Log($"Reading config from appsettings.json");
            var config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(".\\appsettings.json"));
            return config;
        }

        private static void Exit()
        {
            Console.WriteLine("Invalid Selection!. Press any key to exit.");
            Console.Read();
            Environment.Exit(-1);
        }
    }

    public class Logger
    {
        public void Log(string msg)
        {
            Console.WriteLine(msg);
        }
    }
}
