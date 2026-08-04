using System;
using System.Collections.Generic;
using System.Text;

namespace OptimizationXY.Charger.Models.Settings
{
    public sealed class ScanStorageSettings
    {

        /// <summary>
        /// Number of days to keep saved scans before deleting them.
        /// </summary>
        public int DaysToKeepSavedScans { get; set; } = 90;

        public bool AutoDeleteOldScans { get; set; } = true;

    }
}
