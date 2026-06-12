namespace Artskart3.Core.Constants
{
    public static class SearchConstants
    {
        // Taxon search constants
        public const int DefaultMaxTaxonCount = 20;
        public const int MaxTaxonCount = 1000;
        public const int MinTaxonResults = 1;

        // Location search constants
        public const int DefaultMaxLocations = 1000;
        public const int MaxLocationResults = 1000;
        public const int MinLocationResults = 1;

        // Coordinate constants
        public const int DefaultEpsgCode = 25833;

        // Error messages
        public const string CoordinatePrecisionInvalidMessage = "CoordinatePrecisionFrom must be less than or equal to CoordinatePrecisionTo.";
        public const string ServiceUnavailableMessage = "An error occurred while processing your request. Please try again later.";
        public const string UnexpectedErrorMessage = "An unexpected error occurred while processing your request. Please try again later.";
    }
}
