using Microsoft.EntityFrameworkCore;
using BookWormsOnline.Models;

namespace BookWormsOnline.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) 
            : base(options) // ✅ Pass options to the base constructor
        {
        }

        public DbSet<Member> Members { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
    }

    public class AuditLog
{
    public int Id { get; set; }
    public string UserEmail { get; set; }
    public string Action { get; set; }
    public DateTime Timestamp { get; set; }
}
}



