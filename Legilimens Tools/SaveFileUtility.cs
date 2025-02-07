using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

public class SaveFileUtility
{
    private const string CHAR_NAME_STR = "CharacterName\0";
    private const string CHAR_HOUSE_STR = "CharacterHouse\0";
    private const int CHAR_NAME_OFFSET = 43;
    private const int CHAR_HOUSE_OFFSET = 44;
    private const string MAGIC_HEADER = "GVAS";
    private const int CHOICE_COL_WIDTH = 10;
    private const int TABLE_WIDTH = 80;

    private static uint ReadU32(byte[] bytes, int index)
    {
        return (uint)((bytes[index + 3] << 24) |
                      (bytes[index + 2] << 16) |
                      (bytes[index + 1] << 8) |
                      bytes[index]);
    }

    private static bool ReadSaveInfo(string savePath, out string charName, out string charHouse)
    {
        charName = string.Empty;
        charHouse = string.Empty;

        if (!File.Exists(savePath)) return false;

        byte[] saveData = File.ReadAllBytes(savePath);
        string saveString = Encoding.UTF8.GetString(saveData);

        if (!saveString.StartsWith(MAGIC_HEADER)) return false;

        // Find character name
        int found = saveString.IndexOf(CHAR_NAME_STR);
        if (found != -1 && found + CHAR_NAME_OFFSET < saveString.Length)
        {
            int i = 0;
            while (true)
            {
                char c = saveString[found + CHAR_NAME_OFFSET + i++];
                if (c == '\0') break;
                charName += c;
            }
        }

        // Find character house
        found = saveString.IndexOf(CHAR_HOUSE_STR);
        if (found != -1 && found + CHAR_HOUSE_OFFSET < saveString.Length)
        {
            int i = 0;
            while (true)
            {
                char c = saveString[found + CHAR_HOUSE_OFFSET + i++];
                if (c == '\0') break;
                charHouse += c;
            }
        }

        return true;
    }

    private static bool IsValid(string savePath)
    {
        string fileName = Path.GetFileName(savePath);
        if (!fileName.EndsWith(".sav") || !fileName.StartsWith("HL-"))
            return false;

        if (!File.Exists(savePath)) return false;

        using (var fs = new FileStream(savePath, FileMode.Open, FileAccess.Read))
        {
            byte[] buffer = new byte[4];
            fs.Read(buffer, 0, 4);
            string header = Encoding.ASCII.GetString(buffer);
            return header == MAGIC_HEADER;
        }
    }

    private class SaveList
    {
        public List<(string Path, DateTime Time)> Paths = new List<(string Path, DateTime Time)>();
        public string CharName = string.Empty;
        public string CharHouse = string.Empty;
    }

    private static List<SaveList> GetSaveList()
    {
        var result = new List<SaveList>();

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string usersPath = Path.Combine(localAppData, "HogwartsLegacy", "Saved", "SaveGames");

        if (!Directory.Exists(usersPath))
            usersPath = Path.Combine(localAppData, "Hogwarts Legacy", "Saved", "SaveGames");

        if (!Directory.Exists(usersPath)) return result;

        foreach (var userFolder in Directory.GetDirectories(usersPath))
        {
            var savesByChar = new Dictionary<string, List<string>>();

            foreach (var saveFile in Directory.GetFiles(userFolder))
            {
                if (!IsValid(saveFile)) continue;

                string charIndex = Path.GetFileName(saveFile).Substring(3, 2); // "HL-01-11.sav" -> "01"
                if (!savesByChar.ContainsKey(charIndex))
                    savesByChar[charIndex] = new List<string>();

                savesByChar[charIndex].Add(saveFile);
            }

            foreach (var entry in savesByChar)
            {
                var saves = new SaveList();
                foreach (var savePath in entry.Value)
                {
                    if (string.IsNullOrEmpty(saves.CharName) || string.IsNullOrEmpty(saves.CharHouse))
                    {
                        ReadSaveInfo(savePath, out saves.CharName, out saves.CharHouse);
                    }
                    saves.Paths.Add((savePath, File.GetLastWriteTime(savePath)));
                }

                saves.Paths = saves.Paths.OrderByDescending(p => p.Time).ToList();
                if (saves.CharName == null)
                    saves.CharName = "Unknown name";
                if (saves.CharHouse == null)
                    saves.CharHouse = "Unknown house";

                if (saves.Paths.Count > 0)
                    result.Add(saves);
            }
        }

        return result;
    }

