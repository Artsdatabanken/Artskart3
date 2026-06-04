using System;

namespace Artskart3.Core.Application.DTOs
{
    public class LocationSearchFilterDto
    {
        public int[]? TaxonGroupIds { get; set; }

        public int[]? Categories { get; set; }

        public int[]? BasisOfRecords { get; set; }

        /// <summary>
        /// Array of CollectionIds (InstitutionCodes) to filter by
        /// </summary>
        public string[]? CollectionIds { get; set; }

        /// <summary>
        /// Minimum coordinate precision in meters (0 = no filter)
        /// </summary>
        public int CoordinatePrecisionFrom { get; set; } = 0;

        /// <summary>
        /// Maximum coordinate precision in meters (0 = no filter)
        /// </summary>
        public int CoordinatePrecisionTo { get; set; } = 0;

        /// <summary>
        /// EPSG code for coordinate system (default: 25833)
        /// </summary>
        public int? Epsg { get; set; }

        /// <summary>
        /// Maximum number of locations to return (default: 1000, max: 10000)
        /// </summary>
        public int MaxResults { get; set; } = 1000000;
    }
}
