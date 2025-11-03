using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sh.Autofit.New.Dal.UnitOfWork;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.Interfaces.Repos;
using Sh.Autofit.New.Interfaces.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sh.Autofit.New.DependencyInjection
{
    public static class AddSqlExtensions
    {
        public static IServiceCollection AddSql(
            this IServiceCollection services,
            IConfiguration config,
            string? connectionName = "DefaultConnection",
            Action<DbContextOptionsBuilder>? configureDb = null)
        {
            // Get connection string from caller’s config
            var connString = config.GetConnectionString(connectionName);
            if (string.IsNullOrWhiteSpace(connString))
                throw new InvalidOperationException($"Missing connection string '{connectionName}'.");

            services.AddDbContext<ShAutofitContext>(options =>
            {
                if (configureDb is not null)
                    configureDb(options);
                else
                    options.UseSqlServer(connString);
            });

            // Repositories, UoW, etc.
            services.AddScoped(typeof(IRepository<>), typeof(Sh.Autofit.New.Dal.Repos.EfRepository<>));
            services.AddScoped<IUnitOfWork, EfUnitOfWork<ShAutofitContext>>();

            return services;
        }
    }
}