/*
 * File: ToolkitUtilitiesInitializer.cs
 * Project: ToolkitUtilities
 * 
 * Created: [Current Date]
 * Updated: [Current Date]
 * 
 * Summary:
 * Ensures initialization after ToolkitCore and Toolkit mods
 * Uses LongEventHandler to defer initialization until all mods are loaded
 */

using RimWorld;
using System;
using Verse;

namespace ToolkitUtilities
{
    [StaticConstructorOnStartup]
    public static class ToolkitUtilitiesInitializer
    {
        static ToolkitUtilitiesInitializer()
        {
            // Queue initialization to run after all other mods have completed their loading
            LongEventHandler.QueueLongEvent(
                Initialize,
                "ToolkitUtilitiesInitialization",
                false,
                ExceptionHandler
            );
        }

        private static void Initialize()
        {
            try
            {
                Log.Message("[ToolkitUtilities] Starting initialization after all mods have loaded...");

                // Add initialization logic that depends on ToolkitCore and Toolkit here
                // Example: Verify dependencies, register handlers, etc.

                Log.Message("[ToolkitUtilities] Initialization complete");
            }
            catch (Exception ex)
            {
                Log.Error($"[ToolkitUtilities] Initialization failed: {ex}");
                throw;
            }
        }

        private static void ExceptionHandler(Exception ex)
        {
            Log.Error($"[ToolkitUtilities] Initialization encountered an error: {ex}");
        }
    }
}