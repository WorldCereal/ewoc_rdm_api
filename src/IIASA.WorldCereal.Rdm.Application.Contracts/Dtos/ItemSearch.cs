using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Volo.Abp.Application.Dtos;

namespace IIASA.WorldCereal.Rdm.Dtos
{
    public class ItemSearch : PagedResultRequestDto
    {
        public ItemSearch()
        {
            LandCoverTypes = new int[0];
            CropTypes = new int[0];
            IrrigationTypes = new int[0];
        }

        /// <summary>
        /// SkipCount count
        /// </summary>
        public override int SkipCount { get; set; }

        /// <summary>
        /// MaxResultCount the count of items returned
        /// </summary>
        public override int MaxResultCount { get; set; } = DefaultMaxResultCount;

        /// <summary>
        /// The optional ValidityStartTime for range eg - 2019-06-30T00:00:00Z (yyyy-MM-ddTHH:mm:ssZ)
        /// </summary>
        public TimeRange ValidityTime { get; set; }


        /// <summary>
        /// The optional LandCoverTypes to be selected
        /// </summary>
        public int[] LandCoverTypes { get; set; }

        /// <summary>
        /// The optional LandCoverConfidence to be selected
        /// </summary>
        public ValueRange LandCoverConfidence { get; set; }

        /// <summary>
        /// The optional CropTypes to be selected
        /// </summary>
        public int[] CropTypes { get; set; }

        /// <summary>
        /// The optional CropTypeConfidence to be selected
        /// </summary>
        public ValueRange CropTypeConfidence { get; set; }

        /// <summary>
        /// The optional IrrigationTypes to be selected
        /// </summary>
        public int[] IrrigationTypes { get; set; }

        /// <summary>
        /// The optional IrrigationConfidence to be selected
        /// </summary>
        public ValueRange IrrigationConfidence { get; set; }

        /// <summary>
        /// Only features that have a geometry that intersects the bounding box are selected. The bounding box is provided as four decimals:  * Lower left corner, coordinate axis 1 * Lower left corner, coordinate axis 2 * Upper right corner, coordinate axis 1 * Upper right corner, coordinate axis 2  The coordinate reference system of the values is always WGS 84 longitude/latitude (http://www.opengis.net/def/crs/OGC/1.3/CRS84). The values are sequence of minimum longitude, minimum latitude, maximum longitude and maximum latitude.
        /// </summary>
        [Required]
        public ICollection<double?> Bbox { get; set; }

        public IEnumerable<CoordinateDto> GetBoundingBoxPoints()
        {
            var bboxPoints = Bbox?.ToList();

            var xmin = bboxPoints[0].Value;
            var ymin = bboxPoints[1].Value;
            var xmax = bboxPoints[2].Value;
            var ymax = bboxPoints[3].Value;

            return new List<CoordinateDto>
            {
                new() {Latitude = xmin, Longitude = ymin},
                new() {Latitude = xmax, Longitude = ymin},
                new() {Latitude = xmax, Longitude = ymax},
                new() {Latitude = xmin, Longitude = ymax},
                new() {Latitude = xmin, Longitude = ymin}
            };
        }
    }

    /// <summary>
    /// Optional Range
    /// </summary>
    public class ValueRange
    {
        /// <summary>
        /// decimal Start value, Default=0
        /// </summary>
        public double Start { get; set; } = 0.0;

        /// <summary>
        /// decimal End value, Default=100
        /// </summary>
        public double End { get; set; } = 100.0;
    }
    
    /// <summary>
    /// Optional Range
    /// </summary>
    public class TimeRange
    {
        /// <summary>
        /// Optional StartTime eg - 2019-06-30T00:00:00Z (yyyy-MM-ddTHH:mm:ssZ)
        /// </summary>
        public DateTime? Start { get; set; }

        /// <summary>
        /// Optional EndTime eg - 2019-06-30T00:00:00Z (yyyy-MM-ddTHH:mm:ssZ)
        /// </summary>
        public DateTime? End { get; set; }
    }
}