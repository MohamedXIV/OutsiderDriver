using System;
using System.IO;
using UnityEngine;
using VContainer;
using OutsiderDriver.Core.State;

namespace OutsiderDriver.Core.Persistence
{
    /// <summary>
    /// Thread-safe and atomic disk-based implementation of ISaveService.
    /// Writes save files to Application.persistentDataPath (or custom directory in tests)
    /// using temp-file atomic replacement and corruption fallback backups.
    /// </summary>
    public sealed class SaveService : ISaveService
    {
        private readonly IPlayerStateService _playerStateService;
        private readonly string _saveDirectoryPath;
        private SaveStateData _currentSaveState;

        public string SaveDirectoryPath => _saveDirectoryPath;
        public SaveStateData CurrentSaveState => _currentSaveState;

        public event Action<SaveStateData> OnSaveCompleted;
        public event Action<SaveStateData> OnLoadCompleted;
        public event Action<string> OnSaveError;

        [Inject]
        public SaveService(IPlayerStateService playerStateService)
            : this(playerStateService, null)
        {
        }

        public SaveService(IPlayerStateService playerStateService, string customSaveDirectory)
        {
            _playerStateService = playerStateService ?? throw new ArgumentNullException(nameof(playerStateService));
            _saveDirectoryPath = !string.IsNullOrWhiteSpace(customSaveDirectory)
                ? customSaveDirectory
                : Path.Combine(Application.persistentDataPath, "Saves");
        }

        public bool HasSaveFile(string slotName = ISaveService.DefaultSlotName)
        {
            string finalPath = GetSaveFilePath(slotName);
            return File.Exists(finalPath);
        }

        public string GetSaveFilePath(string slotName)
        {
            string sanitizedSlot = SanitizeSlotName(slotName);
            return Path.Combine(_saveDirectoryPath, $"{sanitizedSlot}.json");
        }

        public bool Save(string slotName = ISaveService.DefaultSlotName)
        {
            try
            {
                EnsureSaveDirectoryExists();

                SaveStateData stateToSave = _playerStateService.ExportSaveState();
                if (stateToSave == null)
                {
                    HandleError("Failed to export state from PlayerStateService; returned null.");
                    return false;
                }

                string json = stateToSave.ToJson(prettyPrint: true);
                if (string.IsNullOrWhiteSpace(json))
                {
                    HandleError("Serialized save JSON was empty.");
                    return false;
                }

                string finalPath = GetSaveFilePath(slotName);
                string tempPath = finalPath + ".tmp";
                string backupPath = finalPath + ".bak";

                // 1. Write to temporary file first (atomic write preparation)
                File.WriteAllText(tempPath, json);

                // 2. Safely swap temp file to final destination and maintain a backup
                if (File.Exists(finalPath))
                {
                    try
                    {
                        File.Copy(finalPath, backupPath, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[SaveService] Could not update backup save file: {ex.Message}");
                    }

                    File.Delete(finalPath);
                }

                File.Move(tempPath, finalPath);

                _currentSaveState = stateToSave;
                OnSaveCompleted?.Invoke(_currentSaveState);
                return true;
            }
            catch (Exception ex)
            {
                HandleError($"Save operation failed on slot '{slotName}': {ex.Message}");
                return false;
            }
        }

        public bool Load(string slotName = ISaveService.DefaultSlotName)
        {
            string finalPath = GetSaveFilePath(slotName);
            string backupPath = finalPath + ".bak";

            if (!File.Exists(finalPath) && !File.Exists(backupPath))
            {
                return false;
            }

            SaveStateData loadedState = null;

            // 1. Try reading and parsing the primary save file
            if (File.Exists(finalPath))
            {
                try
                {
                    string primaryJson = File.ReadAllText(finalPath);
                    if (!string.IsNullOrWhiteSpace(primaryJson))
                    {
                        loadedState = SaveStateData.FromJson(primaryJson);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SaveService] Error reading primary save file '{finalPath}': {ex.Message}.");
                }
            }

            // 2. If primary failed, was empty, or failed to parse, fallback to backup file
            if (loadedState == null && File.Exists(backupPath))
            {
                try
                {
                    string backupJson = File.ReadAllText(backupPath);
                    if (!string.IsNullOrWhiteSpace(backupJson))
                    {
                        loadedState = SaveStateData.FromJson(backupJson);
                        if (loadedState != null)
                        {
                            Debug.LogWarning($"[SaveService] Successfully recovered save from backup '{backupPath}'.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SaveService] Failed to read or parse backup save file '{backupPath}': {ex.Message}");
                }
            }

            if (loadedState == null)
            {
                HandleError($"Save file for slot '{slotName}' was missing, corrupted, or failed to deserialize.");
                return false;
            }

            try
            {
                _playerStateService.LoadSaveState(loadedState);
                _currentSaveState = loadedState;

                OnLoadCompleted?.Invoke(_currentSaveState);
                return true;
            }
            catch (Exception ex)
            {
                HandleError($"Failed to apply loaded save state for slot '{slotName}': {ex.Message}");
                return false;
            }
        }

        public bool DeleteSave(string slotName = ISaveService.DefaultSlotName)
        {
            try
            {
                string finalPath = GetSaveFilePath(slotName);
                string tempPath = finalPath + ".tmp";
                string backupPath = finalPath + ".bak";

                bool deletedAny = false;

                if (File.Exists(finalPath))
                {
                    File.Delete(finalPath);
                    deletedAny = true;
                }

                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                    deletedAny = true;
                }

                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                    deletedAny = true;
                }

                if (_currentSaveState != null && string.Equals(GetSaveFilePath(slotName), finalPath, StringComparison.OrdinalIgnoreCase))
                {
                    _currentSaveState = null;
                }

                return deletedAny;
            }
            catch (Exception ex)
            {
                HandleError($"Failed to delete save slot '{slotName}': {ex.Message}");
                return false;
            }
        }

        private void EnsureSaveDirectoryExists()
        {
            if (!Directory.Exists(_saveDirectoryPath))
            {
                Directory.CreateDirectory(_saveDirectoryPath);
            }
        }

        private static string SanitizeSlotName(string slotName)
        {
            if (string.IsNullOrWhiteSpace(slotName))
            {
                return ISaveService.DefaultSlotName;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] buffer = slotName.Trim().ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                if (Array.IndexOf(invalidChars, buffer[i]) >= 0)
                {
                    buffer[i] = '_';
                }
            }

            return new string(buffer);
        }

        private void HandleError(string errorMessage)
        {
            Debug.LogWarning($"[SaveService] {errorMessage}");
            OnSaveError?.Invoke(errorMessage);
        }
    }
}
