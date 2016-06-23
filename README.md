# img2ico

Converts one large image into a Windows icon file with various sizes.

## Get it

Get the [latest release here](https://github.com/btrzcinski/img2ico/releases).

## Instructions

Given an input PNG file `sample.png`, create an `output.ico` with:

`C:\..\Img2Ico> Img2Ico.exe sample.png output.ico`

## What It Does

Takes in a 256x256 32-bit ARGB PNG and outputs a Windows Icon file (ICO) with the following formats:

* 256x256 32-bit PNG (original)
* 16x16 32-bit PNG (downscaled)
* 24x24 32-bit PNG (downscaled)
* 32x32 32-bit PNG (downscaled)
* 48x48 32-bit PNG (downscaled)

## Requirements

Viewers of the icon file will need Windows XP or better, since all icon resources are stored as PNGs.
You will need the [.NET Framework 4.5.2](https://www.microsoft.com/en-us/download/details.aspx?id=42643) to run Img2Ico.

*Developers*: Build the solution with [Visual Studio 2015](https://www.visualstudio.com).
