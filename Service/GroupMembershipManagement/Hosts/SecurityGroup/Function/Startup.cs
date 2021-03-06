// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Common.DependencyInjection;
using DIConcreteTypes;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Repositories.Contracts;
using Repositories.Contracts.InjectConfig;
using Repositories.GraphGroups;
using Repositories.Logging;
using Repositories.ServiceBusQueue;
using System;
using System.Collections.Generic;
using System.Text;

// see https://docs.microsoft.com/en-us/azure/azure-functions/functions-dotnet-dependency-injection
[assembly: FunctionsStartup(typeof(Hosts.SecurityGroup.Startup))]

namespace Hosts.SecurityGroup
{
	public class Startup : FunctionsStartup
	{
		public override void Configure(IFunctionsHostBuilder builder)
		{
			builder.Services.AddOptions<GraphCredentials>().Configure<IConfiguration>((settings, configuration) =>
			{
				configuration.GetSection("graphCredentials").Bind(settings);
			});

			builder.Services.AddOptions<ServiceBusConfiguration>().Configure<IConfiguration>((settings, configuration) =>
			{
				settings.Namespace = configuration.GetValue<string>("differenceServiceBusNamespace");
				settings.QueueName = configuration.GetValue<string>("membershipQueueName");
			});

			builder.Services.AddOptions<LogAnalyticsSecret<LoggingRepository>>().Configure<IConfiguration>((settings, configuration) =>
			{
				settings.WorkSpaceId = configuration.GetValue<string>("logAnalyticsCustomerId");
				settings.SharedKey = configuration.GetValue<string>("logAnalyticsPrimarySharedKey");
				settings.Location = nameof(SecurityGroup);
			});

			builder.Services.AddSingleton<IGraphServiceClient>((services) =>
			{
				 return new GraphServiceClient(FunctionAppDI.CreateAuthProvider(services.GetService<IOptions<GraphCredentials>>().Value));
			})

			.AddSingleton<IMembershipServiceBusRepository, MembershipServiceBusRepository>((services) =>
			{
				var config = services.GetService<IOptions<ServiceBusConfiguration>>().Value;
				return new MembershipServiceBusRepository(serviceBusNamespacePrefix: config.Namespace, queueName: config.QueueName);
			})
			.AddSingleton<IGraphGroupRepository, GraphGroupRepository>()
			.AddSingleton<SGMembershipCalculator>()
			.AddSingleton<ILogAnalyticsSecret<LoggingRepository>>(services => services.GetService<IOptions<LogAnalyticsSecret<LoggingRepository>>>().Value)
			.AddSingleton<ILoggingRepository, LoggingRepository>();
		}

		private class ServiceBusConfiguration
		{
			public string Namespace { get; set; }
			public string QueueName { get; set; }
		}
	}
}
