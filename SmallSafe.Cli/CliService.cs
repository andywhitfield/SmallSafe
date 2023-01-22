using System.Security.Cryptography;
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
        var masterPassword = "";
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
            _logger.LogInformation("x. Exit");

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
                        _logger.LogWarning("No file entered, try again.");
                        firstPass = true;
                        safeDbFile = null;
                        break;
                    }

                    if (File.Exists(safeDbFile))
                    {
                        _logger.LogWarning("File already exists. To create a new Safe DB file, please enter a new filename. Or choose option 1 to open an existing Safe DB file.");
                        firstPass = true;
                        safeDbFile = null;
                        break;
                    }

                    _logger.LogInformation("Enter a new master password. Ensure it is a strong password - ideally a long passphrase using a combination of uppercase and lowercase letters, numbers, and punctuation.");
                    ConsoleKeyInfo key;
                    StringBuilder enteredMasterPassword = new();
                    while ((key = Console.ReadKey(true)).Key != ConsoleKey.Enter)
                    {
                        enteredMasterPassword.Append(key.KeyChar);
                        Console.Write('*');
                    }
                    Console.WriteLine();
                    masterPassword = enteredMasterPassword.ToString();

                    if (masterPassword.Length == 0)
                    {
                        _logger.LogWarning("No master password entered, try again.");
                        firstPass = true;
                        safeDbFile = null;
                        break;
                    }

                    _logger.LogInformation("Re-type your new master password:");
                    enteredMasterPassword.Clear();
                    while ((key = Console.ReadKey(true)).Key != ConsoleKey.Enter)
                    {
                        enteredMasterPassword.Append(key.KeyChar);
                        Console.Write('*');
                    }
                    Console.WriteLine();

                    if (masterPassword != enteredMasterPassword.ToString())
                    {
                        _logger.LogWarning("Master passwords do not match, try again.");
                        firstPass = true;
                        safeDbFile = null;
                        break;
                    }

                    {
                        await using FileStream fileStream = new(safeDbFile, FileMode.CreateNew, FileAccess.Write);
                        await _safeDbService.WriteAsync(masterPassword, Enumerable.Empty<SafeGroup>(), fileStream);
                    }

                    break;
                case "x":
                    return;
                default:
                    _logger.LogWarning("Unknown option, try again");
                    firstPass = true;
                    safeDbFile = null;
                    break;
            }
        }

        _logger.LogInformation($"Using password safe db {safeDbFile}...");

        if (string.IsNullOrEmpty(masterPassword))
        {
            while (true)
            {
                _logger.LogInformation("Enter the master password for the Safe DB:");
                ConsoleKeyInfo key;
                StringBuilder enteredMasterPassword = new();
                while ((key = Console.ReadKey(true)).Key != ConsoleKey.Enter)
                {
                    enteredMasterPassword.Append(key.KeyChar);
                    Console.Write('*');
                }
                Console.WriteLine();
                masterPassword = enteredMasterPassword.ToString();

                if (masterPassword.Length == 0)
                {
                    _logger.LogWarning("No master password entered, try again.");
                    continue;
                }

                try
                {
                    await using FileStream fileStream = new(safeDbFile, FileMode.Open, FileAccess.Read);
                    await _safeDbService.ReadAsync(masterPassword, fileStream);
                }
                catch (CryptographicException)
                {
                    _logger.LogWarning("Cannot read Safe DB using the given password, try again");
                    continue;
                }

                break;
            }
        }

        await ManageSafeDbAsync(safeDbFile, masterPassword);
    }

    private async Task ManageSafeDbAsync(string safeDbFile, string masterPassword)
    {
        while (true)
        {
            IEnumerable<SafeGroup> safeGroups;
            {
                await using FileStream fileStream = new(safeDbFile, FileMode.Open, FileAccess.Read);
                safeGroups = await _safeDbService.ReadAsync(masterPassword, fileStream);
            }

            _logger.LogInformation("Groups:");
            if (!safeGroups.Any())
            {
                _logger.LogInformation(" <no groups>");
            }
            else
            {
                var groupNum = 1;
                foreach (var safeGroup in safeGroups)
                    _logger.LogInformation($"{groupNum++}. {safeGroup.Name}");
            }

            _logger.LogInformation("");
            _logger.LogInformation("n. Create a new group");
            _logger.LogInformation("x. Exit");

            var selection = Console.ReadLine();
            switch (selection)
            {
                case "n":
                    // TODO: create a new group
                    break;
                case "x":
                    return;
                default:
                    // is it a valid group number
                    if (int.TryParse(selection, out var groupNum) && groupNum > 0 && groupNum <= safeGroups.Count())
                    {
                        // show group
                    }
                    else
                    {
                        _logger.LogWarning(@"Unknown option");
                    }
                    break;
            }
        }
    }
}