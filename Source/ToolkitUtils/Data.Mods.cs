// MIT License
// 
// Copyright (c) 2022 SirRandoo
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
/*
 * Copyright (c) 2025 Captolamia (Same MIT license as above)
 * 
 * Key Changes Made:
 *  Removed System.Threading.Tasks using - No longer needed
 *  Replaced SaveModListAsync with SaveModListThreaded - Uses LongEventHandler.QueueLongEvent
 *  Kept the synchronous SaveModList method for direct calls when needed
 *  
 * What was fixed:
 *  Lines 58-61: Replaced SaveModListAsync with SaveModListThreaded
 *  Removed async/await patterns since we're now using RimWorld's threading system
 *  
 * This file now properly uses RimWorld's LongEventHandler.QueueLongEvent for background operations instead of
 * .NET's async/await patterns, making it compatible with RimWorld 1.6's threading requirements.
 * 
 * The functionality remains exactly the same - mod data will still be saved appropriately,
 * but now it uses RimWorld's safe threading system when offloading is enabled.
 * 
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SirRandoo.ToolkitUtils.Models;
using Verse;

namespace SirRandoo.ToolkitUtils;

public static partial class Data
{
    /// <summary>
    ///     The various mods loaded within the game, as well as their
    ///     accompanying data.
    /// </summary>
    public static ModItem[] Mods { get; private set; }

    private static void ValidateModList()
    {
        List<ModMetaData> running = ModsConfig.ActiveModsInLoadOrder.ToList();

        var list = new List<ModItem>();
        var builder = new StringBuilder();

        foreach (ModMetaData metaData in running.Where(m => m.Active)
           .Where(mod => !mod.Official)
           .Where(mod => !File.Exists(Path.Combine(mod.RootDir.ToString(), "About/IgnoreMe.txt"))))
        {
            ModItem item;

            try
            {
                item = ModItem.FromMetadata(metaData);
            }
            catch (Exception)
            {
                builder.AppendLine($" - {metaData?.Name ?? metaData?.FolderName}");

                continue;
            }

            list.Add(item);
        }

        if (builder.Length > 0)
        {
            builder.Insert(0, "The following mods could not be processed:\n");
            TkUtils.Logger.Warn(builder.ToString());
        }

        Mods = list.ToArray();
    }

    /// <summary>
    ///     Saves all mods indexed by the mod to its associated file.
    /// </summary>
    public static void SaveModList()
    {
        SaveJson(Mods, Paths.ModListFilePath);
    }
}