# Clancy Clock

Windows Forms clock that picks an image based on the current minute and transitions between minutes with layered generative effects.

## Image naming

Place your images in either:

- `Images/` next to the project or published executable
- or directly beside the executable

Supported file extensions:

- `.png`
- `.jpg`
- `.jpeg`
- `.bmp`
- `.gif`
- `.webp`

Supported filename styles include:

- `hhmm` -> `0237.png`
- `hh_mm` -> `02_37.jpg`
- `hh-mm` -> `02-37.webp`
- `hh:mm` -> `02:37.png`
- `hh.mm` -> `02.37.png`
- `hhhmm` -> `02h37.png`
- `HHmm` -> `1437.png`
- `HH_mm` -> `14_37.jpg`
- `HH-mm` -> `14-37.webp`
- `HH:mm` -> `14:37.png`
- `HH.mm` -> `14.37.png`
- `HHhmm` -> `14h37.png`
- `mm` -> `37.png`
- `m` -> `7.png`
- `minute-mm` -> `minute-37.png`

If you have a 12-hour image set, name files with `hhmm` so 2:24 PM maps to `0224`.

If you only have one image per minute regardless of hour, naming the files `00` through `59` is enough.
Four-digit filenames such as `0138.jpg` are treated only as hour+minute images, not as generic minute `38` fallbacks.

## Running locally on Windows

This project targets `.NET 8` and Windows Forms:

```powershell
dotnet build
dotnet run
```

## Publishing a Windows executable

For a self-contained single-file Windows build:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

The executable will be produced under:

`bin\Release\net8.0-windows\win-x64\publish\`

Copy your `Images` folder beside the published executable if you want the app to find the artwork after publishing.

## Controls

- `F5` refresh image discovery
- `Esc` close the app
