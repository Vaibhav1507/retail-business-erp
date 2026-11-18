using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Rbos.Api.Persistence;

/// <summary>ASP.NET Core Identity store only. Business data is accessed through tenant-scoped SQL, not this context.</summary>
public sealed class RbosIdentityDbContext(DbContextOptions<RbosIdentityDbContext> options)
    : IdentityDbContext<
        IdentityUser<Guid>,
        IdentityRole<Guid>,
        Guid,
        IdentityUserClaim<Guid>,
        IdentityUserRole<Guid>,
        IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>,
        IdentityUserToken<Guid>,
        IdentityUserPasskey<Guid>>(options)
{
    public const string Schema = "identity";

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);
        base.OnModelCreating(builder);
    }
}
