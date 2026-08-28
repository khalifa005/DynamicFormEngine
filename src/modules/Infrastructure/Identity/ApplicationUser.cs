using Microsoft.AspNetCore.Identity;

namespace KH.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// The field team this login belongs to, when the account is a crew account rather than a
    /// back-office one. Set, it makes the token carry the team so the mobile app never has to be
    /// told which crew it is signed in as.
    /// </summary>
    public long? FieldTeamId { get; set; }
}
