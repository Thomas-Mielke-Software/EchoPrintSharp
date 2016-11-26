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

namespace EchoPrintSharp
{
	public class FPCode {
		public FPCode() { frame = code = 0; }
		public FPCode(uint f, uint c) { frame = f; code = c; }
		public uint frame;
		public uint code;
	};

	public class Fingerprint
	{
		private const uint HASH_SEED = 0x9ea5fa36;
		private const double QUANTIZE_DT_S = (256.0/11025.0);
		private const double QUANTIZE_A_S = (256.0/11025.0); 
		private const uint HASH_BITMASK = 0x000fffff;
		private const int SUBBANDS = 8;

		public FPCode[] GetCodes() { return _Codes; }
		protected SubbandAnalysis _pSubbandAnalysis;
		protected int _Offset;
		protected FPCode[] _Codes;

		const UInt32 m = 0x5bd1e995;
		const Int32 r = 24;
		uint MurmurHash2 (Byte[] key, UInt32 seed) {
			// MurmurHash2, by Austin Appleby http://sites.google.com/site/murmurhash/
			// C# implementation grabbed from http://landman-code.blogspot.de/2009/02/c-superfasthash-and-murmurhash2.html
			Int32 length = key.Length;
			if (length == 0)
				return 0;
			UInt32 h = seed ^ (UInt32)length;
			Int32 currentIndex = 0;
			while (length >= 4)
			{
				UInt32 k = BitConverter.ToUInt32(key, currentIndex);
				k *= m;
				k ^= k >> r;
				k *= m;

				h *= m;
				h ^= k;
				currentIndex += 4;
				length -= 4;
			}
			switch (length)
			{
			case 3:
				h ^= BitConverter.ToUInt16(key, currentIndex);
				h ^= (UInt32)key[currentIndex + 2] << 16;
				h *= m;
				break;
			case 2:
				h ^= BitConverter.ToUInt16(key, currentIndex);
				h *= m;
				break;
			case 1:
				h ^= key[currentIndex];
				h *= m;
				break;
			default:
				break;
			}

			// Do a few final mixes of the hash to ensure the last few
			// bytes are well-incorporated.

			h ^= h >> 13;
			h *= m;
			h ^= h >> 15;

			return h;
		}

		public Fingerprint(ref SubbandAnalysis pSubbandAnalysis, int offset)
		{
			_pSubbandAnalysis = pSubbandAnalysis;
			_Offset = offset;
		}

		public uint AdaptiveOnsets(int ttarg, out float[,] output, out uint[] onset_counter_for_band)
		{
			//  E is a sgram-like matrix of energies.
			int EIndex;
			int bands, frames, i, j, k;
			int deadtime = 128;
			var H = new double[SUBBANDS];
			var taus = new double[SUBBANDS];
			var N = new double[SUBBANDS];
			var contact = new int[SUBBANDS];
			var lcontact = new int[SUBBANDS];
			var tsince = new int[SUBBANDS];
			double overfact = 1.1;  /* threshold rel. to actual peak */
			uint onset_counter = 0;

			float[,] E = _pSubbandAnalysis.Data;

			// Take successive stretches of 8 subband samples and sum their energy under a hann window, then hop by 4 samples (50% window overlap).
			int hop = 4;
			int nsm = 8;
			var ham = new float[8];
			for(i = 0 ; i != nsm ; i++)
				ham[i] = (float)(0.5 - 0.5*Math.Cos((2.0*3.1415926536/(nsm-1))*i));

			int nc = (int)(Math.Floor((float)E.GetLength(1)/(float)hop)-(Math.Floor((float)nsm/(float)hop)-1.0));
			var Eb = new float[nc, 8];
			for(uint r=0;r<Eb.GetLength(0);r++) for(uint c=0;c<Eb.GetLength(1);c++) Eb[r,c] = 0.0f;

			for(i=0;i<nc;i++) {
				for(j=0;j<SUBBANDS;j++) {
					for(k=0;k<nsm;k++)  Eb[i,j] = Eb[i,j] + ( E[j,(i*hop)+k] * ham[k]);
					Eb[i,j] = (float)Math.Sqrt(Eb[i,j]);
				}
			}

			frames = Eb.GetLength(0);
			bands = Eb.GetLength(1);
			EIndex = 0;

			output = new float[SUBBANDS, frames];
			onset_counter_for_band = new uint[SUBBANDS];

			double[] bn = {0.1883, 0.4230, 0.3392}; /* preemph filter */   // new
			int nbn = 3;
			double a1 = 0.98;
			var Y0 = new double[SUBBANDS];

			for (j = 0; j < bands; ++j) 
			{
				onset_counter_for_band[j] = 0;
				N[j] = 0.0;
				taus[j] = 1.0;
				H[j] = Eb[EIndex, j];
				contact[j] = 0;
				lcontact[j] = 0;
				tsince[j] = 0;
				Y0[j] = 0;
			}

			for (i = 0; i < frames; ++i) 
			{
				for (j = 0; j < SUBBANDS; ++j) 
				{

					double xn = 0;
					/* calculate the filter -  FIR part */
					if (i >= 2*nbn) {
						for (k = 0; k < nbn; ++k) {
							xn += bn[k]*(Eb[EIndex-k, j] - Eb[EIndex-(2*nbn-k), j]);
						}
					}
					/* IIR part */
					xn = xn + a1*Y0[j];
					/* remember the last filtered level */
					Y0[j] = xn;

					contact[j] = (xn > H[j])? 1 : 0;

					if (contact[j] == 1 && lcontact[j] == 0) 
					{
						/* attach - record the threshold level unless we have one */
						if(N[j] == 0) 
						{
							N[j] = H[j];
						}
					}
					if (contact[j] == 1) 
					{
						/* update with new threshold */
						H[j] = xn * overfact;
					} else 
					{
						/* apply decays */
						H[j] = H[j] * Math.Exp(-1.0/(double)taus[j]);
					}

					if (contact[j] == 0 && lcontact[j] == 1) 
					{
						/* detach */
						if (onset_counter_for_band[j] > 0 && (int)output[j, onset_counter_for_band[j]-1] > i - deadtime) 
						{
							// overwrite last-written time
							--onset_counter_for_band[j];
							--onset_counter;
						}
						output[j, onset_counter_for_band[j]++] = i;
						++onset_counter;
						tsince[j] = 0;
					}
					++tsince[j];
					if (tsince[j] > ttarg) 
					{
						taus[j] = taus[j] - 1;
						if (taus[j] < 1) taus[j] = 1;
					} else {
						taus[j] = taus[j] + 1;
					}

					if ( (contact[j] == 0) &&  (tsince[j] > deadtime)) 
					{
						/* forget the threshold where we recently hit */
						N[j] = 0;
					}
					lcontact[j] = contact[j];
				}
				EIndex++;
			}

			return onset_counter;
		}

