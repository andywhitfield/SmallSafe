using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SmallSafe.Secure;
using SmallSafe.Secure.Model;
using SmallSafe.Secure.Services;

namespace SmallSafe.Cli;

public class CliService
{
    private readonly ILogger<CliService> _logger;
    private readonly ISafeDbService _safeDbService;
    private readonly IEncryptDecrypt _encryptDecrypt;

    public CliService(ILogger<CliService> logger, ISafeDbService safeDbService, IEncryptDecrypt encryptDecrypt)
    {
        _logger = logger;
        _safeDbService = safeDbService;
        _encryptDecrypt = encryptDecrypt;
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

                    await WriteSafeGroupsAsync(safeDbFile, masterPassword, Enumerable.Empty<SafeGroup>());

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
                masterPassword = ReadConsolePassword();
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
            var safeGroups = await ReadSafeGroupsAsync(safeDbFile, masterPassword);

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
                    _logger.LogInformation("Enter the name of the new group:");
                    var newGroupName = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(newGroupName))
                    {
                        _logger.LogWarning("No name entered, try again.");
                        break;
                    }

                    newGroupName = newGroupName.Trim();
                    if (safeGroups.Any(g => string.Equals(g.Name, newGroupName, StringComparison.OrdinalIgnoreCase)))
                    {
                        _logger.LogWarning($"Group '{newGroupName}' already exists, try again.");
                        break;
                    }

