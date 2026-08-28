using System.Data;
using System.Data.Common;
using System.Globalization;
using KH.Application.Fsms.Reporting.Interfaces;
using KH.Application.Fsms.Reporting.Models;
using KH.Domain.Constants.Fsms;
using KH.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KH.Infrastructure.Services.Fsms.Reporting;

/// <summary>
/// Executes <c>usp_Fsms_Dashboard_Get</c> and walks its ten result sets in order. The order is the
/// contract between this reader and the procedure, so each block is labelled with its set number;
/// inserting a result set in the SQL means inserting a <c>NextResultAsync</c> here to match.
/// </summary>
public sealed class DashboardStore(ApplicationDbContext context) : IDashboardStore
{
    public async Task<DashboardDto> GetAsync(DashboardStoreRequest request, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;

        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = DashboardSql.ProcedureName;
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, DashboardSql.Parameters.FromDate, request.FromDate);
            AddParameter(command, DashboardSql.Parameters.ToDate, request.ToDate);
            AddParameter(command, DashboardSql.Parameters.ClusterCode, request.ClusterCode);
            AddParameter(command, DashboardSql.Parameters.CbuCode, request.CbuCode);
            AddParameter(command, DashboardSql.Parameters.BranchCode, request.BranchCode);
            AddParameter(command, DashboardSql.Parameters.OperationAreaCode, request.OperationAreaCode);
            AddParameter(command, DashboardSql.Parameters.DepartmentId, request.DepartmentId);
            AddParameter(command, DashboardSql.Parameters.TemplateId, request.TemplateId);
            AddParameter(command, DashboardSql.Parameters.FaTypeCode, request.FaTypeCode);
            AddParameter(command, DashboardSql.Parameters.Status, request.Status);
            AddParameter(command, DashboardSql.Parameters.Source, request.Source);
            AddParameter(command, DashboardSql.Parameters.UserId, request.UserId);
            AddParameter(command, DashboardSql.Parameters.PeerScope, request.PeerScope);
            AddParameter(command, DashboardSql.Parameters.RoleName, request.RoleName);
            AddParameter(command, DashboardSql.Parameters.TrendGrain, request.TrendGrain);
            AddParameter(command, DashboardSql.Parameters.TopN, request.TopN);
            AddParameter(command, DashboardSql.Parameters.IsUnrestricted, request.IsUnrestricted);
            AddParameter(command, DashboardSql.Parameters.ScopeGroupsJson, request.ScopeGroupsJson);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // 1 — KPIs
            var kpis = await reader.ReadAsync(cancellationToken) ? ReadKpis(reader) : new DashboardKpisDto();

            // 2 — Trend
            var trend = await ReadNextAsync(reader, ReadTrendPoint, cancellationToken);

            // 3 — Status distribution
            var statuses = await ReadNextAsync(reader, ReadStatusSlice, cancellationToken);

            // 4 — Teams per CBU / Branch / Operation Area
            var teamsByOrg = await ReadNextAsync(reader, ReadOrgTeamStats, cancellationToken);

            // 5 — Teams per department
            var teamsByDepartment = await ReadNextAsync(reader, ReadDepartmentTeamStats, cancellationToken);

            // 6 — The caller's own statistics (always exactly one row)
            await reader.NextResultAsync(cancellationToken);
            var myStats = await reader.ReadAsync(cancellationToken) ? ReadMyStats(reader) : new DashboardMyStatsDto();

            // 7 — Peer comparison
            var peers = await ReadNextAsync(reader, ReadPeerStat, cancellationToken);

            // 8 — Transaction feed
            var transactions = await ReadNextAsync(reader, ReadTransaction, cancellationToken);

            // 9 — Recent surveys
            var recentSurveys = await ReadNextAsync(reader, ReadRecentSurvey, cancellationToken);

            // 10 — Breakdowns
            var breakdowns = await ReadNextAsync(reader, ReadBreakdown, cancellationToken);

            // 11 — Late surveys
            var lateSurveys = await ReadNextAsync(reader, ReadLateSurvey, cancellationToken);

            // 12 — Late teams
            var lateTeams = await ReadNextAsync(reader, ReadLateTeam, cancellationToken);

            return new DashboardDto
            {
                Kpis = kpis,
                Trend = trend,
                StatusDistribution = statuses,
                TeamsByOrg = teamsByOrg,
                TeamsByDepartment = teamsByDepartment,
                MyStats = myStats,
                PeerComparison = peers,
                Transactions = transactions,
                RecentSurveys = recentSurveys,
                Breakdowns = breakdowns,
                LateSurveys = lateSurveys,
                LateTeams = lateTeams,
            };
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<IReadOnlyList<T>> ReadNextAsync<T>(
        DbDataReader reader,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken)
    {
        var items = new List<T>();

        if (!await reader.NextResultAsync(cancellationToken))
        {
            return items;
        }

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(map(reader));
        }

