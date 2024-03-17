# LapisItemEditor

This is an item editor for OTB files written in C# using Avalonia. The editor supports the version 11+ file format (appearances.dat & LZMA-compressed sprites).

Currently, the **Main functionality** of this application is to:

-   Create `items.otb` files.
-   Modify the `appearances.dat` file.

---

There is limited support for changing the default attributes of items, but this functionality is only **partially implented** and **contains bugs**.

If you would like to try to improve/implement changing of item attributes, good places to start are

```text
./LapisItemEditor/ViewModels/Main/ItemPropertiesViewModel.cs
./LapisItemEditor/Views/Main/ItemPropertiesView.axaml
```

There is partial work on support for the older file format (dat & spr), but it is not finished. It can be found under `./Backend/Tibia7`.

## Credits

Small parts of this repository are copied from the following projects:

-   [ottools/open-tibia](https://github.com/ottools/open-tibia)
-   [ottools/ItemEditor](https://github.com/ottools/ItemEditor)

Also, parts of this repository are inspired by code in the above repositories.

## Compiling

1. Open `./LapisItemEditor.sln` in Visual Studio.
2. In the solution explorer, right-click the LapisItemEditor project and select _Set as Startup Project_.
3. Build/Run.

## Usage

1. Start the application
2. Select assets folder
3. Select OTB file
4. (optional) Click "Create missing items"
5. (optional) Click "Import item names from file". Item name file must be of format:

```text
[100] void
[3457] a shovel
[35523] an exotic amulet
[...]
```

6. Click "Save items.otb"
7. (optional) Click "Save client data" to save modifications to the appearances (`appearances.dat`).

    **Note**: While `items.otb` stores some item-related data, many of the options of this tool (like `Take`, `Lying Object`, `Animate Always`, etc.) are only stored in `appearances.dat`.

    Unless you only use "Create missing items" and bump version data, you probably also want to use `Save client data`. Since this writes a new `appearances.dat`, you must of course use this new file in your client to see those changes (as opposed to the `items.otb` file which is a server-side file).

## Configuration

Major otb version & client versions can be specified in data/config.json:

```json
{
    "clientVersions": [
        { "name": "12.71", "version": 1271 },
        { "name": "12.72", "version": 1272 },
        { "name": "12.81", "version": 1281 }
    ],
    "majorOtbVersions": [1, 2, 3]
}
```

## Dependencies

-   [Avalonia](https://avaloniaui.net/)
    -   Avalonia.Desktop
    -   Avalonia.Diagnostics
    -   Avalonia.ReactiveUI
-   [System.Drawing.Common](https://www.nuget.org/packages/System.Drawing.Common/)
-   [SevenZip](https://www.nuget.org/packages/SevenZip/)
-   [Protobuf](https://www.nuget.org/packages/Google.Protobuf/)

## Instructions for generating proto files

1. clone [Protod3](https://github.com/giuinktse7/Protod3)
2. Get client.exe from <tibia_dir>/packages/Tibia/bin/client.exe
3. Run python3 src/protod.py client.exe
4. Add `syntax = "proto2";` as the first line in each `.proto` file.
5. To generate C# bindings, place `.proto` files in `./in`. Then:

    ```
    protoc -I=./in --csharp_out=./out ./in/appearances.proto
    protoc -I=./in --csharp_out=./out ./in/map.proto
    protoc -I=./in --csharp_out=./out ./in/shared.proto
    ```
