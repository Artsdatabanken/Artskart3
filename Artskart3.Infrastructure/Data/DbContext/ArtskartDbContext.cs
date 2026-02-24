namespace Artskart3.Infrastructure.Data.DbContext
{
    using Artskart3.Core.Domain.Entities;
    using Artskart3.Infrastructure.Persistence.Repositories;
    using Microsoft.EntityFrameworkCore;

    public class ArtskartDbContext : DbContext, IArtsKartDbContext
    {
        public ArtskartDbContext(DbContextOptions<ArtskartDbContext> options)
            : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
