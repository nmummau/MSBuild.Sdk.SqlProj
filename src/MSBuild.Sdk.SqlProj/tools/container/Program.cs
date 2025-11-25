using System.Diagnostics;

namespace SqlPackageRunner;

class Program
{
    static int Main(string[] args)
    {
        const string workDir = "/work";
        const string sqlPkgDir = "/app/sqlpkg";
        
        string? dacpacName = Environment.GetEnvironmentVariable("DACPAC_NAME");

        // If DACPAC_NAME not set, auto-pick the single dacpac in /work
        if (string.IsNullOrWhiteSpace(dacpacName))
        {
            var candidates = Directory.EnumerateFiles(workDir, "*.dacpac").ToArray();
            if (candidates.Length == 1)
            {
                dacpacName = Path.GetFileName(candidates[0]);
            }
            else if (candidates.Length == 0)
            {
                Console.Error.WriteLine("No .dacpac files found in /work");
                return 2;
            }
            else
            {
                Console.Error.WriteLine($"Multiple .dacpac files found. Set DACPAC_NAME environment variable. Found: {string.Join(", ", candidates.Select(Path.GetFileName))}");
                return 2;
            }
        }

        var dacpacPath = Path.Combine(workDir, dacpacName);
        if (!File.Exists(dacpacPath))
        {
            Console.Error.WriteLine($"DACPAC not found: {dacpacPath}");
            return 2;
        }

        // Find sqlpackage executable
        var sqlpackageDll = Path.Combine(sqlPkgDir, ".store", "microsoft.sqlpackage");
        if (!Directory.Exists(sqlpackageDll))
        {
            Console.Error.WriteLine($"SqlPackage not found in {sqlPkgDir}");
            return 127;
        }

        // Find the actual sqlpackage.dll in the store
        var sqlpackageExe = Directory.EnumerateFiles(sqlpackageDll, "sqlpackage.dll", SearchOption.AllDirectories)
            .FirstOrDefault();
        
        if (string.IsNullOrEmpty(sqlpackageExe) || !File.Exists(sqlpackageExe))
        {
            Console.Error.WriteLine("sqlpackage.dll not found in tool store");
            return 127;
        }

        // Build arguments for sqlpackage
        var finalArgs = new List<string> { sqlpackageExe };
        
        // Add default action if not specified
        bool hasAction = args.Any(a => 
            a.StartsWith("-Action:", StringComparison.OrdinalIgnoreCase) ||
            a.StartsWith("/Action:", StringComparison.OrdinalIgnoreCase));
        
        if (!hasAction)
        {
            finalArgs.Add("/Action:Publish");
        }

        // Add source file if not specified
        bool hasSource = args.Any(a => 
            a.StartsWith("-SourceFile:", StringComparison.OrdinalIgnoreCase) ||
            a.StartsWith("/SourceFile:", StringComparison.OrdinalIgnoreCase));
        
        if (!hasSource)
        {
            finalArgs.Add($"/SourceFile:{dacpacPath}");
        }

        // Check for publish profile
        var profileArg = args.FirstOrDefault(a => 
            a.StartsWith("-Profile:", StringComparison.OrdinalIgnoreCase) ||
            a.StartsWith("/Profile:", StringComparison.OrdinalIgnoreCase));
        
        if (!string.IsNullOrEmpty(profileArg))
        {
            // Extract profile filename
            var profileFile = profileArg.Split(':', 2)[1];
            var profilePath = Path.Combine(workDir, profileFile);
            
            if (!File.Exists(profilePath))
            {
                Console.Error.WriteLine($"Publish profile not found: {profilePath}");
                return 2;
            }
        }

        // Add user arguments
        finalArgs.AddRange(args);

        // Execute sqlpackage
        var psi = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false
        };
        
        foreach (var arg in finalArgs)
        {
            psi.ArgumentList.Add(arg);
        }

        try
        {
            using var process = Process.Start(psi);
            if (process == null)
            {
                Console.Error.WriteLine("Failed to start sqlpackage");
                return 127;
            }
            
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to launch sqlpackage: {ex.Message}");
            return 127;
        }
    }
}
