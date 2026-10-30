
using Attendify.Models;
using Microsoft.EntityFrameworkCore;

namespace Attendify.Data
{
    public class AttendifyContext : DbContext
    {
        public AttendifyContext(
            DbContextOptions<AttendifyContext> options)
            : base(options)
        {
        }

        public DbSet<Person> Persons { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Lecturer> Lecturers { get; set; }
        public DbSet<ITAdmin> ITAdmins { get; set; }
        public DbSet<Institution> Institutions { get; set; }
        public DbSet<Module> Modules { get; set; }
        public DbSet<Venue> Venues { get; set; }
        public DbSet<ClassSession> ClassSessions { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<Attendance> Attendances { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>().ToTable("PERSONS");
            modelBuilder.Entity<Institution>().ToTable("INSTITUTIONS");
            modelBuilder.Entity<Student>().ToTable("STUDENTS");
            modelBuilder.Entity<Lecturer>().ToTable("LECTURERS");
            modelBuilder.Entity<ITAdmin>().ToTable("IT_ADMINS");
            modelBuilder.Entity<Module>().ToTable("MODULES");
            modelBuilder.Entity<Venue>().ToTable("VENUES");
            modelBuilder.Entity<ClassSession>().ToTable("CLASS_SESSIONS");
            modelBuilder.Entity<Enrollment>().ToTable("ENROLLMENTS");
            modelBuilder.Entity<Attendance>().ToTable("ATTENDANCES");
            modelBuilder.Entity<Student>()
             .Property(s => s.StudentId)
          .HasDefaultValueSql("NEXT VALUE FOR dbo.StudentIdSequence");
        }
    }
}