namespace Shared.Core.Constants;

/// <summary>
/// Single registry for every cache key used by the application. Read sites and invalidation
/// sites must reference the same constant here so they can never drift apart — no inline strings.
/// </summary>
public static class CacheKeys
{
    public const string Separator = ":";
    public const string RootPrefix = "MK";

    public static class Fsms
    {
        public const string Prefix = RootPrefix + Separator + "FSMS";

        /// <summary>Reference data cached as a whole list, then filtered/paged in memory.</summary>
        public static class Lookups
        {
            public const string Prefix = Fsms.Prefix + Separator + "Lookups";

            public const string Departments = Prefix + Separator + "Departments";
            public const string FaTypes = Prefix + Separator + "FaTypes";
            public const string TaskTypes = Prefix + Separator + "TaskTypes";
            public const string CustomerTypes = Prefix + Separator + "CustomerTypes";
            public const string Contractors = Prefix + Separator + "Contractors";
            public const string ReturnReasons = Prefix + Separator + "ReturnReasons";

            /// <summary>The org geography, one cached list per level.</summary>
            public const string Clusters = Prefix + Separator + "Clusters";
            public const string Cbus = Prefix + Separator + "Cbus";
            public const string Branches = Prefix + Separator + "Branches";
            public const string OperationAreas = Prefix + Separator + "OperationAreas";

            /// <summary>
            /// The flattened parent chain of every org unit — code → its cluster / CBU / branch /
            /// operation area. Derived from all four lists above, so a write to any of them evicts
            /// it. This is what lets a scope assigned at one level be expanded to the codes beneath
            /// it without walking the tables per request.
            /// </summary>
            /// <remarks>
            /// Suffixed <c>v2</c> for the Zone → Branch rename: the cached value is JSON, and a
            /// payload written by the previous shape deserializes into the new one without error and
            /// with the wrong parents. A new key retires the old entries instead of trusting a TTL.
            /// Bump it again whenever <c>OrgHierarchyMaps</c> changes shape.
            /// </remarks>
            public const string OrgHierarchy = Prefix + Separator + "OrgHierarchy:v2";

            /// <summary>
            /// Slim code/id → bilingual name maps, held separately from the full lists above so list
            /// pages can label their rows without joining the lookup tables per row. Written and read
            /// only through the shared lookup name cache; evicted alongside their parent list.
            /// </summary>
            public const string BranchNames = Prefix + Separator + "BranchNames";
            public const string DepartmentNames = Prefix + Separator + "DepartmentNames";
            public const string CbuNames = Prefix + Separator + "CbuNames";
            public const string OperationAreaNames = Prefix + Separator + "OperationAreaNames";
            public const string ReturnReasonNames = Prefix + Separator + "ReturnReasonNames";
            public const string TaskTypeNames = Prefix + Separator + "TaskTypeNames";
            public const string CustomerTypeNames = Prefix + Separator + "CustomerTypeNames";

            /// <summary>Every lookup key — used when a bulk refresh (e.g. re-seed) invalidates all of them.</summary>
            public static IReadOnlyList<string> All { get; } =
            [
                Departments,
                FaTypes,
                TaskTypes,
                CustomerTypes,
                Contractors,
                ReturnReasons,
                Clusters,
                Cbus,
                Branches,
                OperationAreas,
                OrgHierarchy,
                BranchNames,
                DepartmentNames,
                CbuNames,
                OperationAreaNames,
                ReturnReasonNames,
                TaskTypeNames,
                CustomerTypeNames
            ];
        }

        /// <summary>
        /// The field-catalog dictionary (shared <c>data_name</c> definitions). Read by the
        /// form-builder autocomplete and the lookups admin grid, and grown by template publish,
        /// so it is cached as a whole list and filtered/paged in memory like the lookups above.
        /// </summary>
        public static class Catalog
        {
            public const string Prefix = Fsms.Prefix + Separator + "Catalog";

            public const string Fields = Prefix + Separator + "Fields";

            public static IReadOnlyList<string> All { get; } = [Fields];
        }
    }

    /// <summary>Builds a key for a single entity, e.g. <c>MK:FSMS:Lookups:Branches:12</c>.</summary>
    public static string ForId(string prefix, object id) => $"{prefix}{Separator}{id}";
}
