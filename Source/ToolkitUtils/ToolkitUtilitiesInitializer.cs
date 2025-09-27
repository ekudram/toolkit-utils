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
using SirRandoo.ToolkitUtils;
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
                null,
                false,
                ExceptionHandler,
                true
            );
        }

        private static void Initialize()
        {
            try
            {
                TkUtils.Logger.Log("Starting initialization after all mods have loaded...");

                // Add initialization logic that depends on ToolkitCore and Toolkit here
                // Example: Verify dependencies, register handlers, etc.

                TkUtils.Logger.Log("Initialization complete");
            }
            catch (Exception ex)
            {       
                TkUtils.Logger.Log($"Initialization failed: {ex}");
                throw;
            }
        }

        private static void ExceptionHandler(Exception ex)
        {
            TkUtils.Logger.Error($"[ToolkitUtilities] Initialization encountered an error: {ex}");
        }
    }
}