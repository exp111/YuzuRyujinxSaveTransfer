using System.CommandLine;
using System.Threading.Tasks;

internal class Program
{
    // Input
    public static DirectoryInfo YuzuPath;
    public static DirectoryInfo RyujinxPath;

    // The final save folders
    public static DirectoryInfo YuzuSavePath;
    public static DirectoryInfo RyujinxSavePath;


    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        var yuzuPath = new Option<DirectoryInfo?>( //TODO: is FileInfo the right class? we want a path/folder
            name: "--yuzu-path",
            description: "Yuzu's file path");
        var ryujinxPath = new Option<DirectoryInfo?>(
            name: "--ryujinx-path",
            description: "Ryujinx's file path");

        var rootCommand = new RootCommand("Transfers ");
        rootCommand.AddOption(yuzuPath);
        rootCommand.AddOption(ryujinxPath);

        rootCommand.SetHandler((yuzu, ryujinx) =>
        {
            //ReadFile(file!);
            Console.WriteLine($"yuzu: {yuzu}, ryujinx: {ryujinx}");
            //TODO: ask interactively for missing stuff
            YuzuPath = yuzu;
            RyujinxPath = ryujinx;

            //TODO: sanity checks on yuzu/ryujinx save folders
            //TODO: ask which yuzu user to use if there are multiple
            var users = GetYuzuUsers();
            dynamic selectedUser;
            if (users.Count > 1)
            {
                //TODO: ask
                foreach (var user in users)
                {
                    Console.WriteLine($"User {user.Name} ({user.SaveCount} saves)");
                }
                //FIXME: debug test thing
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
            //TODO: print all save gameids (plus option to fetch names)
            var yuzuSaves = ListYuzuSaves();
            Console.WriteLine("Yuzu:");
            foreach (var save in yuzuSaves)
            {
                Console.WriteLine($"- TitleID {save.TitleID}, Path: {save.Path}");
            }
            var ryujinxSaves = ListRyujinxSaves();
            Console.WriteLine("Ryujinx:");
            foreach (var save in ryujinxSaves)
            {
                Console.WriteLine($"- TitleID {save.TitleID}, Path: {save.Path}");
            }

            //TODO: ask/take from args what to transfer + overwrite? backups?
        },
            yuzuPath, ryujinxPath);

        return await rootCommand.InvokeAsync(args);
    }

    // Gets all yuzu users and their saves //TODO: change dynamic to class?
    public static List<dynamic> GetYuzuUsers()
    {
        // fixme: DirInfo->string->DirInfo => make it into DirInfo->DirInfo?
        var baseSavePath = new DirectoryInfo(
            Path.Combine(YuzuPath.FullName, 
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
    public static List<dynamic> ListYuzuSaves()
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

    public static List<dynamic> ListRyujinxSaves()
    {
        List<dynamic> saves = new();
        foreach (var dir in RyujinxSavePath.GetDirectories())
        {
            saves.Add(
                new
                {
                    Path = dir.Name,
                    TitleID = dir.Name //TODO: do vodoo magic to get the titleID
                });
        }
        return saves;
    }
}