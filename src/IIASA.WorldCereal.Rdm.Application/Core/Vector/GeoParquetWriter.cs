using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Core.Vector.GeoParquet;
using IIASA.WorldCereal.Rdm.Entity;
using NetTopologySuite.Geometries;
using Parquet;
using Parquet.Rows;
using Parquet.Schema;
using DateTime = System.DateTime;

namespace IIASA.WorldCereal.Rdm.Core.Vector;

public class GeoParquetWriter
{
    public static async Task<string> WriteDataset(IList<ItemEntity> itemEntities, string tempPath, string file,
        Coordinate[] bboxCoordinates, bool ignoreOptional)
    {
        if (itemEntities.Any() == false)
        {
            return string.Empty;
        }

        Directory.CreateDirectory(tempPath);
        var fileName = Path.Combine(tempPath, file + ".parquet");

        var geometadata = GetGeoMetadata(itemEntities, bboxCoordinates);

        var schema = GetSchema(ignoreOptional);

        using (var ms = File.OpenWrite(fileName))
        {
            using (ParquetWriter writer = await ParquetWriter.CreateAsync(schema, ms))
            {
                writer.CustomMetadata = geometadata;
                writer.CompressionMethod = CompressionMethod.Gzip;
                writer.CompressionLevel = CompressionLevel.SmallestSize;

                var table = new Table(schema);
                foreach (var itemEntity in itemEntities)
                {
                    var rowCell = GetRowEntity(itemEntity, ignoreOptional);
                    table.Add(rowCell);
                }

                await writer.WriteAsync(table);
            }
        }

        return fileName;
    }

    private static Row GetRowEntity(ItemEntity itemEntity, bool ignoreOptional)
    {
        /*
         * sample_id (System.String)
        ewoc_code (System.Int64)
        valid_time (System.DateTime)
        irrigation_status (System.Int32)
        image_time (System.DateTime)
        extract (System.Int32)
        quality_score_lc (System.Int32)
        quality_score_ct (System.Int32)
        h3_l3_cell (System.String?)
        number_validations (System.Int32)
        type_validation (System.Int32)
        agreement (System.Int32)
        disagreement (System.Int32)
        geometry (System.Byte[])
         */
        if (ignoreOptional)
        {
            return new Row(
                itemEntity.SampleId,
                itemEntity.EwocCode,
                itemEntity.ValidityTime,
                itemEntity.IrrigationStatus,
                itemEntity.Geometry.ToBinary()
            );
        }

        return new Row(
            itemEntity.SampleId,
            itemEntity.EwocCode,
            itemEntity.ValidityTime,
            itemEntity.IrrigationStatus,
            itemEntity.ImageryDate.HasValue ? itemEntity.ImageryDate.Value : null,
            itemEntity.Extract,
            itemEntity.LcQualityScore,
            itemEntity.CtQualityScore,
            itemEntity.H3Index,
            itemEntity.NumberOfValidations ?? 0,
            itemEntity.TypeOfValidator.HasValue ? (int)itemEntity.TypeOfValidator : 0,
            itemEntity.AgreementOfObservations ?? 0,
            itemEntity.DisAgreementOfObservations ?? 0,
            itemEntity.Geometry.ToBinary()
        );
    }

    private static ParquetSchema GetSchema(bool ignoreOptional)
    {
        if (ignoreOptional)
        {
            return new ParquetSchema(
                new DataField<string>(AttributeNames.SampleId, false),
                new DataField<long>(AttributeNames.EwocCode, false),
                new DataField<DateTime>(AttributeNames.ValidityTime, false),
                new DataField<int>(AttributeNames.Irrigation),
                new DataField<byte[]>("geometry", false));
        }

        return new ParquetSchema(
            new DataField<string>(AttributeNames.SampleId, false),
            new DataField<long>(AttributeNames.EwocCode, false),
            new DataField<DateTime>(AttributeNames.ValidityTime, false),
            new DataField<int>(AttributeNames.Irrigation),
            new DataField<DateTime>(AttributeNames.ImageryTime, true),
            new DataField<int>(AttributeNames.Extract),
            new DataField<int>(AttributeNames.LandCoverQualityScore),
            new DataField<int>(AttributeNames.CropTypeQualityScore),
            new DataField<string>(AttributeNames.H3Index),
            new DataField<int>(AttributeNames.NumberOfValidations),
            new DataField<int>(AttributeNames.TypeOfValidator),
            new DataField<int>(AttributeNames.AgreementOfObservations),
            new DataField<int>(AttributeNames.DisagreementOfObservations),
            new DataField<byte[]>("geometry", false));
    }


    private static Dictionary<string, string> GetGeoMetadata(IList<ItemEntity> itemEntities,
        Coordinate[] bboxCoordinates)
    {
        var bbox = new[]
        {
            bboxCoordinates.Min(x => x.X),
            bboxCoordinates.Min(x => x.Y),
            bboxCoordinates.Max(x => x.X),
            bboxCoordinates.Max(x => x.Y)
        };

        var geoColumn = new GeoColumn
        {
            Edges = Edges.Planar,
            Bbox = bbox,
            Encoding = "WKB",
        };

        geoColumn.Geometry_types.Add(itemEntities.First().Geometry.GeometryType);
        var geometadata = GeoMetadata.GetGeoMetadata(geoColumn);
        return geometadata;
    }
}