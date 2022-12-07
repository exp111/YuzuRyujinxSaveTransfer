## Random links
https://switchbrew.org/wiki/Main_Page
https://switchbrew.org/wiki/Savegames
https://github.com/Ryujinx/Ryujinx/
https://github.com/yuzu-emu/yuzu/

## Ryujinx
Ryujinx folder defaults on Windows to `%appdata%/Ryujinx`
code: https://github.com/Ryujinx/Ryujinx/blob/master/Ryujinx/Ui/Widgets/GameTableContextMenu.cs#L134

save example: `bis\user\save\0000000000000004\0\`
* saved in `user` partition
* saveDataId is `4`  (as x16)
* commited save dir `0` (in contrast to working save dir, which is `1`; loads preferably commited dir)
* save files should then be the same as yuzu

how does one transform saveDataId to user+titleid/reverse?
=> extradata0?
imhex parser:
```c
#pragma endian little

#include <std/sys.pat>
enum SaveDataType : u8
{
System,
Account,
Bcat,
Device,
Temp,
Cache,
SystemBcat
};

enum SaveDataRank : u8
{
Primary,
Secondary,
};

// https://switchbrew.org/wiki/Filesystem_services#SaveDataAttribute
struct SaveDataAttribute
{
u64 ApplicationID; // 0 for SystemSaveData
u128 UserID;
u64 SystemSaveDataID; // 0 for SaveData
SaveDataType SaveDataType;
SaveDataRank SaveDataRank;
u16 SaveDataIndex;
u32 Padding;
u64 x28; // 0 for SystemSaveData/SaveData
u64 x30; // 0 for SystemSaveData/SaveData
u64 x38; // 0 for SystemSaveData/SaveData
};

// https://switchbrew.org/wiki/Savegames#Extra_data
struct ExtraData
{
SaveDataAttribute SaveDataAttribute;
u64 SaveOwnerID;
u64 Timestamp;
u32 Flags; //?
u32 Unused; //?
u64 UsableSaveDataSize;
u64 JournalSize;
u64 CommitID;
};

ExtraData data @ 0x0;
std::print("Application ID: {0:X}", data.SaveDataAttribute.ApplicationID);
std::print("User ID: {0:032X}", data.SaveDataAttribute.UserID);
std::print("SaveDataID: {:X}", data.SaveDataAttribute.SystemSaveDataID);
std::print("Yuzu Path: nand/user/save/{0:016X}/{1:08X}{2:08X}/{3:X}",
	0, // always 0 for non system saves, else SystemSaveDataID?
	data.SaveDataAttribute.UserID & (0xFFFFFFFFFFFFFFFF), //user_id[1], low bits
	data.SaveDataAttribute.UserID >> 64, // user_id[0], high bits
	data.SaveDataAttribute.ApplicationID); //title_id
std::print("Ryujinx Path: bis/user/save/{0:X}/{1:X}/", 
	data.SaveDataAttribute.SystemSaveDataID,
	0); // commited save dir
```
=> can build yuzu path out of that with `{0:x16}/{$UserID[1]:x8}{$UserID[0]:x8}/{$ApplicationID:x16}/`
=> if extradata file exist, can also find ryujinx folder (check application id, userID, saveDataType)
=> if  not, may be able to build one?

=> SaveDataID (which is used for the ryujinx save folder) is saved in ExtraData1


## yuzu
yuzu folder defaults on Windows to `%appdata%/yuzu` 
code: https://github.com/yuzu-emu/yuzu/blob/master/src/core/file_sys/savedata_factory.cpp#L137

save example: `\0000000000000000\00000000000000000000000000000000\01006F8002326000`
* saved in `user` partition
* `0000000000000000` is static for user/device save data (for system saves its save_id)
* user_id is `00000000000000000000000000000000`
* title_id is `01006F8002326000`

