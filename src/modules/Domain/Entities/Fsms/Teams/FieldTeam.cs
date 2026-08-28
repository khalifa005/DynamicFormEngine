namespace KH.Domain.Entities.Fsms.Teams;

/// <summary>
/// Local mirror of WFM <c>FIELDTEAMSINFOAVL_GVT</c>. Populated locally until task-09 wires the
/// live WFM API. Table: <c>TEAMS</c>.
/// </summary>
public sealed class FieldTeam : BaseAuditableEntity<long>
{
    public const int DeviceNameMaxLength = 200;
    public const int DeviceUuidMaxLength = 128;
    public const int AppVersionMaxLength = 50;
    public const int DeviceOsMaxLength = 50;

    public string UserCode { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Mobile { get; set; }
    public string? TeamType { get; set; }

    /// <summary>
    /// Optional WFM contractor / blanket PO this crew works under.
    /// </summary>
    public int? ContractorId { get; set; }

    /// <summary>
    /// Retired comma-separated department ids (the WFM shape), superseded by
    /// <see cref="FieldTeamDepartment"/> rows. Nothing reads it any more; the column survives one
    /// release so the old values stay recoverable if the join rows have to be rebuilt.
    /// </summary>
    public string? Departments { get; set; }

    /// <summary>Phone / tablet name from the last mobile login or last-active ping.</summary>
    public string? DeviceName { get; private set; }

    /// <summary>Stable device id (<c>uuid</c> on the mobile body) from the last login or ping.</summary>
    public string? DeviceUuid { get; private set; }

    /// <summary>Mobile app version from the last login or last-active ping.</summary>
    public string? AppVersion { get; private set; }

    /// <summary>Device OS from the last login or last-active ping.</summary>
    public string? DeviceOs { get; private set; }

    public double? LastActiveLatitude { get; private set; }

    public double? LastActiveLongitude { get; private set; }

    /// <summary>When the crew last signed in or sent a last-active ping.</summary>
    public DateTimeOffset? LastActiveAt { get; private set; }

    /// <summary>
    /// Overwrites the last-known device snapshot from a mobile login or hourly ping. Empty device
    /// fields leave the previous device as-is. Location is replaced only when both coordinates are
    /// present and in range. <see cref="LastActiveAt"/> is always stamped.
    /// </summary>
    public void RecordDeviceSession(
        string? deviceName,
        string? uuid,
        string? version,
        string? os,
        double? lastActiveLatitude,
        double? lastActiveLongitude,
        DateTimeOffset lastActiveAt)
    {
        var name = Normalize(deviceName);
        var deviceUuid = Normalize(uuid);
        var appVersion = Normalize(version);
        var deviceOs = Normalize(os);

        if (name is not null || deviceUuid is not null || appVersion is not null || deviceOs is not null)
        {
            DeviceName = name;
            DeviceUuid = deviceUuid;
            AppVersion = appVersion;
            DeviceOs = deviceOs;
        }

        if (IsValidCoordinate(lastActiveLatitude, lastActiveLongitude))
        {
            LastActiveLatitude = lastActiveLatitude;
            LastActiveLongitude = lastActiveLongitude;
        }

        LastActiveAt = lastActiveAt;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsValidCoordinate(double? latitude, double? longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
}
