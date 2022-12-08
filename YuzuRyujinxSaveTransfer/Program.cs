using System.CommandLine;
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

        var rootCommand = new RootCommand("Transfers ");
        rootCommand.AddOption(yuzuPath);
        rootCommand.AddOption(ryujinxPath);

        //TODO: subcommands list/transfer?

        rootCommand.SetHandler((yuzu, ryujinx, yUser) =>
        {
            //ReadFile(file!);
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

            //TODO: ask/take from args what to transfer + overwrite? backups?
        },
            yuzuPath, ryujinxPath, yuzuUser); //TODO: use binders for yuzu parser?

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
}