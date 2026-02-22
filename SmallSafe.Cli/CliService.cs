using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SmallSafe.Secure.Model;
using SmallSafe.Secure.Services;

namespace SmallSafe.Cli;

public class CliService(ILogger<CliService> logger, ISafeDbService safeDbService)
{
    public async Task ExecuteAsync(string? safeDbFile)
    {
        var masterPassword = "";
        var firstPass = true;
        while (true)
        {
            if (!string.IsNullOrWhiteSpace(safeDbFile) && File.Exists(safeDbFile))
                break;

            if (!firstPass || !string.IsNullOrWhiteSpace(safeDbFile))
                logger.LogWarning("Password Safe DB '{SafeDbFile}' does not exist, please try again.", safeDbFile);

            firstPass = false;

            logger.LogInformation("1. Choose an existing Safe DB file");
            logger.LogInformation("2. Create a new Safe DB file");
            logger.LogInformation("x. Exit");

            var selection = Console.ReadLine();
            switch (selection?.Trim())
            {
                case "1":
                    logger.LogInformation("Enter Safe DB file:");
                    safeDbFile = Console.ReadLine();
                    break;
                case "2":
                    logger.LogInformation("Enter Safe DB file:");
                    safeDbFile = Console.ReadLine();

                    if (string.IsNullOrEmpty(safeDbFile))
                    {
                        logger.LogWarning("No file entered, try again.");
                        firstPass = true;
                        safeDbFile = null;
                        break;
                    }

                    if (File.Exists(safeDbFile))
                    {
                        logger.LogWarning("File already exists. To create a new Safe DB file, please enter a new filename. Or choose option 1 to open an existing Safe DB file.");
                        firstPass = true;
                        safeDbFile = null;
                        break;
                    }

                    logger.LogInformation("Enter a new master password. Ensure it is a strong password - ideally a long passphrase using a combination of uppercase and lowercase letters, numbers, and punctuation.");
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
                        logger.LogWarning("No master password entered, try again.");
                        firstPass = true;
                        safeDbFile = null;
                        break;
                    }

                    logger.LogInformation("Re-type your new master password:");
                    enteredMasterPassword.Clear();
                    while ((key = Console.ReadKey(true)).Key != ConsoleKey.Enter)
                    {
                        enteredMasterPassword.Append(key.KeyChar);
                        Console.Write('*');
                    }
                    Console.WriteLine();

                    if (masterPassword != enteredMasterPassword.ToString())
                    {
                        logger.LogWarning("Master passwords do not match, try again.");
                        firstPass = true;
                        safeDbFile = null;
                        break;
                    }

                    await WriteSafeGroupsAsync(safeDbFile, masterPassword, Enumerable.Empty<SafeGroup>());

                    break;
                case "x":
                    return;
                default:
                    logger.LogWarning("Unknown option, try again");
                    firstPass = true;
                    safeDbFile = null;
                    break;
            }
        }

        logger.LogInformation("Using password safe db {SafeDbFile}...", safeDbFile);

        if (string.IsNullOrEmpty(masterPassword))
        {
            while (true)
            {
                logger.LogInformation("Enter the master password for the Safe DB:");
                masterPassword = ReadConsolePassword();
                if (masterPassword.Length == 0)
                {
                    logger.LogWarning("No master password entered, try again.");
                    continue;
                }

                try
                {
                    await using FileStream fileStream = new(safeDbFile, FileMode.Open, FileAccess.Read);
                    await safeDbService.ReadAsync(masterPassword, fileStream);
                }
                catch (CryptographicException)
                {
                    logger.LogWarning("Cannot read Safe DB using the given password, try again");
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

            logger.LogInformation("Groups:");
            if (!safeGroups.Any(g => g.DeletedTimestamp == null))
            {
                logger.LogInformation(" <no groups>");
            }
            else
            {
                var groupNum = 1;
                foreach (var safeGroup in safeGroups.Where(g => g.DeletedTimestamp == null))
                    logger.LogInformation("{GroupNum}. {SafeGroupName}", groupNum++, safeGroup.Name);
            }

            logger.LogInformation("");
            logger.LogInformation("n. Create a new group");
            logger.LogInformation("x. Exit");

            var selection = Console.ReadLine();
            switch (selection)
            {
                case "n":
                    logger.LogInformation("Enter the name of the new group:");
                    var newGroupName = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(newGroupName))
                    {
                        logger.LogWarning("No name entered, try again.");
                        break;
                    }

                    newGroupName = newGroupName.Trim();
                    if (safeGroups.Any(g => g.DeletedTimestamp == null && string.Equals(g.Name, newGroupName, StringComparison.OrdinalIgnoreCase)))
                    {
                        logger.LogWarning("Group '{NewGroupName}' already exists, try again.", newGroupName);
                        break;
                    }

                    safeGroups = safeGroups.Append(new() { Name = newGroupName });
                    await WriteSafeGroupsAsync(safeDbFile, masterPassword, safeGroups);
                    await ManageSafeDbGroupAsync(safeDbFile, masterPassword, safeGroups, safeGroups.Single(x => x.DeletedTimestamp == null && x.Name == newGroupName));
                    break;
                case "x":
                    return;
                default:
                    // is it a valid group number
                    SafeGroup? selectedGroup;
                    if (int.TryParse(selection, out var groupNum) && groupNum > 0 && groupNum <= safeGroups.Count() && (selectedGroup = safeGroups.Where(g => g.DeletedTimestamp == null).ElementAtOrDefault(groupNum - 1)) != null)
                    {
                        await ManageSafeDbGroupAsync(safeDbFile, masterPassword, safeGroups, selectedGroup);
                    }
                    else
                    {
                        logger.LogWarning("Unknown option");
                    }
                    break;
            }
        }
    }
    private static string ReadConsolePassword()
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
            logger.LogInformation("Group: {SafeGroupName}", safeGroup.Name);
            logger.LogInformation("Entries:");
            if (safeGroup.Entries?.Any(e => e.DeletedTimestamp == null) != true)
            {
                logger.LogInformation(" <no password entries in group>");
            }
            else
            {
                var entryNum = 1;
                foreach (var entry in safeGroup.Entries.Where(e => e.DeletedTimestamp == null))
                    logger.LogInformation("{EntryNum}. {EntryName}", entryNum++, entry.Name);
            }

