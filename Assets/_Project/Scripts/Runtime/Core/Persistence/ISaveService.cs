using System;
using OutsiderDriver.Core.State;

namespace OutsiderDriver.Core.Persistence
{
    /// <summary>
    /// Service contract for atomic disk-based save file management, corruption resilience,
    /// and save state hydration (GAME_DESIGN §2 & Systems Architecture §1).
    /// </summary>
    public interface ISaveService
    {
        /// <summary>
        /// Default slot identifier used for standard game saves.
        /// </summary>
        public const string DefaultSlotName = "save_slot_0";

        /// <summary>
        /// Absolute path to the active save directory.
        /// </summary>
        string SaveDirectoryPath { get; }

        /// <summary>
        /// In-memory cache of the most recently saved or loaded state.
        /// </summary>
        SaveStateData CurrentSaveState { get; }

        /// <summary>
        /// Fired when save state has been successfully written to disk.
        /// </summary>
        event Action<SaveStateData> OnSaveCompleted;

        /// <summary>
        /// Fired when save state has been successfully loaded from disk and injected into runtime services.
        /// </summary>
        event Action<SaveStateData> OnLoadCompleted;

        /// <summary>
        /// Fired when a save or load operation encounters an I/O or parsing error.
        /// </summary>
        event Action<string> OnSaveError;

        /// <summary>
        /// Checks if a valid save file exists for the given slot.
        /// </summary>
        bool HasSaveFile(string slotName = DefaultSlotName);

        /// <summary>
        /// Exports runtime state from PlayerStateService and writes it atomically to disk.
        /// </summary>
        bool Save(string slotName = DefaultSlotName);

        /// <summary>
        /// Reads save state from disk and injects it into PlayerStateService and TimeService.
        /// </summary>
        bool Load(string slotName = DefaultSlotName);

        /// <summary>
        /// Deletes the save file and its backup for the given slot.
        /// </summary>
        bool DeleteSave(string slotName = DefaultSlotName);

        /// <summary>
        /// Resolves the absolute file path on disk for a given slot name.
        /// </summary>
        string GetSaveFilePath(string slotName);
    }
}
