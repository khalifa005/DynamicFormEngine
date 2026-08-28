namespace Shared.Core.Common;

public static class ApiErrorHttpMapper
{
    private static readonly Dictionary<string, int> CodeToStatus = new(StringComparer.Ordinal)
    {
        [ApiErrorCodes.ValidationError] = 400,
        [ApiErrorCodes.AuthenticationFailed] = 401,
        [ApiErrorCodes.InvalidRefreshToken] = 401,
        [ApiErrorCodes.UnauthorizedAccess] = 403,
        [ApiErrorCodes.NotFound] = 404,
        [ApiErrorCodes.ApiMethodNotFound] = 404,
        [ApiErrorCodes.UnsupportedApiMetadata] = 400,
        [ApiErrorCodes.InvalidApiRequestFormat] = 400,
        [ApiErrorCodes.InternalServiceError] = 500,

        [ApiErrorCodes.DuplicateRequestOpenComplaint] = 409,
        [ApiErrorCodes.InvalidCoordinatesOutOfMashaar] = 400,
        [ApiErrorCodes.InternalServiceErrorDownstream] = 502,
        [ApiErrorCodes.NoDataFound] = 404,
        [ApiErrorCodes.InvalidDateRangeForInquiry] = 400,
        [ApiErrorCodes.InvalidSubCategoryCodeForInquiry] = 400,
        [ApiErrorCodes.UnableToDownloadAttachmentUri] = 502,
        [ApiErrorCodes.InvalidFileContentType] = 400,
        [ApiErrorCodes.BackendBusinessRuleValidation] = 422,
        [ApiErrorCodes.ServiceUnavailable] = 503,

        [ApiErrorCodes.TwoWaySessionNotAllowed] = 400,
        [ApiErrorCodes.ActiveSessionAlreadyExists] = 400,
        [ApiErrorCodes.MaximumActiveSessionsReached] = 400,
        [ApiErrorCodes.SessionExpired] = 400,
        [ApiErrorCodes.InvalidSessionOwnership] = 400,
        [ApiErrorCodes.InvalidSessionStatusTransition] = 400,

        ["DUPLICATE_REQUEST"] = 409,
        ["VALIDATION_ERROR"] = 400,
        ["INVALID_COORDINATES"] = 400,
        ["UNAUTHORIZED"] = 401,
    };

    public static int GetHttpStatusCode(string? apiErrorCode)
    {
        if (string.IsNullOrEmpty(apiErrorCode))
        {
            return 400;
        }

        return CodeToStatus.TryGetValue(apiErrorCode, out var status) ? status : 400;
    }
}
