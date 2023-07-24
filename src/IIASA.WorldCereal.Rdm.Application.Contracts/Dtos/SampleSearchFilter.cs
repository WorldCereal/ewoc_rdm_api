using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Volo.Abp.Application.Dtos;

namespace IIASA.WorldCereal.Rdm.Dtos
{
    public class SampleSearchFilter : PagedResultRequestDto
    {
        /// <summary>
        /// Only features that have a geometry that intersects the bounding box are selected. The bounding box is provided as four decimals:  * Lower left corner, coordinate axis 1 * Lower left corner, coordinate axis 2 * Upper right corner, coordinate axis 1 * Upper right corner, coordinate axis 2  The coordinate reference system of the values is always WGS 84 longitude/latitude (http://www.opengis.net/def/crs/OGC/1.3/CRS84). The values are sequence of minimum longitude, minimum latitude, maximum longitude and maximum latitude.
        /// </summary>
        [Required]
        public ICollection<double?> Bbox { get; set; }

        /// <summary>
        /// Version of the Patch Sample
        /// </summary>
        public double? Version { get; set; } = 1.0;

        /// <summary>
        /// Optional Validity Range StartTime eg - 2019-06-30T00:00:00Z (yyyy-MM-ddTHH:mm:ssZ)
        /// </summary>
        public DateTime? ValidityStartTime { get; set; }
        /// <summary>
        /// Optional Validity Range EndTime eg - 2019-12-31T00:00:00Z (yyyy-MM-ddTHH:mm:ssZ)
        /// </summary>
        public DateTime? ValidityEndTime { get; set; }

        /// <summary>
        /// CAL, VAL, TEST samples
        /// </summary>
        public string Split { get; set; }

        public IEnumerable<CoordinateDto> GetBoundingBoxPoints()
        {
            var bboxPoints = Bbox.ToList();

            var xmin = bboxPoints[0].Value;
            var ymin = bboxPoints[1].Value;
            var xmax = bboxPoints[2].Value;
            var ymax = bboxPoints[3].Value;

            return new List<CoordinateDto>
            {
                new CoordinateDto {Latitude = xmin, Longitude = ymin},
                new CoordinateDto {Latitude = xmax, Longitude = ymin},
                new CoordinateDto {Latitude = xmax, Longitude = ymax},
                new CoordinateDto {Latitude = xmin, Longitude = ymax},
                new CoordinateDto {Latitude = xmin, Longitude = ymin}
            };
        }
    }
}