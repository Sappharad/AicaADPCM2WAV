# AicaADPCM2WAV
Command Line tool to convert Dreamcast AICA ADPCM to WAV (or PCM)

Written in C# for .NET Core

This should be used instead of bero's adpcm2wav because that does not decode AICA correctly, and that wrong code has been used in other places like ffmpeg which is also wrong.

Usage:
aicaadpcm2wav inputFile outputFile \[-start=\] \[-length=\] \[-freq=\]

Example:
aicaadpcm2wav file.bin out.wav -start=0x1000 -freq=44100  
Converts data in file.bin starting at offset 0x1000 (4096) into a WAV file with a frequency of 44100hz.

Outputs 22050hz WAV by default. If outputFile extension is .pcm you get raw PCM instead and no WAV header will be added.

The following arguments are optional:  
  -start=### specify a start offset  
  -length=### specify a length to convert  
  -freq=### override the default frequency for WAV output  
  
