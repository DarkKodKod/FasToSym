# FasToSym
Tool to convert .fas symbols files from FASM (Flat assembler) to any type of Symbol format.

> ⚠️Now it is only implemented for the No$Gba .SYM file format. But the idea is to have an easy way to from a commmand line to convert the FAS file to anything.

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

## 2. GBA symbols for No$Gba.

When developping homebrew for the Nintendo Game Boy Advance using FASMARM, https://arm.flatassembler.net/, it is important to debug with symbols. So in order to generate them, we can use:
```
./fasmarm.exe main.asm gba_game.gba -s mygame.fas
```
The file generated is not compatible with the current best emulator for debugging, No$gba, https://problemkaputt.de/gba.htm, the documentation for its sym file format is here: https://problemkaputt.de/gbahlp.htm#symbolicdebuginfo










