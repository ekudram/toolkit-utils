// XenotypeHelper.cs - Create this as a new file
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SirRandoo.ToolkitUtils.Helpers
{
    public static class XenotypeHelper
    {
        private static List<XenotypeDef> _allXenotypes;

        public static List<XenotypeDef> AllXenotypes
        {
            get
            {
                if (_allXenotypes == null)
                {
                    RefreshXenotypeCache();
                }
                return _allXenotypes;
            }
        }

        public static void RefreshXenotypeCache()
        {
            _allXenotypes = new List<XenotypeDef>();

            if (!ModsConfig.BiotechActive)
                return;

            _allXenotypes = DefDatabase<XenotypeDef>.AllDefs
                .Where(x => x != XenotypeDefOf.Baseliner)
                .Where(x => !x.defName.Contains("Test") && !x.defName.Contains("Debug"))
                .OrderBy(x => x.label)
                .ToList();

            TkUtils.Logger.Warn($"Loaded {_allXenotypes.Count} xenotypes for filtering");
        }

        public static XenotypeDef GetXenotypeDef(string xenotypeInput)
        {
            if (!ModsConfig.BiotechActive || string.IsNullOrEmpty(xenotypeInput))
                return null;

            return DefDatabase<XenotypeDef>.AllDefs.FirstOrDefault(x =>
                x.defName.Equals(xenotypeInput, StringComparison.OrdinalIgnoreCase) ||
                x.label.Equals(xenotypeInput, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetDisplayName(XenotypeDef xenotype)
        {
            return xenotype?.label?.CapitalizeFirst() ?? xenotype?.defName ?? "Unknown";
        }
    }
}