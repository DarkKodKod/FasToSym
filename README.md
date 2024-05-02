# FasToSym
Tool to convert .fas symbols files from FASM to No$Gba .SYM file format.

When developping homebrew for the Nintendo Game Boy Advance using FASM as assembler, it is important to debug with symbols. So in order to generate them, we can use:
```
./fasmarm.exe main.asm gba_game.gba -s [filename].fas
```

The file generated is not compatible with the current best emulator for debugging, No$gba, https://problemkaputt.de/gba.htm, the documentation for its sym file format is here: https://problemkaputt.de/gbahlp.htm#symbolicdebuginfo

Now with this tool it is possible to convert from .fas symbols file to the one accepted by No$gba like this:
```
.\FasToSym -i [filename].fas
```
This should generate in the same folder with the same name but with the extension .SYM.