		public uint quantized_time_for_frame_delta(uint frame_delta)
		{
			double time_for_frame_delta = (double)frame_delta / ((double)Params.SamplingRate / 32.0);
			return (uint)((Math.Floor((time_for_frame_delta * 1000.0) / (float)QUANTIZE_DT_S) * (float)QUANTIZE_DT_S) / Math.Floor(QUANTIZE_DT_S*1000.0));
		}

		public uint quantized_time_for_frame_absolute(uint frame)
		{
			double time_for_frame = _Offset + (double)frame / ((double)Params.SamplingRate / 32.0);
			return (uint)((Math.Round((time_for_frame * 1000.0) /  (float)QUANTIZE_A_S) * QUANTIZE_A_S) / Math.Floor(QUANTIZE_A_S*1000.0));
		}

		public void Compute()
		{
			uint actual_codes = 0;
			var hash_material = new byte[5];
			uint i;
			for(i = 0; i < 5; i++) hash_material[i] = 0;
			uint[] onset_counter_for_band;
			float[,] output;
			uint onset_count = AdaptiveOnsets(345, out output, out onset_counter_for_band);
			Array.Resize(ref _Codes, (int)onset_count*6);

			for(byte band = 0; band < SUBBANDS; band++) 
			{
				if (onset_counter_for_band[band]>2) 
				{
					for(uint onset = 0; onset < onset_counter_for_band[band] - 2; onset++) 
					{
						// What time was this onset at?
						uint time_for_onset_ms_quantized = quantized_time_for_frame_absolute((uint)output[band,onset]);

						var p = new uint[2, 6];
						for (i = 0; i < 6; i++) 
						{
							p[0, i] = 0;
							p[1, i] = 0;
						}
						int nhashes = 6;

						if ((int)onset == (int)onset_counter_for_band[band]-4)  { nhashes = 3; }
						if ((int)onset == (int)onset_counter_for_band[band]-3)  { nhashes = 1; }
						p[0, 0] = (uint)(output[band,onset+1] - output[band,onset]);
						p[1, 0] = (uint)(output[band,onset+2] - output[band,onset+1]);
						if(nhashes > 1) {
							p[0, 1] = (uint)(output[band,onset+1] - output[band,onset]);
							p[1, 1] = (uint)(output[band,onset+3] - output[band,onset+1]);
							p[0, 2] = (uint)(output[band,onset+2] - output[band,onset]);
							p[1, 2] = (uint)(output[band,onset+3] - output[band,onset+2]);
							if(nhashes > 3) {
								p[0, 3] = (uint)(output[band,onset+1] - output[band,onset]);
								p[1, 3] = (uint)(output[band,onset+4] - output[band,onset+1]);
								p[0, 4] = (uint)(output[band,onset+2] - output[band,onset]);
								p[1, 4] = (uint)(output[band,onset+4] - output[band,onset+2]);
								p[0, 5] = (uint)(output[band,onset+3] - output[band,onset]);
								p[1, 5] = (uint)(output[band,onset+4] - output[band,onset+3]);
							}
						}

						// For each pair emit a code
						for(uint k=0;k<6;k++) 
						{
							// Quantize the time deltas to 23ms
							short time_delta0 = (short)quantized_time_for_frame_delta(p[0, k]);
							short time_delta1 = (short)quantized_time_for_frame_delta(p[1, k]);
							// Create a key from the time deltas and the band index
							hash_material[0] = (byte)(time_delta0 & 0x00ff);
							hash_material[1] = (byte)((time_delta0 & 0xff00) >> 8);
							hash_material[2] = (byte)(time_delta1 & 0x00ff);
							hash_material[3] = (byte)((time_delta1 & 0xff00) >> 8);
							hash_material[4] = band;
							uint hashed_code = MurmurHash2(hash_material, HASH_SEED) & HASH_BITMASK;

							// Set the code alongside the time of onset
							_Codes[actual_codes++] = new FPCode(time_for_onset_ms_quantized, hashed_code);
							//Debug.WriteLine("whee {0},{1}: [{2}, {3}] ({4}, {5}), {6} = {7} at {8}\n", actual_codes, k, time_delta0, time_delta1, p[0, k], p[1, k], band, hashed_code, time_for_onset_ms_quantized);
						}
					}
				}
			}

			Array.Resize(ref _Codes, (int)actual_codes);	
		}
	}
}

