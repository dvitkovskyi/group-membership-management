// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Common.DependencyInjection;
using DIConcreteTypes;
using Entities;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Repositories.Contracts;
using Repositories.Contracts.InjectConfig;
using Repositories.GraphGroups;
using Repositories.Logging;
using Repositories.MembershipDifference;
using Repositories.SyncJobsRepository;
using System;
using System.Collections.Generic;
using System.Text;

// see https://docs.microsoft.com/en-us/azure/azure-functions/functions-dotnet-dependency-injection
[assembly: FunctionsStartup(typeof(Hosts.GraphUpdater.Startup))]

namespace Hosts.GraphUpdater
{
	public class Startup : FunctionsStartup
	{
		public override void Configure(IFunctionsHostBuilder builder)
		{
			builder.Services.AddOptions<GraphCredentials>().Configure<IConfiguration>((settings, configuration) =>
			{
				configuration.GetSection("graphCredentials").Bind(settings);
			});

			builder.Services.AddOptions<SyncJobRepoCredentials>().Configure<IConfiguration>((settings, configuration) =>
			{
				settings.ConnectionString = configuration.GetValue<string>("jobsStorageAccountConnectionString");
				settings.TableName = configuration.GetValue<string>("jobsTableName");
			});

			builder.Services.AddOptions<LogAnalyticsSecret<LoggingRepository>>().Configure<IConfiguration>((settings, configuration) =>
			{
				settings.WorkSpaceId = configuration.GetValue<string>("logAnalyticsCustomerId");
				settings.SharedKey = configuration.GetValue<string>("logAnalyticsPrimarySharedKey");
				settings.Location = nameof(GraphUpdater);
			});

			builder.Services.AddSingleton<IMembershipDifferenceCalculator<AzureADUser>, MembershipDifferenceCalculator<AzureADUser>>()
			.AddSingleton<IGraphServiceClient>((services) =>
			{
				return new GraphServiceClient(FunctionAppDI.CreateAuthProvider(services.GetService<IOptions<GraphCredentials>>().Value));
			})

			.AddScoped<IGraphGroupRepository, GraphGroupRepository>()
			.AddSingleton<ISyncJobRepository>(services =>
			{
				var creds = services.GetService<IOptions<SyncJobRepoCredentials>>();
				return new SyncJobRepository(creds.Value.ConnectionString, creds.Value.TableName);
			})
			.AddSingleton<ILogAnalyticsSecret<LoggingRepository>>(services => services.GetService<IOptions<LogAnalyticsSecret<LoggingRepository>>>().Value)
			.AddScoped<SessionMessageCollector>()
			.AddScoped<ILoggingRepository, LoggingRepository>()
			.AddScoped<IGraphUpdater, GraphUpdaterApplication>();
		}

		private class SyncJobRepoCredentials
		{
			public string ConnectionString { get; set; }
			public string TableName { get; set; }
		}
	}

}
