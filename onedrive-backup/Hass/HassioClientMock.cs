﻿using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Hass.Events;
using Newtonsoft.Json;
using onedrive_backup;
using onedrive_backup.Contracts;
using onedrive_backup.Telemetry;
using System.Diagnostics;
using System.Net.Http;
using static hassio_onedrive_backup.Contracts.HassAddonsResponse;
using static hassio_onedrive_backup.Contracts.HassBackupsResponse;

namespace hassio_onedrive_backup.Hass
{
	internal class HassioClientMock : IHassioClient
    {
        private readonly TelemetryManager _telemetryManager;

        private List<Backup> _backups = new();
        private IDateTimeProvider _dateTimeProvider;

        public HassioClientMock(TelemetryManager telemetryManager)
        {
            _telemetryManager = telemetryManager;
        }

		public Task<bool> CreateBackupAsync(string backupName, DateTime timeStamp, bool appendTimestamp = true, bool compressed = true, string? password = null, IEnumerable<string>? folders = null, IEnumerable<string>? addons = null)
        {
            string finalBackupName = appendTimestamp ? $"{backupName}_{timeStamp.ToString("yyyy-MM-dd-HH-mm")}" : backupName;
            var backup = new Backup
            {
                Compressed = compressed,
                Name = finalBackupName,
                Protected = !string.IsNullOrEmpty(password),
                Type = folders == null && addons == null ? "full" : "partial",
                Date = DateTime.Now,
                Slug = Guid.NewGuid().ToString(),                
            };

            _backups.Add(backup);
            return Task.FromResult(true);
        }

        public Task<bool> DeleteBackupAsync(Backup backup)
        {
            var backupToRemove = _backups.SingleOrDefault(bck => bck.Slug.Equals(backup.Slug, StringComparison.OrdinalIgnoreCase));
            if (backupToRemove != null)
            {
                _backups.Remove(backupToRemove);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<string> DownloadBackupAsync(string backupSlug)
        {
            string backupFile = $"./{backupSlug}.tar";
            
            // 15MB File
            byte[] data = new byte[10 * 1024 * 1024];
            new Random().NextBytes(data);
            File.WriteAllBytes(backupFile, data); 
            return Task.FromResult(backupFile);
        }

        public Task<HassAddonInfoResponse> GetAddonInfo(string slug)
        {
            return Task.FromResult(new HassAddonInfoResponse
            {
                DataProperty = new HassAddonInfoResponse.Data
                {
                    IngressEntry = "/",
                    IngressUrl = "/"
                }
            });
        }

        public Task<List<HassAddonsResponse.Addon>> GetAddonsAsync()
        {
            return Task.FromResult(new List<HassAddonsResponse.Addon>()
            {
                new HassAddonsResponse.Addon{ Slug = "Addon1", Name = "First Addon" },
                new HassAddonsResponse.Addon { Slug = "Addon2", Name = "Second Addon" }
            });
        }

        public Task<List<Backup>> GetBackupsAsync(Predicate<Backup> filter)
        {
            return Task.FromResult(_backups);
        }

        public Task<string> GetTimeZoneAsync()
        {
            return Task.FromResult("Local");
        }

        public Task PublishEventAsync(OneDriveEvents eventType, string payload = "")
        {
            Debug.WriteLine($"EventType: {eventType}. Payload: {payload}");
            return Task.CompletedTask;
        }

		public Task RestartSelf()
		{
            return Task.CompletedTask;
		}

		public Task SendPersistentNotificationAsync(string message)
        {
            var payload = new
            {
                message = message,
                title = "hassio-onedrive-backup"
            };

            string payloadStr = JsonConvert.SerializeObject(payload);
            Debug.WriteLine(payloadStr);
            return Task.CompletedTask;
        }

        public Task UpdateHassEntityStateAsync(string entityId, string payload)
        {
            Debug.WriteLine($"EntityId: {entityId}. State: {payload}");
            return Task.CompletedTask;
        }

		public void UpdateTimeoutValue(int timeoutMinutes)
		{
            return;
		}

		public Task<bool> UploadBackupAsync(string filePath)
        {
            _backups.Add(new Backup
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                Slug = Guid.NewGuid().ToString()
            });

            return Task.FromResult(true);
        }
    }
}
