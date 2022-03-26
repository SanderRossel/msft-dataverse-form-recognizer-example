using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace DataverseFunction
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<Form> Forms { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer("<...>");
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Form>().ToTable(nameof(Form));
        }
    }

    public class Form
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Base64Image { get; set; }
        public ICollection<FormDetail> FormDetails { get; set; }
    }

    public class FormDetail
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string TextData { get; set; }
        public int FormId { get; set; }
    }

}
