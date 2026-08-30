# MHRise-Save-Reader

A C# console app to read Monster Hunter Rise save files and extract master rank monster kill counts.

## Usage

```bash
dotnet run -- <path-to-save-file> [--steamid <steamid64>] [--curve-index <index>]
```

If no save path is provided, the app prompts for one. If the save matches the Monster Hunter Rise Citrus encryption layout, the app also prompts for your SteamID64 when needed.

Typical Steam save location on Windows:

```text
C:\Program Files (x86)\Steam\userdata\<steam_id>\1446780\remote\
```

## Notes

- The reader accepts raw `data###Slot.bin` files directly.
- For Citrus-encrypted saves, decryption follows the community-documented REE/Citrus block layout used by tools like `kvasszn/ree-save-editor`.
- Monster names are based on the public Monster Hunter Rise modding wiki monster ID list.
- The parser reports all large-monster entries from the detected Master Rank hunt table so base-game monsters such as Great Wroggi still contribute toward Hunter's Gold Shield progress.
- The parser locates the monster statistics table heuristically so future title updates are less likely to break a single hard-coded offset.
