// EchoPrintSharp is released under the MIT License (MIT)
// 
// Copyright (c) 2016 Thomas Mielke
//
// based on the C++ code of echoprint-codegen by The Echo Nest Corporation 
// (also released under the MIT license)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy 
// of this software and associated documentation files (the "Software"), to deal 
// in the Software without restriction, including without limitation the rights 
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in 
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE 
// SOFTWARE.

using System;
using System.Diagnostics;
using System.Text;
using System.IO.Compression;
using System.IO;

namespace EchoPrintSharp
{
	public class CodeGen
	{
/*		static float[] whiteningTest = {
			0f,
			0f,
			-6.10352e-05f,
			9.88094e-05f,
			-0.000156418f,
			0.000196428f,
			-0.000221543f,
			0.000167611f,
			-9.31111e-05f,
			5.73524e-06f,
			3.73807e-05f,
			-9.06385e-05f,
			7.1944e-05f,
			-5.8101e-05f,
			3.83176e-05f,
			-5.72064e-05f,
			7.00401e-05f,
			-9.05802e-05f,
			7.1089e-05f,
			-8.92023e-05f
		};

		static float[] subAnalTest = {
			2.83638e-11f,
			3.06465e-12f,
			1.51252e-12f,
			8.80312e-13f,
			4.52466e-12f,
			6.16352e-11f,
			3.03414e-10f,
			1.25312e-10f
		};
*/
		private const double ECHOPRINT_VERSION = 4.12;
		public string GetCodeString() { return _CodeString; }
		public int GetNumCodes() { return _NumCodes; }
		public static double GetVersion() { return ECHOPRINT_VERSION; }
		private string _CodeString;
		private int _NumCodes;

		public CodeGen()
		{
			//Debug.WriteLine("EchoPrintSharp PCL initialized.");
		}

		// Code generation for deviating audio data that has to be resampled first
		public string Generate(Int16[] pcm, int bitsPerSample, int numberOfChannels, int samplingrate)
		{
			if (bitsPerSample != 16 || numberOfChannels != 1 || samplingrate != 11025)
				return Generate(Resample(pcm, bitsPerSample, numberOfChannels, samplingrate));
			else
				return Generate(pcm);
		}

		// Code generation for 11,025Hz mono 16 bit audio data
		public string Generate(Int16[] pcm)
		{
			var whitening = new Whitening(pcm);
			whitening.Compute();


			// Test Whitening
			/*int i;
			for (i = 0; i < 20; i++)
			{
				if (Math.Abs(whiteningTest[i]-whitening.WhitenedSamples[i]) > 0.00001f)
				{
					Debug.WriteLine("Whitening test failed");
					break;					
				}
			}*/


			var subAnal = new SubbandAnalysis(whitening.WhitenedSamples);
			subAnal.Compute();


			// Subband analysis test
			//int j;
			//for (j = 0; j < 10; ++j)
			/*{
				Debug.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t", 
					subAnal.Data[0,j],
					subAnal.Data[1,j],
					subAnal.Data[2,j],
					subAnal.Data[3,j],
					subAnal.Data[4,j],
					subAnal.Data[5,j],
					subAnal.Data[6,j],
					subAnal.Data[7,j]
				);
				for (i = 0; i < 8; i++)
				{
					if (Math.Abs(subAnalTest[i] - subAnal.Data[i, 9]) > 0.00001f)
					{
						Debug.WriteLine("Whitening tst failed");
						break;					
					}
				}
			}*/



			var fingerprint = new Fingerprint(ref subAnal, 0);
			fingerprint.Compute();
			/*if (fingerprint.GetCodes()[1800].code != 19692 || fingerprint.GetCodes()[1800].frame != 1383)
			{
				Debug.WriteLine("Fingerprinting test failed");
			}*/

			_CodeString = CreateCodeString(fingerprint.GetCodes());
			_NumCodes = fingerprint.GetCodes().Length;

			return _CodeString;
		}

		private string CreateCodeString(FPCode[] vCodes)
		{
			if (vCodes.Length < 3) 
			{
				return "";
			}
			StringBuilder codestream = new StringBuilder(vCodes.Length * 10);

			foreach (FPCode vCode in vCodes)
				codestream.AppendFormat("{0:x05}", vCode.frame);

			foreach (FPCode vCode in vCodes)
				codestream.AppendFormat("{0:x05}", vCode.code);
			
			return Compress(codestream.ToString());
			//return compress(codestream.str());
		}

		//static readonly char[] padding = { '=' };.TrimEnd(padding)
		private string Compress(string s)
		{
			Encoding enc = Encoding.GetEncoding("us-ascii");
			byte[] bytes = enc.GetBytes(s);

			MemoryStream output = new MemoryStream();
            /*using (DeflateStream dstream = new DeflateStream(output, CompressionLevel.Optimal))
			{
				dstream.Write(bytes, 0, bytes.Length);
			} obviously not the same Deflate as in zlib!*/

            // ZLib Deflate
            using (Ionic.Zlib.ZlibStream dstream = new Ionic.Zlib.ZlibStream(output, Ionic.Zlib.CompressionMode.Compress))
            {
                dstream.Write(bytes, 0, bytes.Length);
                dstream.Close();
            }

			string base64string = Convert.ToBase64String(output.ToArray()).Replace('+', '-').Replace('/', '_');
			return base64string;
		}

		// Resample -- emulates the following ffmpeg call:
		// ffmpeg -i file.wav -f s16le -ac 1 -ar 11025 - (creates signed 16-bit shorts, mono, 11,025Hz sampling rate)
		private Int16[] Resample(Int16[] source_samples, int source_bps, int source_numberofchannels, int source_samplingrate)
		{
			if (source_samples == null) return null;
			int source_buffersize = source_samples.Length;
			var target_samples = new Int16[(Int64)source_buffersize * (Int64)Params.SamplingRate / (source_numberofchannels * source_samplingrate)];
			int target_buffersize = target_samples.Length;
			if (source_bps != 16) return null; // TODO: support sample width other than 16 bits

			int source_index, target_index;

			for (target_index = source_index = 0; 
				target_index < target_buffersize && source_index < source_buffersize;
				target_index++, source_index = (int)((Int64)target_index * source_numberofchannels * source_samplingrate / (Int64)Params.SamplingRate))
			{
				// mix stereo or multichannel source samples to mono
				int mixed_source_sample = 0;
				int channel;
				for (channel = 0; channel < source_numberofchannels; channel ++)
					mixed_source_sample += source_samples[source_index + channel];
				mixed_source_sample /= source_numberofchannels;

				target_samples[target_index] = (short)mixed_source_sample;	// TODO: proper interpolation for source sampling rates that are no multiple of 11,025	
			}
			return target_samples;
		}
	}
}