        return items;
    }

    private static DashboardKpisDto ReadKpis(DbDataReader r) => new()
    {
        FromDate = GetDateTimeOffset(r, "FromDate") ?? default,
        ToDate = GetDateTimeOffset(r, "ToDate") ?? default,
        TotalSurveys = GetInt(r, "TotalSurveys"),
        ApprovedSurveys = GetInt(r, "ApprovedSurveys"),
        ReturnedSurveys = GetInt(r, "ReturnedSurveys"),
        InProgressSurveys = GetInt(r, "InProgressSurveys"),
        SubmittedSurveys = GetInt(r, "SubmittedSurveys"),
        OverdueSurveys = GetInt(r, "OverdueSurveys"),
        ActiveTeams = GetInt(r, "ActiveTeams"),
        AvgCompletionHours = GetDecimal(r, "AvgCompletionHours"),
        CompletionRatePercent = GetDecimal(r, "CompletionRatePercent"),
        ReturnRatePercent = GetDecimal(r, "ReturnRatePercent"),
        OnTimeRatePercent = GetDecimal(r, "OnTimeRatePercent"),
        TotalSurveysDelta = GetDecimal(r, "TotalSurveysDelta"),
        ApprovedSurveysDelta = GetDecimal(r, "ApprovedSurveysDelta"),
        SubmittedSurveysDelta = GetDecimal(r, "SubmittedSurveysDelta"),
        ReturnedSurveysDelta = GetDecimal(r, "ReturnedSurveysDelta"),
        InProgressSurveysDelta = GetDecimal(r, "InProgressSurveysDelta"),
        OverdueSurveysDelta = GetDecimal(r, "OverdueSurveysDelta"),
        ActiveTeamsDelta = GetDecimal(r, "ActiveTeamsDelta"),
        CompletionRateDelta = GetDecimal(r, "CompletionRateDelta"),
        ReturnRateDelta = GetDecimal(r, "ReturnRateDelta"),
        OnTimeRateDelta = GetDecimal(r, "OnTimeRateDelta"),
    };

    private static DashboardTrendPointDto ReadTrendPoint(DbDataReader r) => new()
    {
        Bucket = GetDateTime(r, "Bucket") ?? default,
        BucketKey = GetString(r, "BucketKey") ?? string.Empty,
        CreatedCount = GetInt(r, "CreatedCount"),
        AssignedCount = GetInt(r, "AssignedCount"),
        SubmittedCount = GetInt(r, "SubmittedCount"),
        ApprovedCount = GetInt(r, "ApprovedCount"),
        ReturnedCount = GetInt(r, "ReturnedCount"),
    };

    private static DashboardStatusSliceDto ReadStatusSlice(DbDataReader r) => new()
    {
        Status = GetString(r, "Status") ?? string.Empty,
        Count = GetInt(r, "Count"),
        Percent = GetDecimal(r, "Percent"),
        SortOrder = GetInt(r, "SortOrder"),
    };

    private static DashboardOrgTeamStatsDto ReadOrgTeamStats(DbDataReader r) => new()
    {
        Level = GetString(r, "Level") ?? string.Empty,
        Code = GetString(r, "Code") ?? string.Empty,
        NameEn = GetString(r, "NameEn"),
        NameAr = GetString(r, "NameAr"),
        ParentCode = GetString(r, "ParentCode"),
        TeamCount = GetInt(r, "TeamCount"),
        WorkingTeamCount = GetInt(r, "WorkingTeamCount"),
        MemberCount = GetInt(r, "MemberCount"),
        SurveyCount = GetInt(r, "SurveyCount"),
        OpenSurveyCount = GetInt(r, "OpenSurveyCount"),
        ApprovedSurveyCount = GetInt(r, "ApprovedSurveyCount"),
        SurveysPerTeam = GetDecimal(r, "SurveysPerTeam"),
        AvgCompletionHours = GetDecimal(r, "AvgCompletionHours"),
    };

    private static DashboardDepartmentTeamStatsDto ReadDepartmentTeamStats(DbDataReader r) => new()
    {
        DepartmentId = GetInt(r, "DepartmentId"),
        NameEn = GetString(r, "NameEn"),
        NameAr = GetString(r, "NameAr"),
        TeamCount = GetInt(r, "TeamCount"),
        SurveyCount = GetInt(r, "SurveyCount"),
        OpenSurveyCount = GetInt(r, "OpenSurveyCount"),
        ApprovedSurveyCount = GetInt(r, "ApprovedSurveyCount"),
        SurveysPerTeam = GetDecimal(r, "SurveysPerTeam"),
    };

    private static DashboardMyStatsDto ReadMyStats(DbDataReader r) => new()
    {
        UserId = GetString(r, "UserId"),
        DisplayName = GetString(r, "DisplayName"),
        RoleName = GetString(r, "RoleName"),
        TransactionCount = GetInt(r, "TransactionCount"),
        SurveysTouched = GetInt(r, "SurveysTouched"),
        AssignedCount = GetInt(r, "AssignedCount"),
        SubmittedCount = GetInt(r, "SubmittedCount"),
        ApprovedCount = GetInt(r, "ApprovedCount"),
        ReturnedCount = GetInt(r, "ReturnedCount"),
        AvgHandlingHours = GetDecimal(r, "AvgHandlingHours"),
        LastActivityDate = GetDateTimeOffset(r, "LastActivityDate"),
        CohortSize = GetInt(r, "CohortSize"),
        PeerScope = GetString(r, "PeerScope"),
    };

    private static DashboardPeerStatDto ReadPeerStat(DbDataReader r) => new()
    {
        UserId = GetString(r, "UserId") ?? string.Empty,
        DisplayName = GetString(r, "DisplayName"),
        RoleName = GetString(r, "RoleName"),
        IsCurrentUser = GetBool(r, "IsCurrentUser"),
        TransactionCount = GetInt(r, "TransactionCount"),
        SurveysTouched = GetInt(r, "SurveysTouched"),
        ApprovedCount = GetInt(r, "ApprovedCount"),
        ReturnedCount = GetInt(r, "ReturnedCount"),
        AvgHandlingHours = GetDecimal(r, "AvgHandlingHours"),
        LastActivityDate = GetDateTimeOffset(r, "LastActivityDate"),
        Rank = GetInt(r, "Rank"),
        PercentileRank = GetDecimal(r, "PercentileRank"),
        CohortAvg = GetDecimal(r, "CohortAvg"),
        CohortMedian = GetDecimal(r, "CohortMedian"),
        CohortMax = GetInt(r, "CohortMax"),
        CohortSize = GetInt(r, "CohortSize"),
    };

    private static DashboardTransactionDto ReadTransaction(DbDataReader r) => new()
    {
        HistoryId = GetLong(r, "HistoryId"),
        SurveyId = GetLong(r, "SurveyId"),
        SurveyCode = GetString(r, "SurveyCode") ?? string.Empty,
        UserId = GetString(r, "UserId"),
        DisplayName = GetString(r, "DisplayName"),
        RoleName = GetString(r, "RoleName"),
        FromStatus = GetString(r, "FromStatus"),
        ToStatus = GetString(r, "ToStatus") ?? string.Empty,
        ChangedDate = GetDateTimeOffset(r, "ChangedDate") ?? default,
        Note = GetString(r, "Note"),
        CbuCode = GetString(r, "CbuCode"),
        BranchCode = GetString(r, "BranchCode"),
        OperationAreaCode = GetString(r, "OperationAreaCode"),
        FieldTeamName = GetString(r, "FieldTeamName"),
        IsCurrentUser = GetBool(r, "IsCurrentUser"),
    };

    private static DashboardRecentSurveyDto ReadRecentSurvey(DbDataReader r) => new()
    {
        Id = GetLong(r, "Id"),
        SurveyCode = GetString(r, "SurveyCode") ?? string.Empty,
        TemplateId = GetLong(r, "TemplateId"),
        TemplateNameEn = GetString(r, "TemplateNameEn"),
        TemplateNameAr = GetString(r, "TemplateNameAr"),
        BranchCode = GetString(r, "BranchCode"),
        BranchNameEn = GetString(r, "BranchNameEn"),
        BranchNameAr = GetString(r, "BranchNameAr"),
        CbuCode = GetString(r, "CbuCode"),
        FieldTeamId = GetNullableLong(r, "FieldTeamId"),
        FieldTeamName = GetString(r, "FieldTeamName"),
        Source = GetString(r, "Source") ?? string.Empty,
        Status = GetString(r, "Status") ?? string.Empty,
        Created = GetDateTimeOffset(r, "Created") ?? default,
        DueDate = GetDateTimeOffset(r, "DueDate"),
        IsOverdue = GetBool(r, "IsOverdue"),
    };

    private static DashboardBreakdownDto ReadBreakdown(DbDataReader r) => new()
    {
        Dimension = GetString(r, "Dimension") ?? string.Empty,
        Code = GetString(r, "Code"),
        NameEn = GetString(r, "NameEn"),
        NameAr = GetString(r, "NameAr"),
        Count = GetInt(r, "Count"),
        Percent = GetDecimal(r, "Percent"),
    };

    private static DashboardLateSurveyDto ReadLateSurvey(DbDataReader r) => new()
    {
        Id = GetLong(r, "Id"),
        SurveyCode = GetString(r, "SurveyCode") ?? string.Empty,
        Status = GetString(r, "Status") ?? string.Empty,
        LatenessKind = GetString(r, "LatenessKind") ?? string.Empty,
        DeadlineKind = GetString(r, "DeadlineKind") ?? string.Empty,
        DueDate = GetDateTimeOffset(r, "DueDate"),
        CompletedDate = GetDateTimeOffset(r, "CompletedDate"),
        DaysLate = GetInt(r, "DaysLate"),
        FieldTeamId = GetNullableLong(r, "FieldTeamId"),
        FieldTeamName = GetString(r, "FieldTeamName"),
        CbuCode = GetString(r, "CbuCode"),
        BranchCode = GetString(r, "BranchCode"),
        BranchNameEn = GetString(r, "BranchNameEn"),
        BranchNameAr = GetString(r, "BranchNameAr"),
        TemplateNameEn = GetString(r, "TemplateNameEn"),
        TemplateNameAr = GetString(r, "TemplateNameAr"),
        Created = GetDateTimeOffset(r, "Created") ?? default,
    };

    private static DashboardLateTeamDto ReadLateTeam(DbDataReader r) => new()
    {
        FieldTeamId = GetLong(r, "FieldTeamId"),
        FieldTeamName = GetString(r, "FieldTeamName"),
        UserCode = GetString(r, "UserCode"),
        SurveyCount = GetInt(r, "SurveyCount"),
        OverdueCount = GetInt(r, "OverdueCount"),
        CompletedLateCount = GetInt(r, "CompletedLateCount"),
        OnTimeCount = GetInt(r, "OnTimeCount"),
        AvgDaysLate = GetDecimal(r, "AvgDaysLate"),
        MaxDaysOverdue = GetInt(r, "MaxDaysOverdue"),
        OnTimeRatePercent = GetDecimal(r, "OnTimeRatePercent"),
    };

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    // Column accessors are name-based and null-tolerant so that adding a column to the procedure
    // never breaks the reader — the ordinal lookup simply misses and the property keeps its default.
    private static int Ordinal(DbDataReader reader, string name)
    {
        try
        {
            return reader.GetOrdinal(name);
        }
        catch (IndexOutOfRangeException)
        {
            return -1;
        }
    }

    private static bool TryGet(DbDataReader reader, string name, out int ordinal)
    {
        ordinal = Ordinal(reader, name);
        return ordinal >= 0 && !reader.IsDBNull(ordinal);
    }

    private static string? GetString(DbDataReader r, string name) =>
        TryGet(r, name, out var i) ? r.GetString(i) : null;

    private static int GetInt(DbDataReader r, string name) =>
        TryGet(r, name, out var i) ? Convert.ToInt32(r.GetValue(i), CultureInfo.InvariantCulture) : 0;

    private static long GetLong(DbDataReader r, string name) =>
        TryGet(r, name, out var i) ? Convert.ToInt64(r.GetValue(i), CultureInfo.InvariantCulture) : 0L;

    private static long? GetNullableLong(DbDataReader r, string name) =>
        TryGet(r, name, out var i) ? Convert.ToInt64(r.GetValue(i), CultureInfo.InvariantCulture) : null;

    private static decimal GetDecimal(DbDataReader r, string name) =>
        TryGet(r, name, out var i) ? Convert.ToDecimal(r.GetValue(i), CultureInfo.InvariantCulture) : 0m;

    private static bool GetBool(DbDataReader r, string name) =>
        TryGet(r, name, out var i) && Convert.ToBoolean(r.GetValue(i), CultureInfo.InvariantCulture);

    private static DateTime? GetDateTime(DbDataReader r, string name) =>
        TryGet(r, name, out var i) ? Convert.ToDateTime(r.GetValue(i), CultureInfo.InvariantCulture) : null;

    private static DateTimeOffset? GetDateTimeOffset(DbDataReader r, string name)
    {
        if (!TryGet(r, name, out var i))
        {
            return null;
        }

        // datetimeoffset comes back as DateTimeOffset, but the trend/calendar columns are plain
        // dates. Treating a bare DateTime as UTC keeps the client from shifting it by the offset.
        return r.GetValue(i) switch
        {
            DateTimeOffset offset => offset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            var other => new DateTimeOffset(
                DateTime.SpecifyKind(Convert.ToDateTime(other, CultureInfo.InvariantCulture), DateTimeKind.Utc)),
        };
    }
}
