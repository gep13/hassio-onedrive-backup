﻿using hassio_onedrive_backup;
using hassio_onedrive_backup.Contracts;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace onedrive_backup.Telemetry
{
    public class TelemetryManager
	{
		private const string AppInsightsConnection = "InstrumentationKey=039e8125-ce48-4cce-94e4-71527e72214c;IngestionEndpoint=https://westeurope-5.in.applicationinsights.azure.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.com/";
		private const string ClientIdPath = ".clientId";

		private TelemetryClient _telemetryClient;
		private Guid _clientId;
		private readonly ConsoleLogger _logger;

		public TelemetryManager(ConsoleLogger logger)
		{
			TelemetryConfiguration configuration = TelemetryConfiguration.CreateDefault();
			configuration.ConnectionString = AppInsightsConnection;
			_telemetryClient = new TelemetryClient(configuration);
			_clientId = CheckClientId();
			_logger = logger;
		}

		private Guid CheckClientId()
		{
			if (File.Exists(ClientIdPath) == false)
			{
				var clientId = Guid.NewGuid();
				File.WriteAllText(ClientIdPath, clientId.ToString());
				return clientId;
			}

			string clientIdStr = File.ReadAllText(ClientIdPath);
			return Guid.Parse(clientIdStr);
		}

		public async Task SendConfig(AddonOptions options)
		{
			try
			{
				var telemetryMsg = new EventTelemetry("Config")
				{
					Timestamp = DateTime.UtcNow,
					Properties = 
					{
						{ "ClientId", _clientId.ToString() },
						{ nameof(AddonOptions.FileSyncEnabled), options.FileSyncEnabled.ToString() },
						{ nameof(AddonOptions.GenerationalBackups), options.GenerationalBackups.ToString() },
						{ nameof(AddonOptions.BackupAllowedHours), options.BackupAllowedHours?.ToString() },
						{ nameof(AddonOptions.InstanceName), (string.IsNullOrEmpty(options.InstanceName) == false).ToString() },
						{ nameof(AddonOptions.MonitorAllLocalBackups), options.MonitorAllLocalBackups.ToString() },
						{ nameof(AddonOptions.IgnoreUpgradeBackups), options.IgnoreUpgradeBackups.ToString() },
						{ nameof(AddonOptions.IgnoreAllowedHoursForFileSync), options.IgnoreAllowedHoursForFileSync.ToString() },
						{ nameof(AddonOptions.AddonVersion), AddonOptions.AddonVersion },
						{ nameof(AddonOptions.NotifyOnError), options.NotifyOnError.ToString() },
						{ nameof(AddonOptions.DarkMode), options.DarkMode.ToString() }
					}
				};

				_telemetryClient.TrackEvent(telemetryMsg);
			}
			catch (Exception e)
			{
				_logger.LogError(e.ToString());
			}		
		}
	}
}
