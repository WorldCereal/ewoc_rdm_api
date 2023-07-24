using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Application.Dtos;

namespace IIASA.WorldCereal.Rdm.Dtos
{
    public class ItemsRequestFilter : PagedResultRequestDto
    {
        public ItemsRequestFilter()
        {
            MaxMaxResultCount = 10000;
            LandCoverTypes = new int[0];
            CropTypes = new int[0];
            IrrigationTypes = new int[0];
        }

        /// <summary>
        /// Offset count
        /// </summary>
        public override int SkipCount { get; set; }

        /// <summary>
        /// limits the count of items returned
        /// </summary>
        public override int MaxResultCount { get; set; } = DefaultMaxResultCount;

        /// <summary> 
        /// The optional ValidityStartTime eg - 2019-06-30T00:00:00Z (yyyy-MM-ddTHH:mm:ssZ)
        /// </summary>
        public DateTime? ValidityStartTime { get; set; }
        /// <summary>
        /// The optional ValidityEndTime eg - 2019-12-31T00:00:00Z (yyyy-MM-ddTHH:mm:ssZ)
        /// </summary>
        public DateTime? ValidityEndTime { get; set; }
        /// <summary>
        /// The optional LandCoverTypes to be selected
        /// </summary>
        public int[] LandCoverTypes { get; set; }
        /// <summary>
        /// The optional CropTypes to be selected
        /// </summary>
        public int[] CropTypes { get; set; }
        /// <summary>
        /// The optional IrrigationTypes to be selected
        /// </summary>
        public int[] IrrigationTypes { get; set; }
        
        /// <summary>
        /// CAL, VAL, TEST samples
        /// </summary>
        public string Split { get; set; }

        /// <summary>
        /// Only features that have a geometry that intersects the bounding box are selected. The bounding box is provided as four decimals:  * Lower left corner, coordinate axis 1 * Lower left corner, coordinate axis 2 * Upper right corner, coordinate axis 1 * Upper right corner, coordinate axis 2  The coordinate reference system of the values is always WGS 84 longitude/latitude (http://www.opengis.net/def/crs/OGC/1.3/CRS84). The values are sequence of minimum longitude, minimum latitude, maximum longitude and maximum latitude.
        /// </summary>
        public ICollection<double?> Bbox { get; set; }

        public IEnumerable<CoordinateDto> GetBoundingBoxPoints()
        {
            if(Bbox== null) return  new List<CoordinateDto>();

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
