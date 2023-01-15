﻿using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Graph;
using hassio_onedrive_backup.Hass;
using Microsoft.Graph;
using System.Collections;
using System.Diagnostics;
using System.Security.Cryptography;

namespace hassio_onedrive_backup.Sync
{
    internal class SyncManager
    {
        private const string HashAlgo = "SHA256";
        private const string OneDriveFileSyncRootDir = "FileSync";
        private AddonOptions _addonOptions;
        private IGraphHelper _graphHelper;
        private IHassioClient _hassIoClient;
        private BitArray _allowedHours;
        private HassOnedriveFileSyncEntityState _hassEntityState;

        public SyncManager(AddonOptions addonOptions, IGraphHelper graphHelper, IHassioClient hassIoClient, BitArray allowedHours)
        {
            _addonOptions = addonOptions;
            _graphHelper = graphHelper;
            _hassIoClient = hassIoClient;
            _allowedHours = allowedHours;
            _hassEntityState = HassOnedriveFileSyncEntityState.Initialize(hassIoClient);
        }

        public async void SyncLoop(CancellationToken ct)
        {
            while (true && ct.IsCancellationRequested == false)
            {
                try
                {
                    var now = DateTimeHelper.Instance!.Now;

                    // Check if we are in the allowed hours
                    if (_allowedHours[now.Hour] == false)
                    {
                        continue;
                    }

                    HassOnedriveFileSyncEntityState.Instance.State = HassOnedriveFileSyncEntityState.FileState.Syncing;
                    await HassOnedriveFileSyncEntityState.Instance.UpdateBackupEntityInHass();

                    var paths = _addonOptions.SyncPaths;
                    foreach (var syncPath in paths)
                    {
                        string path = syncPath.path;
                        if (System.IO.Directory.Exists(path))
                        {
                            await SyncDirectory(path, includeSubFolders: syncPath.includeSubFolders);
                        }
                        else if (System.IO.File.Exists(path))
                        {
                            await SyncFile(path);
                        }
                        else if (System.IO.Directory.Exists(Path.GetDirectoryName(path)))
                        {
                            await SyncDirectory(Path.GetDirectoryName(path), filter: Path.GetFileName(path), includeSubFolders: syncPath.includeSubFolders);
                        }
                        else
                        {
                            ConsoleLogger.LogError($"Error: Path {path} was not found");
                        }
                    }
                        
                }
                catch (Exception ex)
                {
                    ConsoleLogger.LogError($"Error Syncing: {ex}");
                }
                finally
                {
                    HassOnedriveFileSyncEntityState.Instance.State = HassOnedriveFileSyncEntityState.FileState.Synced;
                    await HassOnedriveFileSyncEntityState.Instance.UpdateBackupEntityInHass();
                    await Task.Delay(TimeSpan.FromMinutes(5));
                }
            }
        }

        private async Task SyncFile(string path)
        {
            var fileInfo = new FileInfo(path);
            var now = DateTimeHelper.Instance!.Now;
            string fileHash = CalculateFileHash(path);
            var localFileSyncData = new SyncFileData(path, fileHash, fileInfo.Length);

            string remotePath = $"/{OneDriveFileSyncRootDir}{path}".Replace("//", "/").Replace(@"\\", @"\");
            DriveItem? remoteFile = await _graphHelper.GetItemInAppFolderAsync(remotePath);                             
            bool requiresUpload = 
                remoteFile == null 
                || remoteFile.Size != fileInfo.Length 
                || remoteFile.File.Hashes.Sha256Hash.Equals(fileHash, StringComparison.OrdinalIgnoreCase) == false;

            if (requiresUpload == false)
            {
                return;
            }
            
            ConsoleLogger.LogInfo($"File {path} out of sync. Starting Upload");
            var uploadSuccessful = await _graphHelper.UploadFileAsync(
                path, 
                now, 
                _addonOptions.InstanceName, 
                remotePath,
                async (prog) =>
                {
                    HassOnedriveFileSyncEntityState.Instance.UploadPercentage = prog;
                    await HassOnedriveFileSyncEntityState.Instance.UpdateBackupEntityInHass();
                    Debug.WriteLine($"Progress: {prog}");

                },
                flatten: false,
                omitDescription: true
            );
        }

        private string CalculateFileHash(string path)
        {
            using (var hasher = HashAlgorithm.Create(HashAlgo))
            {
                using (var fileStream = System.IO.File.OpenRead(path))
                {
                    byte[] hash = hasher!.ComputeHash(fileStream);
                    return BitConverter.ToString(hash).Replace("-", "");
                }
            }
        }

        private async Task SyncDirectory(string path, string filter = "*", bool includeSubFolders = false)
        {
            var files = System.IO.Directory.GetFiles(path, filter, includeSubFolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            // var remoteParentFolder = await VerifyRemoteFolderPathExists(path);
            foreach (var file in files)
            {
                if (System.IO.File.Exists(file))
                {
                     await SyncFile(file);
                }
            }            
        }
    }
}
