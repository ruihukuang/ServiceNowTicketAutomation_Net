using System;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Persistent;

public class AppDbContext(DbContextOptions options) : DbContext(options)
{
    public required DbSet<Activity> Activities { get; set; }
}
