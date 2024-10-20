# FasToSym
Command line tool to convert .fas symbols files from FASM (Flat assembler) to any type of Symbol format.

> ⚠️If you are here because you want to understand the symmbol file, FAS, from FASM, I think here you will find a good example of how to read the data and with this tool it should be possible to extend it to convert the FAS format file to anything.

## 1. Usage

Now with this tool it is possible to convert from .fas symbols file format.
```
.\FasToSym -i [filename].fas -t [outputType]
```
To for example the one accepted by No$gba like this:
```
eg: .\FasToSym -i mygame.fas -t nocashgba
```
This should generate in the same folder with the same name but with the extension .SYM.

## 2. Output files

When developping homebrew for the Nintendo Game Boy Advance using FASMARM, https://arm.flatassembler.net/, it is important to debug with symbols. So in order to generate them:

```
./fasmarm.exe main.asm gba_game.gba -s mygame.fas
```
The fas symbol file is not compatible with any of the GBA amulators so in order to convert it to for example No$Gba or Mesen (these two emulators have good debugging tool).

### 2.1 GBA symbols for No$Gba.

https://problemkaputt.de/gba.htm

To generate for this emulator use the the following command line:
```
.\FasToSym -i mygame.fas -t nocashgba
```
The documentation for its sym file format is here: https://problemkaputt.de/gbahlp.htm#symbolicdebuginfo

### 2.1 GBA symbols for Mesen2.

https://www.mesen.ca/

To generate for this emulator use the the following command line:

```
.\FasToSym -i mygame.fas -t mesen
```
The documentation for its sym file format is here: https://www.mesen.ca/docs/debugging/debuggerintegration.html#mesen-label-files-mlb







