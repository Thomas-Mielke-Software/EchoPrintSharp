# EchoPrintSharp .NET Standard Library

Library that generates an EchoPrint audio fingerprint string from PCM audio data.

## How To Use

Add the Nuget package to your project 

    Install-Package EchoPrintSharp.Netstandard -Version 1.1.1

or the old PCL:

    Install-Package EchoPrintSharp
 
then add some code:

    using EchoPrintSharp;
    ...
    var pcmData = new Int16[441000]; // 40 seconds x 11,025Hz = 441,000 samples, for example
    ... // put your mono/11,025Hz audio data in pcmData here
    var echoPrint = new CodeGen();
    string epCodes = echoPrint.Generate(pcmData);

If your audio data is not in mono/11,025Hz/16bit format, use this call:

    ...
    string epCodes = Generate(pcmData, bitsPerSample, numberOfChannels, samplingrate)

## Build from source & test the .NET Standard Library

### Linux

Install monodevelop and all necessary mono stuff.

DEPRECATED: If you still want to build a PCL, checkout a0086813964858147184c8b35ea36174fc679ded. In order to be able to build PCLs on Linux, follow the instructions in this stack overflow answer: http://stackoverflow.com/questions/35245840/build-monodevelop-on-debian-jessie-using-mono-4-3-3

Make sure TestEchoPrint is selected as startup project and press the play button do build.

### Windows

Grab the latest Visual Studio Community Edition. 

Make sure TestEchoPrint is selected as startup project and press F5.
 
## Credits / Licenses

EchoPrintSharp: Copyright (c) 2018 Thomas Mielke, released under the MIT License (MIT)

Murmurhash: (c) Austin Appleby (Public Domain / MIT), C# port by Davy Landman

Wav sample Nine Inch Nails "999,999" from album "The Slip": (CC BY-NC-SA) Trent Reznor and Atticus Ross 
