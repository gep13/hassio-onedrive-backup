﻿namespace hassio_onedrive_backup.Storage
{
    internal class LocalStorage
    {
        public const string TempFolder = "../tmp";
        private const string oldTempFolder = "./tmp";
        private static HashSet<Flag> setFlags = new();

        public static void InitializeTempStorage(ConsoleLogger logger)
        {
            // Legacy cleanup
            if (Directory.Exists(oldTempFolder))
            {
                logger.LogVerbose($"Deleting deprecated temp storage folder");
                Directory.Delete(oldTempFolder, true);
            }

            // Clear temporary storage
            if (Directory.Exists(TempFolder))
            {
                if (Directory.EnumerateFiles($"{TempFolder}").Any())
                {
                    logger.LogVerbose("Cleaning up temporary artifcats");
                }

                Directory.Delete(TempFolder, true); 
            }

            // (Re)Create temporary directory
            Directory.CreateDirectory(TempFolder);
        }

        public static bool CheckAndMarkFlag(Flag flag)
        {
            string fileName = $"./.{flag}";
            if (setFlags.Contains(flag) || File.Exists(fileName))
            {
                return true;
            }

            File.Create(fileName);
            setFlags.Add(flag);
            return false;        
        }

        public enum Flag
        {
            NativeSettings,
        }
    }
}
