# OrphanPatchMover
Checks the %SYSTEMROOT%\Installer folder for patch files that are not in use anymore. Offers to delete them or move them to a different location.

These files are not used anymore but can take up a lot of space on your system drive.
On my machine the whole directory had 17 GB after cleaning up everything it had 7.66 GB.

Should work on Windows Vista and upwards, I tested it with Windows 8.1 Pro.

Required libraries:
https://www.nuget.org/packages/WindowsAPICodePack-Core/
https://www.nuget.org/packages/WindowsAPICodePack-Shell/

Screenshot:
![Screenshot](https://raw.githubusercontent.com/g4rb4g3/OrphanPatchMover/master/screenshot.JPG)

[Download](https://raw.githubusercontent.com/g4rb4g3/OrphanPatchMover/master/OrphanPatchMover.zip "Download")

How to use:
1. Start it
2. Push scan
3. Check files (header checkbox checks all items)
4. Delete or move them!