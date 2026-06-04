namespace Artskart3.Api.Constants
{    
    public static class SearchConstants
    {
        public const int DefaultMaxTaxonCount = 20;
        public const int MaxTaxonCount = 1000;
        public const int MinTaxonResults = 1;
        public const int DefaultMaxLocations = 1000;
        public const int MaxLocationResults = 1000000;
        public const int MinLocationResults = 1;
        public const int DefaultEpsgCode = 25833;     
        public const string CoordinatePrecisionInvalidMessage = "CoordinatePrecisionFrom must be less than or equal to CoordinatePrecisionTo.";
        public const string ServiceUnavailableMessage = "An error occurred while processing your request. Please try again later.";
        public const string UnexpectedErrorMessage = "An unexpected error occurred while processing your request. Please try again later.";
    }
}
