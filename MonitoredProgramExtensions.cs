using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskPilot
{
    /// <summary>
    /// Extension-Methoden für MonitoredProgram-Listen
    /// </summary>
    public static class MonitoredProgramExtensions
    {
        /// <summary>
        /// Findet ein Programm nach ProcessName (Case-Insensitive)
        /// </summary>
        public static MonitoredProgram? FindByProcessName(this IEnumerable<MonitoredProgram> programs, string processName)
        {
            return programs.FirstOrDefault(p =>
                p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Prüft, ob ein Programm mit diesem ProcessName existiert (Case-Insensitive)
        /// </summary>
        public static bool ExistsByProcessName(this IEnumerable<MonitoredProgram> programs, string processName)
        {
            return programs.Any(p =>
                p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
