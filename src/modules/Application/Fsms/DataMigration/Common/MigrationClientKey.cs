using System.Security.Cryptography;
using System.Text;

namespace KH.Application.Fsms.DataMigration.Common;

/// <summary>
/// The client submission key an imported fill is written under.
///
/// Reusing the offline-sync key for this is deliberate. The <c>SUBMISSIONS</c> table already carries
/// a filtered unique index on it, built so a mobile app replaying a queue cannot write the same fill
/// twice — which is precisely the guarantee a re-runnable import needs. Deriving the key from the
/// source's own record id means the database enforces "import this record once" without a single
/// line of new schema.
/// </summary>
public static class MigrationClientKey
{
    /// <summary>
    /// A GUID for <paramref name="externalId"/>, stable across runs. A source whose key is already a
    /// GUID (Fulcrum's <c>fulcrum_id</c>) keeps it as it is, so the value in our column is the value
    /// in their export and the two can be matched by eye. Anything else is hashed down to a GUID,
    /// namespaced by source so two systems using the same record number cannot collide.
    /// </summary>
    public static Guid For(string sourceCode, string externalId)
    {
        if (Guid.TryParse(externalId, out var parsed))
        {
            return parsed;
        }

        var bytes = MD5.HashData(Encoding.UTF8.GetBytes($"{sourceCode}:{externalId}"));
        return new Guid(bytes);
    }
}
