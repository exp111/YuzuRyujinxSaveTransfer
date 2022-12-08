using System.CommandLine;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

internal class Program
{
    //TODO: move input args into local vars
    // Input
    public static DirectoryInfo YuzuPath;
    public static DirectoryInfo RyujinxPath;

    //TODO: give them as args
    // The final save folders
    public static DirectoryInfo YuzuSavePath;
    public static DirectoryInfo RyujinxSavePath;


    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        var yuzuPath = new Option<DirectoryInfo?>( //TODO: alias
            name: "--yuzu-path",
            description: "Yuzu's file path");
        var yuzuUser = new Option<string?>(
            name: "--yuzu-user",
            description: "Preferred user for yuzu");
        var ryujinxPath = new Option<DirectoryInfo?>(
            name: "--ryujinx-path",
            description: "Ryujinx's file path");
        var transferTitles = new Option<string[]?>(
            name: "--titles",
            description: "Title IDs that should be transfered")
        {
            AllowMultipleArgumentsPerToken = true
        };
        var transferAll = new Option<bool>(
            name: "--all",
            getDefaultValue: () => false,
            description: "Transfers all saves.");
        var source = new Option<string?>(
            name: "--source",
            description: "Source of the saves to be transfered").FromAmong("yuzu", "ryujinx");

        var rootCommand = new RootCommand("Transfers ");
        rootCommand.AddOption(yuzuPath);
        rootCommand.AddOption(ryujinxPath);
        rootCommand.AddOption(yuzuUser);
        rootCommand.AddOption(transferTitles);
        rootCommand.AddOption(transferAll);
        rootCommand.AddOption(source);

        //TODO: subcommands list/transfer?

        rootCommand.SetHandler((yuzu, ryujinx, yUser, titles, all, source) =>
        {
            Console.WriteLine($"yuzu: {yuzu}, ryujinx: {ryujinx}");
            //TODO: ask interactively for missing stuff
            YuzuPath = yuzu;
            RyujinxPath = ryujinx;

            //TODO: sanity checks on yuzu/ryujinx save folders
            //TODO: ask which yuzu user to use if there are multiple
            var users = GetYuzuUsers(yuzu.FullName);
            dynamic selectedUser;
            if (users.Count > 1)
            {
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
                //FIXME: remove debug test thing
                selectedUser = users[1];

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
            YuzuSavePath = selectedUser.FullPath;
            RyujinxSavePath = new DirectoryInfo(Path.Combine(RyujinxPath.FullName, "bis/user/save"));
            //TODO: do the main stuff
            // print all save gameids //TODO: plus option to fetch names
            var yuzuSaves = ListYuzuSaves(YuzuSavePath);
            Console.WriteLine("Yuzu:");
            foreach (var save in yuzuSaves)
            {
                Console.WriteLine($"- TitleID {save.TitleID}, Path: {save.Path}");
            }
            var ryujinxSaves = ListRyujinxSaves(RyujinxSavePath);
            Console.WriteLine("Ryujinx:");
            foreach (var save in ryujinxSaves)
            {
                Console.WriteLine($"- TitleID {save.TitleID}, Path: {save.Path}");
            }

            //TODO: ask/take from args what to transfer + delete+overwrite? backups?
            if (string.IsNullOrEmpty(source))
            {
                Console.WriteLine("No source given!");
                return;
            }
            var sourceSaves = source == "yuzu" ? yuzuSaves : ryujinxSaves;
            var destSaves = source == "yuzu" ? ryujinxSaves : yuzuSaves;
            if (all)
            {
                titles = sourceSaves.Select(s => (string)s.TitleID).ToArray();
            }
            Console.WriteLine("Titles to transfer:");
            Console.WriteLine(string.Join(",", titles));
            //TODO: each one: check if exists on both ends + transfer
            foreach (var title in titles)
            {
                var srcSave = sourceSaves.Find(s => s.TitleID == title);
                if (srcSave == null)
                {
                    Console.WriteLine($"Title {title} not found in source {source}");
                    return;
                }
                var dstSave = destSaves.Find(s => s.TitleID == title);
                if (dstSave == null)
                {
                    Console.WriteLine($"Title {title} not found in destination {(source == "yuzu" ? "Ryujinx" : "Yuzu")}");
                    return;
                }

                Console.WriteLine($"Transfering {title} from {srcSave.Path} to {dstSave.Path}");
                var sourcePath = source == "yuzu" ? YuzuSavePath : RyujinxSavePath;
                var destPath = source == "yuzu" ? RyujinxSavePath : YuzuSavePath;
                TransferSave(sourcePath, destPath, srcSave.Path, dstSave.Path, source); //TODO: transfer
            }
        },
        yuzuPath, ryujinxPath, yuzuUser, transferTitles, transferAll, source); //TODO: use binders for yuzu parser?

        return await rootCommand.InvokeAsync(args);
    }

    //TODO: move those into a shared lib so we can call it from cli and gui
    // Gets all yuzu users and their saves //TODO: change dynamic to class?
    public static List<dynamic> GetYuzuUsers(string savePath)
    {
        // fixme: DirInfo->string->DirInfo => make it into DirInfo->DirInfo?
        var baseSavePath = new DirectoryInfo(
            Path.Combine(savePath,
                "nand/user/save/0000000000000000")); // traverse into the base user save folder
        var userDirs = baseSavePath.GetDirectories();
        List<dynamic> users = new(userDirs.Length);
        foreach (var userDir in userDirs)
        {
            // Add each user save dir and the number of saves inside
            users.Add(new
            {
                Name = userDir.Name,
                SaveCount = userDir.GetDirectories().Length,
                FullPath = new DirectoryInfo(Path.Combine(baseSavePath.FullName, userDir.Name))
            });
        }
        return users;
    }

    //TODO: change to custom Game class or smth?
    public static List<dynamic> ListYuzuSaves(DirectoryInfo YuzuSavePath)
    {
        List<dynamic> saves = new();
        foreach (var dir in YuzuSavePath.GetDirectories())
        {
            saves.Add(
                new
                {
                    Path = dir.Name,
                    TitleID = dir.Name
                });
        }
        return saves;
    }

    public static List<dynamic> ListRyujinxSaves(DirectoryInfo RyujinxSavePath)
    {
        List<dynamic> saves = new();
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
                new
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
    public static bool TransferSave(DirectoryInfo srcSavePath, DirectoryInfo dstSavePath, string srcDir, string dstDir, string source)
    {
        // build save folders paths
        var srcPath = Path.Combine(srcSavePath.FullName, srcDir);
        var dstPath = Path.Combine(dstSavePath.FullName, dstDir);
        // ryujinx has the save data in a subdir called 0 (the commited save dir), so add that to the yuzuPath
        if (source == "yuzu")
            dstPath = Path.Combine(dstPath, "0");
        else
            srcPath = Path.Combine(srcPath, "0");

        //Console.WriteLine($"source: {srcPath}");
        Process.Start("explorer.exe", srcPath);
        //Console.WriteLine($"dst: {dstPath}");
        Process.Start("explorer.exe", dstPath);
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
}