            logger.LogInformation("");
            logger.LogInformation("n. Create a new entry in this group");
            logger.LogInformation("d. Delete this group, and all entries in the group");
            logger.LogInformation("x. Back");

            var selection = Console.ReadLine();
            switch (selection)
            {
                case "n":
                    logger.LogInformation("Enter the name of the new entry (optional):");
                    var newEntryName = (Console.ReadLine() ?? "").Trim();

                    logger.LogInformation("Enter the value to be encrypted for the new entry:");
                    var newValue = ReadConsolePassword();
                    safeGroup.Entries ??= [];
                    safeGroup.Entries.Add(new() { Name = newEntryName, EntryValue = newValue });
                    await WriteSafeGroupsAsync(safeDbFile, masterPassword, safeGroups);

                    break;
                case "d":
                    safeGroup.DeletedTimestamp = DateTime.UtcNow;
                    await WriteSafeGroupsAsync(safeDbFile, masterPassword, safeGroups);
                    return;
                case "x":
                    return;
                default:
                    // is it a valid entry number
                    SafeEntry? selectedEntry;
                    if (safeGroup.Entries != null && int.TryParse(selection, out var entryNum) && entryNum > 0 && entryNum <= safeGroup.Entries.Count(e => e.DeletedTimestamp == null) && (selectedEntry = safeGroup.Entries.Where(e => e.DeletedTimestamp == null).ElementAtOrDefault(entryNum - 1)) != null)
                    {
                        await ManageSafeDbEntryAsync(safeDbFile, masterPassword, safeGroups, safeGroup, selectedEntry);
                    }
                    else
                    {
                        logger.LogWarning("Unknown option");
                    }
                    break;
            }
        }
    }

    private async Task ManageSafeDbEntryAsync(string safeDbFile, string masterPassword, IEnumerable<SafeGroup> safeGroups, SafeGroup safeGroup, SafeEntry safeEntry)
    {
        while (true)
        {
            logger.LogInformation("1. View");
            logger.LogInformation("2. Edit");
            logger.LogInformation("3. Delete");
            logger.LogInformation("x. Back");

            var selection = Console.ReadLine();
            switch (selection)
            {
                case "1":
                    logger.LogInformation("Group: {SafeGroupName}", safeGroup.Name);
                    logger.LogInformation("Entry: {SafeEntryName}", safeEntry.Name);

                    logger.LogInformation("Entry value:");
                    logger.LogInformation("---");
                    logger.LogInformation(safeEntry.EntryValue);
                    logger.LogInformation("---");
                    logger.LogInformation("");
                    break;
                case "2":
                    logger.LogInformation("Enter the new name of the new entry (optional):");
                    var entryNewName = (Console.ReadLine() ?? "").Trim();

                    logger.LogInformation("Enter the new value for the entry:");
                    var entryNewValue = ReadConsolePassword();

                    if (safeGroup.PreserveHistory)
                    {
                        safeGroup.EntriesHistory ??= [];
                        safeGroup.EntriesHistory.Add(new()
                        {
                            Id = safeEntry.Id,
                            Name = safeEntry.Name,
                            EntryValue = safeEntry.EntryValue,
                            CreatedTimestamp = safeEntry.CreatedTimestamp,
                            UpdatedTimestamp = safeEntry.UpdatedTimestamp
                        });
                    }

                    safeEntry.Name = entryNewName;
                    safeEntry.EntryValue = entryNewValue;
                    safeEntry.UpdatedTimestamp = DateTime.UtcNow;
                    await WriteSafeGroupsAsync(safeDbFile, masterPassword, safeGroups);

                    break;
                case "3":
                    safeEntry.DeletedTimestamp = DateTime.UtcNow;
                    await WriteSafeGroupsAsync(safeDbFile, masterPassword, safeGroups);
                    return;
                case "x":
                    return;
                default:
                    logger.LogWarning("Unknown option, try again");
                    break;
            }
        }
    }

    private async Task<IEnumerable<SafeGroup>> ReadSafeGroupsAsync(string safeDbFile, string masterPassword)
    {
        await using FileStream fileStream = new(safeDbFile, FileMode.Open, FileAccess.Read);
        return await safeDbService.ReadAsync(masterPassword, fileStream);
    }

    private async Task WriteSafeGroupsAsync(string safeDbFile, string masterPassword, IEnumerable<SafeGroup> safeGroups)
    {
        await using FileStream fileStream = new(safeDbFile, FileMode.Create, FileAccess.Write);
        await safeDbService.WriteAsync(masterPassword, safeGroups, fileStream);
    }
}