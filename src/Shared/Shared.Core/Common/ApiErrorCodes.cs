namespace Shared.Core.Common;

public static class ApiErrorCodes
{
    public const string ValidationError = "E10001002";
    public const string AuthenticationFailed = "E10002001";
    public const string InvalidRefreshToken = "E10002003";
    public const string UnauthorizedAccess = "E10002002";
    public const string NotFound = "E10004000";
    public const string ApiMethodNotFound = "E10004001";
    public const string UnsupportedApiMetadata = "E10004002";
    public const string InvalidApiRequestFormat = "E10004003";
    public const string InternalServiceError = "E10005001";

    public const string DuplicateRequestOpenComplaint = "E14074002";
    public const string InvalidCoordinatesOutOfMashaar = "E14074003";
    public const string InternalServiceErrorDownstream = "E14075000";
    public const string NoDataFound = "E14074004";
    public const string InvalidDateRangeForInquiry = "E14074005";
    public const string InvalidSubCategoryCodeForInquiry = "E14074006";
    public const string UnableToDownloadAttachmentUri = "E14074007";
    public const string InvalidFileContentType = "E14074008";
    public const string BackendBusinessRuleValidation = "E14076000";
    public const string ServiceUnavailable = "E14075001";

    public const string TwoWaySessionNotAllowed = "E4007";
    public const string ActiveSessionAlreadyExists = "E4008";
    public const string MaximumActiveSessionsReached = "E4009";
    public const string SessionExpired = "E4010";
    public const string InvalidSessionOwnership = "E4011";
    public const string InvalidSessionStatusTransition = "E4012";
    public const string SessionConsumptionExhausted = "E4013";
}
