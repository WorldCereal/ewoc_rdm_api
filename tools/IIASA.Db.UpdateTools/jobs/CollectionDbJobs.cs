using System.Collections.Generic;
using IIASA.Db.UpdateTools.core;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Entity;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql.Bulk;

namespace IIASA.Db.UpdateTools.jobs
{
    public class CollectionDbJobs
    {
        private readonly Logger _logger = new Logger();

        public void Upload(string filePath, Configuration config)
        {
            var context = new UpdaterContext(config, false);
            var shpReader = new ShapefileDataReader(filePath, new GeometryFactory());
            var total = shpReader.RecordCount;
            _logger.Log($" Shape file has total {total}  features.");

            var lcIndex = shpReader.GetOrdinal("LC");
            var ctIndex = shpReader.GetOrdinal("CT");
            var irrIndex = shpReader.GetOrdinal("IRR");
            var areaIndex = shpReader.GetOrdinal("area");
            var userConfIndex = shpReader.GetOrdinal("userConf");
            var sampleIdIndex = shpReader.GetOrdinal("sampleID");
            var splitIndex = shpReader.GetOrdinal("split");
            var valTimeIndex = shpReader.GetOrdinal("validityTi");

            var index = 0;
            var batchCount = 50000;
            var items = new List<ItemEntity>();
            NpgsqlBulkUploader uploader = new NpgsqlBulkUploader(context);
            while (shpReader.Read())
            {
                var entity = new ItemEntity
                {
                    CollectionId = config.CollectionId,
                    Area = shpReader.GetDouble(areaIndex),
                    Lc = shpReader.GetInt32(lcIndex),
                    Ct = shpReader.GetInt32(ctIndex),
                    Irr = shpReader.GetInt32(irrIndex),
                    SampleId = shpReader.GetString(sampleIdIndex),
                    ValidityTime = shpReader.GetDateTime(valTimeIndex),
                    UserConf = shpReader.GetInt32(userConfIndex),
                    Split = SplitHelper.Get(shpReader.GetString(splitIndex)),
                    Geometry = shpReader.Geometry
                };
                entity.Geometry.SRID = 4326;
                items.Add(entity);
                index++;

                if (index == batchCount)
                {
                    _logger.Log($"Uploading items with batch size- {batchCount}");
                    uploader.Import(items);
                    index = 0;
                    items = new List<ItemEntity>();
                }
            }

            _logger.Log($"Completed uploading all the items.");
        }
    }
}