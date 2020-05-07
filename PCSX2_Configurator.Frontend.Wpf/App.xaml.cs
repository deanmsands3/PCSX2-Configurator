﻿using System;
using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PCSX2_Configurator.Core;
using PCSX2_Configurator.Settings;

namespace PCSX2_Configurator.Frontend.Wpf
{
    public partial class App : Application
    {
        private readonly IHost host;
        private static IServiceProvider ServiceProvider { get; set; }
        public static T Get<T>() => ServiceProvider.GetRequiredService<T>();

        static App()
        {
            LoadAssembliesFromDirectory($"{Directory.GetCurrentDirectory()}\\ThirdParty");
        }

        public App()
        {
            host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, builder) => builder.AddJsonFile("Settings.json", optional: false, reloadOnChange: true))
                .ConfigureServices(ConfigureServices)
                .Build();
        }

        private void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            var settings = new AppSettings();
            context.Configuration.Bind(settings, options => options.BindNonPublicProperties = true);
            services.AddTransient(provider => settings);
            services.AddTransient(provider => settings.Covers);
            services.AddTransient(provider => settings.VersionManager);

            services.AddHttpClient();
            services.AddSingleton<GameLibraryService>();
            services.AddSingleton<EmulationService>();
            services.AddSingleton<ConfigurationService>();
            services.AddSingleton<ICoverService, ChainedCoverService>();
            services.AddSingleton<VersionManagementService>();

            services.AddSingleton<MainWindow>();
            services.AddTransient<ConfigWizard>();
            services.AddTransient<VersionManager>();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            ServiceProvider = host.Services;
            Get<MainWindow>().Show();
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            host.Dispose();
        }

        private static void LoadAssembliesFromDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                foreach (var assembly in Directory.GetFiles(path, "*dll"))
                {
                    Assembly.LoadFrom(assembly);
                }
            }
        }
    }
}
