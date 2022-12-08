using System.CommandLine;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        var yuzuPath = new Option<DirectoryInfo?>( //TODO: alias
            name: "--yuzu-path",
            getDefaultValue: () =>
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "yuzu"));
                else // ~/.local/share/
                    return new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "yuzu"));
            },
            description: "Yuzu's file path");
        var yuzuUser = new Option<string?>(
            name: "--yuzu-user",
            description: "Preferred user for yuzu")
        {
            ArgumentHelpName = "user-id"
        };
        var ryujinxPath = new Option<DirectoryInfo?>(
            name: "--ryujinx-path",
            getDefaultValue: () =>
            {
                return new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ryujinx"));
            },
            description: "Ryujinx's file path");
        var transferTitles = new Option<string[]?>(
            name: "--titles",
            description: "Title IDs that should be transfered")
        {
            AllowMultipleArgumentsPerToken = true,
            ArgumentHelpName = "title-ids"
        };
        var transferAll = new Option<bool>(
            name: "--all",
            getDefaultValue: () => false,
            description: "Transfers all saves.");
        var gameNames = new Option<bool>( //TODO: add force refresh?
            name: "--game-names",
            getDefaultValue: () => true,
            description: "Tries to fetch the game names for the title ids to make those more readable.");
        var sourceOption = new Option<string?>(
            name: "--source",
            description: "Source of the saves to be transfered")
            .FromAmong("yuzu", "ryujinx");

        var rootCommand = new RootCommand("Transfers Yuzu and Ryujinx saves between each other");
        rootCommand.AddOption(yuzuPath);
        rootCommand.AddOption(ryujinxPath);
        rootCommand.AddOption(yuzuUser);
        rootCommand.AddOption(transferTitles);
        rootCommand.AddOption(transferAll);
        rootCommand.AddOption(sourceOption);
        rootCommand.AddOption(gameNames);

        //TODO: subcommands list/transfer?

        rootCommand.SetHandler((yuzu, ryujinx, yUser, titles, all, source, shouldShowGame) =>
        {
            Console.WriteLine($"Yuzu Folder: {yuzu}\nRyujinx Folder: {ryujinx}");
            //TODO: ask interactively for missing stuff

            // sanity checks on yuzu/ryujinx save folders
            if (!Directory.Exists(yuzu.FullName))
            {
                Console.WriteLine("Yuzu path doesn't exist!");
                return;
            }
            if (!Directory.Exists(ryujinx.FullName))
            {
                Console.WriteLine("RyuJinx path doesn't exist!");
                return;
            }
            Console.WriteLine();

            //TODO: ask which yuzu user to use if there are multiple
            // List yuzu users and specify which one to use
            var users = GetYuzuUsers(yuzu.FullName);
            User? selectedUser = null;
            if (users.Count > 1)
            {
                Console.WriteLine("Yuzu Saves:");
                foreach (var user in users)
                {
                    Console.WriteLine($"User {user.Name} ({user.SaveCount} saves)");
                }

                if (!string.IsNullOrEmpty(yUser))
                {
                    var user = users.Find(u => u.Name.ToLower() == yUser.ToLower());
                    if (user != null)
                        selectedUser = user;
                }
                //TODO: ask interactively
                if (selectedUser == null) // TODO: remove this when we ask
                {
                    Console.WriteLine($"Please specify a valid yuzu user (--{yuzuUser.Name} <{yuzuUser.ArgumentHelpName}>)");
                    return;
                }
            }
            else if (users.Count == 1)
            {
                selectedUser = users[0];
            }
            else
            {
                Console.WriteLine("No Yuzu Users found");
                return; //TODO: still should be able to list ryu stuff?
            }
            Console.WriteLine($"Using yuzu user {selectedUser.Name} with {selectedUser.SaveCount} saves.");
            Console.WriteLine();

            // The final save paths which contain the folder with saves
            var yuzuSavePath = selectedUser.FullPath;
            var ryujinxSavePath = new DirectoryInfo(Path.Combine(ryujinx.FullName, "bis/user/save"));

            // print all save gameids 
            Dictionary<string, string> titleDB = null;
            if (shouldShowGame)
                titleDB = GetTitleDB(); // only when option to fetch names is enabled
            List<Save> yuzuSaves = ListYuzuSaves(yuzuSavePath);
            var printSaves = (List<Save> saves) =>
            {
                foreach (var save in saves)
                {
                    string gameInfo = "";
                    if (shouldShowGame)
                    {
                        var gameName = "<unknown>";
                        titleDB.TryGetValue(save.TitleID, out gameName);
                        gameInfo = $"({gameName})";
                    }

                    Console.WriteLine($"- TitleID {save.TitleID} {gameInfo}, Path: {save.Path}");
                }
            };
            Console.WriteLine("Yuzu:");
            printSaves(yuzuSaves);
            var ryujinxSaves = ListRyujinxSaves(ryujinxSavePath);
            Console.WriteLine("Ryujinx:");
            printSaves(ryujinxSaves);
            Console.WriteLine();

            // Transfer setup
            // TODO: ask interactively what to transfer if missing
            if (string.IsNullOrEmpty(source))
            {
                Console.WriteLine($"No source given! (--{sourceOption.Name} <{string.Join("|", sourceOption.GetCompletions())}>)");
                return;
            }
            var isYuzuSource = source == "yuzu";
            var sourceSaves = isYuzuSource ? yuzuSaves : ryujinxSaves;
            var destSaves = isYuzuSource ? ryujinxSaves : yuzuSaves;
            if (all)
            {
                titles = sourceSaves.Select(s => s.TitleID).ToArray();
            }
            if (titles == null || titles.Length == 0)
                return;

            Console.WriteLine("Titles to transfer:");
            Console.WriteLine(string.Join(",", titles));
            Console.WriteLine();

            // Transfer
            //for each one: check if exists on both ends + transfer
            foreach (var title in titles)
            {
                string gameInfo = "";
                if (shouldShowGame)
                {
                    var gameName = "<unknown>";
                    titleDB.TryGetValue(title, out gameName);
                    gameInfo = $"({gameName}) ";
                }

                var srcSave = sourceSaves.Find(s => s.TitleID == title);
                if (srcSave == null)
                {
                    Console.WriteLine($"Title {title} {gameInfo}not found in source {source}");
                    continue;
                }
                var dstSave = destSaves.Find(s => s.TitleID == title);
                if (dstSave == null)
                {
                    Console.WriteLine($"Title {title} {gameInfo}not found in destination {(isYuzuSource ? "Ryujinx" : "Yuzu")}");
                    continue;
                }

                Console.WriteLine($"Transfering {title} {gameInfo} from {srcSave.Path} to {dstSave.Path}");
                var sourcePath = isYuzuSource ? yuzuSavePath : ryujinxSavePath;
                var destPath = isYuzuSource ? ryujinxSavePath : yuzuSavePath;
                if (!TransferSave(sourcePath, destPath, srcSave.Path, dstSave.Path, isYuzuSource))
                    Console.WriteLine("Failed!");
            }
        },
        yuzuPath, ryujinxPath, yuzuUser, transferTitles, transferAll, sourceOption, gameNames); //TODO: use binders for yuzu parser?

        return await rootCommand.InvokeAsync(args);
    }

    public class User
    {
        public string Name;
        public int SaveCount;
        public DirectoryInfo FullPath;
    }
    //TODO: move those into a shared lib so we can call it from cli and gui
    // Gets all yuzu users and their saves
    public static List<User> GetYuzuUsers(string savePath)
    {
        // fixme: DirInfo->string->DirInfo => make it into DirInfo->DirInfo?
        var baseSavePath = new DirectoryInfo(
            Path.Combine(savePath,
                "nand/user/save/0000000000000000")); // traverse into the base user save folder
        var userDirs = baseSavePath.GetDirectories();
        List<User> users = new(userDirs.Length);
        foreach (var userDir in userDirs)
        {
            // Add each user save dir and the number of saves inside
            users.Add(new User
            {
                Name = userDir.Name,
                SaveCount = userDir.GetDirectories().Length,
                FullPath = new DirectoryInfo(Path.Combine(baseSavePath.FullName, userDir.Name))
            });
        }
        return users;
    }

    public class Save
    {
        public string Path;
        public string TitleID;
    }
    // List all yuzu saves and their titleids in the given folder
    public static List<Save> ListYuzuSaves(DirectoryInfo YuzuSavePath)
    {
        List<Save> saves = new();
        foreach (var dir in YuzuSavePath.GetDirectories())
        {
            saves.Add(
                new Save
                {
                    Path = dir.Name,
                    TitleID = dir.Name
                });
        }
        return saves;
    }
    // List all ryujinx saves and their titleids in the given folder
    public static List<Save> ListRyujinxSaves(DirectoryInfo RyujinxSavePath)
    {
        List<Save> saves = new();
        foreach (var dir in RyujinxSavePath.GetDirectories())
        {
            // Read titleid from extradata
            var extraData = dir.GetFiles("ExtraData0", SearchOption.TopDirectoryOnly);
            var titleID = "<Missing ExtraData>";
            if (extraData.Length > 0)
            {
                titleID = GetApplicationID(extraData[0]);
            }

            saves.Add(
                new Save
                {
                    Path = dir.Name,
                    TitleID = titleID
                });
        }
        return saves;
    }

    // Reads the application id from a ExtraData file
    public static string GetApplicationID(FileInfo extraData)
    {
        // the applicationID are the first 8 bytes (u64) as hex
        var applicationID = string.Empty;
        try
        {
            using (var file = extraData.OpenRead())
            {
                var buffer = new byte[8];
                if (file.Read(buffer, 0, 8) != 8)
                    throw new Exception("ExtraData too small");

                var app = BitConverter.ToUInt64(buffer, 0); // read as u64
                applicationID = string.Format("{0:X16}", app); // convert to hex
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Failed reading ExtraData: {e}");
            return applicationID;
        }
        return applicationID;
    }

    // Transfers the save from $srcSavePath/srcDir to $dstSavePath/dstDir
    public static bool TransferSave(DirectoryInfo srcSavePath, DirectoryInfo dstSavePath, string srcDir, string dstDir, bool isYuzuSource)
    {
        // build save folders paths
        var srcPath = Path.Combine(srcSavePath.FullName, srcDir);
        var dstPath = Path.Combine(dstSavePath.FullName, dstDir);
        // ryujinx has the save data in a subdir called 0 (the commited save dir), so add that to the yuzuPath
        if (isYuzuSource)
            dstPath = Path.Combine(dstPath, "0"); //dst == ryujinx
        else
            srcPath = Path.Combine(srcPath, "0"); //src == ryujinx

        //Console.WriteLine($"source: {srcPath}");
        //Process.Start("explorer.exe", srcPath);
        //Console.WriteLine($"dst: {dstPath}");
        //Process.Start("explorer.exe", dstPath);
        // sanity checks
        if (!Path.Exists(srcPath) || !Path.Exists(dstPath))
            return false;

        // backup
        var origPath = new DirectoryInfo(dstPath);
        var backupPath = Path.Combine(origPath.Parent.FullName, $"{dstDir}_bak");
        // delete old backup if it exists
        if (Path.Exists(backupPath))
            Directory.Delete(backupPath, true);
        Directory.Move(dstPath, backupPath);
        // copy the dir over
        CopyDirectory(srcPath, dstPath, true);
        return true;
    }


    // Copies a directory
    // https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
    static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
    {
        // Get information about the source directory
        var dir = new DirectoryInfo(sourceDir);

        // Check if the source directory exists
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        // Cache directories before we start copying
        DirectoryInfo[] dirs = dir.GetDirectories();

        // Create the destination directory
        Directory.CreateDirectory(destinationDir);

        // Get the files in the source directory and copy to the destination directory
        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        // If recursive and copying subdirectories, recursively call this method
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true);
            }
        }
    }

    public static string TitleDBCache = "titles.json";
    public static Dictionary<string, string> GetTitleDB()
    {
        // check cache
        if (File.Exists(TitleDBCache))
        {
            using (var file = File.OpenRead(TitleDBCache))
                return JsonSerializer.Deserialize<Dictionary<string, string>>(file); //TODO: error handling
        }
        
        // else update
        return UpdateTitleDB();
    }


    public class TitleDB
    {
        [JsonPropertyName("data")]
        public List<Title> Data { get; set; }
    }
    public class Title
    {
        [JsonPropertyName("id")]
        public string ID { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        // there is other stuff, but we dont want that
    }

    // Fetches the titledb from the internet and saves that to cache + returns it
    public static Dictionary<string, string> UpdateTitleDB()
    {
        // The name of the game is in a <a> tag, so we need to remove that (length should be static as all ids are the same length)
        int preLength = "<a href=\"/Title/010000100FB62000\">".Length;
        int postLength = "</a>".Length;

        // Fetch the new db
        var client = new HttpClient();
        //TODO: error handling
        TitleDB data = null;
        try
        {
            data = client.GetFromJsonAsync<TitleDB>("https://tinfoil.media/Title/ApiJson/").Result;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed during updating title db: {e}");
        }
        if (data == null)
            return null;
        var result = new Dictionary<string, string>();
        foreach (var title in data.Data)
        {
            var name = HttpUtility.HtmlDecode(title.Name);
            result[title.ID] = name.Substring(preLength, name.Length - preLength - postLength);
        }

        // Serialize and save it to disk
        var json = JsonSerializer.Serialize(result);
        File.WriteAllText(TitleDBCache, json); //TODO: error handling
        return result;
    }
}