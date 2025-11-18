using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace MindWork.Api.Infrastructure.Persistence;

/// <summary>
/// Fábrica de DbContext para o tempo de design (design-time),
/// usada pelo comando "dotnet ef" para criar o banco e gerar migrations.
/// </summary>
public class DesignTimeMindWorkDbContextFactory : IDesignTimeDbContextFactory<MindWorkDbContext>
{
  public MindWorkDbContext CreateDbContext(string[] args)
  {
    // Lê o appsettings.json para pegar a connection string
    var configuration = new ConfigurationBuilder()
      .SetBasePath(Directory.GetCurrentDirectory())
      .AddJsonFile("appsettings.json", optional: false)
      .AddEnvironmentVariables()
      .Build();

    var connectionString = configuration.GetConnectionString("MindWorkDatabase")
                           ?? throw new InvalidOperationException("Connection string 'MindWorkDatabase' not found.");

    var optionsBuilder = new DbContextOptionsBuilder<MindWorkDbContext>();
    optionsBuilder.UseSqlServer(connectionString);

    return new MindWorkDbContext(optionsBuilder.Options);
  }
}