                    safeGroups = safeGroups.Append(new() { Name = newGroupName });
                    await WriteSafeGroupsAsync(safeDbFile, masterPassword, safeGroups);
                    await ManageSafeDbGroupAsync(safeDbFile, masterPassword, safeGroups, safeGroups.Single(x => x.Name == newGroupName));
                    break;
                case "x":
                    return;
                default:
                    // is it a valid group number
                    SafeGroup? selectedGroup;
                    if (int.TryParse(selection, out var groupNum) && groupNum > 0 && groupNum <= safeGroups.Count() && (selectedGroup = safeGroups.ElementAtOrDefault(groupNum - 1)) != null)
                    {
                        await ManageSafeDbGroupAsync(safeDbFile, masterPassword, safeGroups, selectedGroup);
                    }
                    else
                    {
                        _logger.LogWarning("Unknown option");
                    }
                    break;
            }
        }
    }
    private string ReadConsolePassword()
    {
        ConsoleKeyInfo key;
        StringBuilder enteredPasswordValue = new();
        while ((key = Console.ReadKey(true)).Key != ConsoleKey.Enter)
        {
            enteredPasswordValue.Append(key.KeyChar);
            Console.Write('*');
        }
        Console.WriteLine();
        return enteredPasswordValue.ToString();
    }

    private async Task ManageSafeDbGroupAsync(string safeDbFile, string masterPassword, IEnumerable<SafeGroup> safeGroups, SafeGroup safeGroup)
    {
        while (true)
        {
            _logger.LogInformation($"Group: {safeGroup.Name}");
            _logger.LogInformation("Entries:");
            if (!(safeGroup.Entries?.Any() ?? false))
            {
                _logger.LogInformation(" <no password entries in group>");
            }
            else
            {
                var entryNum = 1;
                foreach (var entry in safeGroup.Entries)
                    _logger.LogInformation($"{entryNum++}. {entry.Name}");
            }

            _logger.LogInformation("");
            _logger.LogInformation("n. Create a new entry in this group");
            _logger.LogInformation("d. Delete this group, and all entries in the group");
            _logger.LogInformation("x. Back");

            var selection = Console.ReadLine();
            switch (selection)
            {
                case "n":
                    _logger.LogInformation("Enter the name of the new entry (optional):");
                    var newEntryName = (Console.ReadLine() ?? "").Trim();

                    _logger.LogInformation("Enter the value to be encrypted for the new entry:");
                    var newValue = ReadConsolePassword();
                    var (encryptedValue, iv, salt) = await _encryptDecrypt.EncryptAsync(masterPassword, newValue);

                    safeGroup.Entries ??= new List<SafeEntry>();
                    safeGroup.Entries.Add(new() { Name = newEntryName, EncryptedValue = encryptedValue, IV = iv, Salt = salt });
                    await WriteSafeGroupsAsync(safeDbFile, masterPassword, safeGroups);

                    break;
                case "d":
                    safeGroups = safeGroups.Where(g => g.Name != safeGroup.Name);
                    await WriteSafeGroupsAsync(safeDbFile, masterPassword, safeGroups);
                    return;
                case "x":
                    return;
                default:
                    // is it a valid entry number
                    SafeEntry? selectedEntry;
                    if (safeGroup.Entries != null && int.TryParse(selection, out var entryNum) && entryNum > 0 && entryNum <= safeGroup.Entries.Count() && (selectedEntry = safeGroup.Entries.ElementAtOrDefault(entryNum - 1)) != null)
                    {
                        await ManageSafeDbEntryAsync(safeDbFile, masterPassword, safeGroups, safeGroup, selectedEntry);
                    }
                    else
                    {
                        _logger.LogWarning("Unknown option");
                    }
                    break;
            }
        }
    }

    private async Task ManageSafeDbEntryAsync(string safeDbFile, string masterPassword, IEnumerable<SafeGroup> safeGroups, SafeGroup safeGroup, SafeEntry safeEntry)
    {
        while (true)
        {
            _logger.LogInformation("1. View");
            _logger.LogInformation("2. Edit");
            _logger.LogInformation("3. Delete");
            _logger.LogInformation("x. Back");

            var selection = Console.ReadLine();
            switch (selection)
            {
                case "1":
                    _logger.LogInformation($"Group: {safeGroup.Name}");
                    _logger.LogInformation($"Entry: {safeEntry.Name}");
                    if (safeEntry.IV == null || safeEntry.Salt == null || safeEntry.EncryptedValue == null)
                    {
                        _logger.LogError("Encrypted value is corrupt");
                        break;
                    }

                    _logger.LogInformation("Encrypted value:");
                    _logger.LogInformation("---");
                    _logger.LogInformation(await _encryptDecrypt.DecryptAsync(masterPassword, safeEntry.IV, safeEntry.Salt, safeEntry.EncryptedValue));
                    _logger.LogInformation("---");
                    _logger.LogInformation("");
                    break;
                case "2":
                    _logger.LogInformation("Enter the new name of the new entry (optional):");
                    var entryNewName = (Console.ReadLine() ?? "").Trim();

                    _logger.LogInformation("Enter the new value to be encrypted for the entry:");
                    var entryNewValue = ReadConsolePassword();
                    var (encryptedValue, iv, salt) = await _encryptDecrypt.EncryptAsync(masterPassword, entryNewValue);

                    safeEntry.Name = entryNewName;
                    safeEntry.EncryptedValue = encryptedValue;
                    safeEntry.IV = iv;
                    safeEntry.Salt = salt;
                    await WriteSafeGroupsAsync(safeDbFile, masterPassword, safeGroups);

                    break;
                case "3":
                    if (safeGroup.Entries?.Remove(safeEntry) ?? false)
                    {
                        await WriteSafeGroupsAsync(safeDbFile, masterPassword, safeGroups);
                        return;
                    }
                    else
                    {
                        _logger.LogError("Could not remove entry");
                    }
                    break;
                case "x":
                    return;
                default:
                    _logger.LogWarning("Unknown option, try again");
                    break;
            }
        }
    }

    private async Task<IEnumerable<SafeGroup>> ReadSafeGroupsAsync(string safeDbFile, string masterPassword)
    {
        await using FileStream fileStream = new(safeDbFile, FileMode.Open, FileAccess.Read);
        return await _safeDbService.ReadAsync(masterPassword, fileStream);
    }

    private async Task WriteSafeGroupsAsync(string safeDbFile, string masterPassword, IEnumerable<SafeGroup> safeGroups)
    {
        await using FileStream fileStream = new(safeDbFile, FileMode.Create, FileAccess.Write);
        await _safeDbService.WriteAsync(masterPassword, safeGroups, fileStream);
    }
}