using System;
using EchoPrintSharp;
using System.Reflection;
using System.IO;
using NAudio.Wave;

namespace TestEchoPrintSharp
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			if (args.Length == 0)
			{
				var assembly = Assembly.GetExecutingAssembly();
				var resourceName = "TestEchoPrintSharp.Resources.NIN-999999-11025.wav";

				using (Stream stream = assembly.GetManifestResourceStream(resourceName))
				using (BinaryReader reader = new BinaryReader(stream, System.Text.Encoding.ASCII))
				{
					string chunkId = new string(reader.ReadChars(4));
					UInt32 chunkSize = reader.ReadUInt32();
					string riffType = new string(reader.ReadChars(4));
					string fmtId = new string(reader.ReadChars(4));
					UInt32 fmtSize = reader.ReadUInt32();
					UInt16 formatTag = reader.ReadUInt16();
					UInt16 channels = reader.ReadUInt16();
					UInt32 samplesPerSec = reader.ReadUInt32();
					UInt32 avgBytesPerSec = reader.ReadUInt32();
					UInt16 blockAlign = reader.ReadUInt16();
					UInt16 bitsPerSample = reader.ReadUInt16();
					string dataID = new string(reader.ReadChars(4));
					UInt32 dataSize = reader.ReadUInt32();

					if (chunkId != "RIFF" || riffType != "WAVE" || fmtId != "fmt " || dataID != "data" || fmtSize != 16)
					{
						Console.WriteLine("Malformed WAV header");
						return;
					}

					if (channels != 1 || samplesPerSec != 11025 || avgBytesPerSec != 22050 || blockAlign != 2 || bitsPerSample != 16 || formatTag != 1 || chunkSize < 48)
					{
						Console.WriteLine("Unexpected WAV format, need 11025 Hz mono 16 bit (little endian integers)");
						return;
					}

					uint numberOfsamples = Math.Min(dataSize / 2, 330750); // max 30 seconds
					var pcmData = new Int16[numberOfsamples];
					for (int i = 0; i < numberOfsamples; i++)
					{
						pcmData[i] = reader.ReadInt16();
					}

					var echoPrint = new CodeGen();
					Console.WriteLine(echoPrint.Generate(pcmData));
					Console.WriteLine("");
					Console.WriteLine("The above is the EchoPrint code for the song '999,999' by Nine Inch Nails.");
					Console.WriteLine("To generate codes for your own mp3s or wavs add them as parameters to ");
					Console.WriteLine("TestEchoPrintSharp.exe in a command line or in the debug settings");
					Console.WriteLine("or execution setting of the TestEchoPrintSharp project.");
					Console.WriteLine("(mp3s only work on Windows, because NAudio depends on Msacm32.dll.)");
				}
			} 
			else
			{
				foreach (string audioFile in args)
				{
					if (audioFile.Length >= 4 && audioFile.Substring(audioFile.Length - 4).Equals(".wav", StringComparison.InvariantCultureIgnoreCase))
					{
						using (WaveFileReader wav = new WaveFileReader(audioFile))
						{
							using (WaveStream pcm = WaveFormatConversionStream.CreatePcmStream(wav))
							{
								using (BinaryReader reader = new BinaryReader(pcm, System.Text.Encoding.ASCII))
								{
									HandleFile(pcm, audioFile);
								}
							}
						}
					}
					else if (audioFile.Length >= 4 && audioFile.Substring(audioFile.Length - 4).Equals(".mp3", StringComparison.InvariantCultureIgnoreCase))
					try
					{
						using (Mp3FileReader mp3 = new Mp3FileReader(audioFile))
						{
							using (WaveStream pcm = WaveFormatConversionStream.CreatePcmStream(mp3))
							{
								HandleFile(pcm, audioFile);
							}
						}
					}
					catch (DllNotFoundException e)
					{
						Console.WriteLine("Sorry, a necessary dll is not found on your system. Here come the details:\r\n\r\n{0}", e);
					}
                    catch (Exception e)
                    {
                        Console.WriteLine("Sorry, something went wrong. Here come the details:\r\n\r\n{0}", e);
                    }
                }
			}
		}
			
		private static void HandleFile(object audioStream, string fileName)
		{
			var pcm = audioStream as WaveStream;

			using (BinaryReader reader = new BinaryReader(pcm, System.Text.Encoding.ASCII))
			{
				int avgBytesPerSec = pcm.WaveFormat.AverageBytesPerSecond;
				int bitsPerSample = pcm.WaveFormat.BitsPerSample;
				int blockAlign = pcm.WaveFormat.BlockAlign;
				int channels = pcm.WaveFormat.Channels;
				int samplesPerSec = pcm.WaveFormat.SampleRate;

				int numberOfsamples = Math.Min((int)pcm.Length, 30 * samplesPerSec * channels); // max 30 seconds
				var pcmData = new Int16[numberOfsamples];
				for (int i = 0; i < numberOfsamples; i++)
				{
					pcmData[i] = reader.ReadInt16();
				}

				Console.WriteLine("Code for {0}:", fileName);
				Console.WriteLine("");

				var echoPrint = new CodeGen();
				Console.WriteLine(echoPrint.Generate(pcmData, bitsPerSample, channels, (int)samplesPerSec));
				Console.WriteLine("");
			}
		}
	}
}
