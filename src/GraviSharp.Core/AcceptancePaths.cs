using System;
using System.IO;

namespace GraviSharp.Core;

/// <summary>
/// Shared helpers for locating the acceptance directory.
/// </summary>
public static class AcceptancePaths
{
    /// <summary>
    /// Locates the 'plans/acceptance' directory by walking up from AppContext.BaseDirectory.
    /// </summary>
    /// <returns>Full path to 'plans/acceptance'.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown if not found within 12 levels.</exception>
    public static string FindAcceptanceDir()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12; i++)
        {
            string candidate = Path.Combine(dir, "plans", "acceptance");
            if (Directory.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate 'plans/acceptance'");
    }
}
