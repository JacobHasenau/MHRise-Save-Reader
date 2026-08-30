using System.Text;

namespace MHRise_Save_Reader;

internal static class Program
{
    private const int HuntersGoldShieldTarget = 1000;

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        try
        {
            var options = CommandLineOptions.Parse(args);
            var savePath = options.SavePath ?? PromptForValue("Enter the path to your Monster Hunter Rise save file: ");

            if (string.IsNullOrWhiteSpace(savePath))
            {
                Console.Error.WriteLine("A save file path is required.");
                return 1;
            }

            var reader = new SaveFileReader();
            byte[] saveData;

            try
            {
                saveData = reader.ReadSaveData(savePath, options.SteamId, options.CurveIndex);
            }
            catch (SaveFileRequiresSteamIdException)
            {
                var steamIdText = PromptForValue("This save appears to use Citrus encryption. Enter your SteamID64: ");
                if (!ulong.TryParse(steamIdText, out var steamId))
                {
                    Console.Error.WriteLine("A valid SteamID64 is required to decrypt this save.");
                    return 1;
                }

                var curveIndexText = PromptForValue("Optional Citrus curve index (press Enter to brute force): ");
                int? curveIndex = int.TryParse(curveIndexText, out var parsedCurveIndex) ? parsedCurveIndex : null;
                saveData = reader.ReadSaveData(savePath, steamId, curveIndex);
            }

            var parser = new MonsterKillCounter();
            var killCounts = parser.GetMasterRankKillCounts(saveData);
            var totalKills = killCounts.Sum(entry => entry.KillCount);
            var remainingKills = Math.Max(0, HuntersGoldShieldTarget - totalKills);

            Console.WriteLine();
            Console.WriteLine($"Master Rank large monster kill counts from: {Path.GetFullPath(savePath)}");
            Console.WriteLine(new string('-', 56));
            Console.WriteLine($"{"Monster",-38}Kills");
            Console.WriteLine(new string('-', 56));

            foreach (var killCount in killCounts)
            {
                Console.WriteLine($"{killCount.MonsterName,-38}{killCount.KillCount,5}");
            }

            Console.WriteLine(new string('-', 56));
            Console.WriteLine($"{"Total",-38}{totalKills,5}");
            Console.WriteLine();
            Console.WriteLine($"Hunter's Gold Shield progress: {totalKills}/{HuntersGoldShieldTarget}");
            Console.WriteLine(remainingKills == 0
                ? "Hunter's Gold Shield requirement met."
                : $"{remainingKills} more large monster hunts needed.");
            return 0;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            PrintUsage();
            return 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Unable to read the save file: {exception.Message}");
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Failed to process save file: {exception.Message}");
            return 1;
        }
    }

    private static string? PromptForValue(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine()?.Trim();
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: dotnet run -- [save-file-path] [--steamid <steamid64>] [--curve-index <index>]");
    }

    private sealed record CommandLineOptions(string? SavePath, ulong? SteamId, int? CurveIndex)
    {
        public static CommandLineOptions Parse(string[] args)
        {
            string? savePath = null;
            ulong? steamId = null;
            int? curveIndex = null;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--steamid":
                        if (++index >= args.Length || !ulong.TryParse(args[index], out var parsedSteamId))
                        {
                            throw new ArgumentException("The --steamid option requires a valid unsigned 64-bit SteamID value.");
                        }

                        steamId = parsedSteamId;
                        break;

                    case "--curve-index":
                        if (++index >= args.Length || !int.TryParse(args[index], out var parsedCurveIndex))
                        {
                            throw new ArgumentException("The --curve-index option requires a valid integer value.");
                        }

                        curveIndex = parsedCurveIndex;
                        break;

                    default:
                        if (args[index].StartsWith("--", StringComparison.Ordinal))
                        {
                            throw new ArgumentException($"Unknown option: {args[index]}");
                        }

                        savePath ??= args[index];
                        break;
                }
            }

            return new CommandLineOptions(savePath, steamId, curveIndex);
        }
    }
}
