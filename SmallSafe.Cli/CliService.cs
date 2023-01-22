using System.Text;
using Microsoft.Extensions.Logging;
using SmallSafe.Secure.Model;
using SmallSafe.Secure.Services;

namespace SmallSafe.Cli;

public class CliService
{
    private readonly ILogger<CliService> _logger;
    private readonly ISafeDbService _safeDbService;

    public CliService(ILogger<CliService> logger, ISafeDbService safeDbService)
    {
        _logger = logger;
        _safeDbService = safeDbService;
    }

    public async Task ExecuteAsync(string? safeDbFile)
    {
        var firstPass = true;
        while (true)
        {
            if (!string.IsNullOrWhiteSpace(safeDbFile) && File.Exists(safeDbFile))
                break;

            if (!firstPass || !string.IsNullOrWhiteSpace(safeDbFile))
                _logger.LogWarning($"Password Safe DB '{safeDbFile}' does not exist, please try again.");

            firstPass = false;

            _logger.LogInformation("1. Choose an existing Safe DB file");
            _logger.LogInformation("2. Create a new Safe DB file");
            _logger.LogInformation("0. Exit");

            var selection = Console.ReadLine();
            switch (selection?.Trim())
            {
                case "1":
                    _logger.LogInformation("Enter Safe DB file:");
                    safeDbFile = Console.ReadLine();
                    break;
                case "2":
                    _logger.LogInformation("Enter Safe DB file:");
                    safeDbFile = Console.ReadLine();

                    if (string.IsNullOrEmpty(safeDbFile))
                    {
                        _logger.LogInformation("No file entered, try again.");
                        firstPass = true;
                        safeDbFile = null;
                        break;
                    }

                    if (File.Exists(safeDbFile))
                    {
                        _logger.LogInformation("File already exists. To create a new Safe DB file, please enter a new filename. Or choose option 1 to open an existing Safe DB file.");
                        firstPass = true;
                        safeDbFile = null;
                        break;
                    }

                    _logger.LogInformation("Enter a new master password. Ensure it is a strong password - ideally a long passphrase using a combination of uppercase and lowercase letters, numbers, and punctuation.");
                    ConsoleKeyInfo key;
                    StringBuilder masterPassword = new();
                    while ((key = Console.ReadKey(true)).Key != ConsoleKey.Enter)
                    {
                        masterPassword.Append(key.KeyChar);
                        Console.Write('*');
                    }
                    Console.WriteLine();

                    if (masterPassword.Length == 0)
                    {
                        _logger.LogInformation("No master password entered, try again.");
                        firstPass = true;
                        safeDbFile = null;
                        break;
                    }

                    {
                        using FileStream fileStream = new(safeDbFile, FileMode.CreateNew, FileAccess.Write);
                        await _safeDbService.WriteAsync(masterPassword.ToString(), Enumerable.Empty<SafeGroup>(), fileStream);
                    }

                    break;
                case "0":
                    return;
                default:
                    _logger.LogWarning("Unknown option, try again");
                    firstPass = true;
                    safeDbFile = null;
                    break;
            }
        }

        _logger.LogInformation($"Using password safe db {safeDbFile}...");

        // TODO: prompt for master password (unless we've created a new DB) and show the password groups
    }
}