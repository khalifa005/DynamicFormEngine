namespace KH.Application.Fsms.Common.Lookups;

/// <summary>
/// The bilingual display name of a reference-data row, keyed elsewhere by its code or id.
/// Deliberately its own type rather than a lookup slice's DTO: list slices label their rows from
/// this without taking a dependency on the lookup slices themselves.
/// </summary>
public sealed record LookupName(string NameEn, string NameAr);