    private static string GetSaveType(string savePath)
    {
        string filename = Path.GetFileName(savePath);
        string type = filename.Substring(6, 1) == "0" ? "Manual Save #" : "Autosave #";
        return type + (int.Parse(filename.Substring(7, 1)) + 1);
    }

    private static string TimeToString(DateTime time)
    {
        return time.ToString("yyyy-MM-dd HH:mm");
    }

    private static int GetChoice(int maxVal, string prompt)
    {
        while (true)
        {
            Console.WriteLine(prompt);
            if (int.TryParse(Console.ReadLine(), out int choice) && choice >= 0 && choice <= maxVal)
            {
                return choice;
            }
            Console.WriteLine($"Invalid choice, must be between 0 and {maxVal}, inclusive.");
        }
    }

    public static string GetSavePath()
    {
        Console.WriteLine("0: Automatically detect saves");
        Console.WriteLine("1: Manual input");
        Console.WriteLine("2: Most recent save");

        int choice = GetChoice(2, "How would you like to find your save path?");
        if (choice == 1)
        {
            Console.Write("Path to .sav file: ");
            return Console.ReadLine()?.Trim('"') ?? string.Empty;
        }
        else if (choice == 2)
        {
            // Define the path to the save folder
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string saveFolder = Path.Combine(localAppData, "HogwartsLegacy", "Saved", "SaveGames");

            if (!Directory.Exists(saveFolder))
            {
                saveFolder = Path.Combine(localAppData, "Hogwarts Legacy", "Saved", "SaveGames");
                if (!Directory.Exists(saveFolder))
                {
                    Console.WriteLine("Save folder not found.");
                    return string.Empty;
                }
            }

            // Initialize variables to track the most recent save
            FileInfo mostRecentSave = null;

            // Recursively search through all subfolders
            foreach (string userFolder in Directory.GetDirectories(saveFolder))
            {
                foreach (string saveFile in Directory.GetFiles(userFolder, "*.sav"))
                {
                    FileInfo fileInfo = new FileInfo(saveFile);

                    // Update if this file is the most recent
                    if (IsValid(saveFile) && (mostRecentSave == null || fileInfo.LastWriteTime > mostRecentSave.LastWriteTime))
                    {
                        mostRecentSave = fileInfo;
                    }
                }
            }

            if (mostRecentSave != null)
            {
                string charName, charHouse;
                
                ReadSaveInfo(mostRecentSave.FullName, out charName, out charHouse);
                Console.Write($"Most recent save found: {charName} (");
                WriteHouse(charHouse);
                Console.WriteLine(")");
                return mostRecentSave.FullName;
            }
            else
            {
                Console.WriteLine("No save files found.");
                return string.Empty;
            }
        }

        var saves = GetSaveList();
        if (saves.Count == 0)
        {
            Console.WriteLine("No saves detected. Please input the path manually.");
            Console.Write("Path to .sav file: ");
            return Console.ReadLine()?.Trim('"') ?? string.Empty;
        }

        foreach (var saveList in saves.Select((value, index) => new { index, value }))
        {
            Console.Write($"{saveList.index}: {saveList.value.CharName} (");
            WriteHouse(saveList.value.CharHouse);
            Console.WriteLine(")");
        }
        Console.WriteLine($"{saves.Count}: Go back");

        choice = GetChoice(saves.Count, "Select a character:");

        if (choice == saves.Count) return string.Empty;

        var selectedSaves = saves[choice];
        foreach (var save in selectedSaves.Paths.Select((value, index) => new { index, value }))
        {
            Console.WriteLine($"{save.index}: {save.value.Path} - {TimeToString(save.value.Time)}");
        }
        Console.WriteLine($"{selectedSaves.Paths.Count}: Go back");

        choice = GetChoice(selectedSaves.Paths.Count, $"Select a save for {selectedSaves.CharName} ({selectedSaves.CharHouse}):");

        return choice == selectedSaves.Paths.Count ? string.Empty : selectedSaves.Paths[choice].Path;
    }

    public static void WriteHouse(string house)
    {
        if (house.StartsWith("G"))
            Console.ForegroundColor = ConsoleColor.Red;
        else if (house.StartsWith("S"))
            Console.ForegroundColor = ConsoleColor.Green;
        else if (house.StartsWith("Hu"))
            Console.ForegroundColor = ConsoleColor.Yellow;
        else if (house.StartsWith("R"))
            Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write(house);
        Console.ResetColor();
        return;
    }
}
