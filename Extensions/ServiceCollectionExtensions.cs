using DatabaseBackup.Models.Configurations;

namespace DatabaseBackup.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Loads the configuration specified in the <see cref="IConfiguration"/> object and registers as <see cref="IOptions{TOptions}"/> with DI framework.
        /// </summary>
        /// <param name="services">Specifies the contract for a collection of service descriptors.</param>
        /// <param name="configuration">Represents a set of key/value application configuration properties.</param>
        /// <param name="sectionKey">The section key name that needs to be checked and loaded from <see cref="IConfiguration"/> to populate the configuration model.</param>
        /// <typeparam name="TConfiguration">The type of the Configuration model that will be populated and registered with the DI framework.</typeparam>
        public static void LoadConfigurationFor<TConfiguration>(this IServiceCollection services, IConfiguration configuration, string sectionKey)
            where TConfiguration : class
        {
            var section = configuration.GetSection(sectionKey);

            // Checks whether the configuration can be retrieved from the section
            var appConfiguration = section.Get<TConfiguration>();
            if (appConfiguration == null)
                throw new ArgumentNullException($"Configuration for '{typeof(TConfiguration).Name}' could not be loaded from appsettings.json or environment variables!");

            services.AddOptions<TConfiguration>().Bind(section);
        }

        public static void RegisterConfigurations(this IServiceCollection services, IConfiguration configuration)
        {
            services.LoadConfigurationFor<DatabaseConfigurations>(configuration, "DatabaseBackup");
        }

    }
}
