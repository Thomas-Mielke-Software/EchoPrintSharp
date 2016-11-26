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

namespace EchoPrintSharp
{
	public class Whitening
	{
		protected float[] _pSamples;
		protected float[] _whitened;
		protected uint _NumSamples;
		protected float[] _R;
		protected float[] _Xo;
		protected float[] _ai;
		protected int _p;

		public Whitening(Int16[] pcm)
		{
			var samples = new float[pcm.Length];
			for (int i = 0; i < pcm.Length; i++)
			{
				samples[i] = (float)pcm[i] / 32768f;
			}
			WhiteningFloat(samples, samples.Length);
		}	

		public void WhiteningFloat(float[] pSamples, int numSamples)
		{
			_pSamples = pSamples;
			_NumSamples = (uint)numSamples;
			Init();
		}
			
		private void Init() {
			int i;
			_p = 40;

			_R = new float[_p+1];
			for (i = 0; i <= _p; ++i)  { _R[i] = 0f; }
			_R[0] = 0.001f;

			_Xo = new float[_p+1];
			for (i = 0; i < _p; ++i)  { _Xo[i] = 0f; }

			_ai = new float[_p+1];
			_whitened = new float[_NumSamples];
		}

		public void Compute() {
			int blocklen = 10000;
			int i, newblocklen;
			for(i=0;i<(int)_NumSamples;i=i+blocklen) {
				if (i+blocklen >= (int)_NumSamples) {
					newblocklen = (int)_NumSamples -i - 1;
				} else { newblocklen = blocklen; }
				ComputeBlock(i, newblocklen);
			}
		}

		void ComputeBlock(int start, int blockSize) {
			int i, j;
			float alpha, E, ki;
			float T = 8;
			alpha = 1f/T;

			// calculate autocorrelation of current block

			for (i = 0; i <= _p; ++i) {
				float acc = 0;
				for (j = i; j < (int)blockSize; ++j) {
					acc += _pSamples[j+start] * _pSamples[j-i+start];
				}
				// smoothed update
				_R[i] += alpha*(acc - _R[i]);
			}

			// calculate new filter coefficients
			// Durbin's recursion, per p. 411 of Rabiner & Schafer 1978
			E = _R[0];
			for (i = 1; i <= _p; ++i) {
				float sumalphaR = 0;
				for (j = 1; j < i; ++j) {
					sumalphaR += _ai[j]*_R[i-j];
				}
				ki = (_R[i] - sumalphaR)/E;
				_ai[i] = ki;
				for (j = 1; j <= i/2; ++j) {
					float aj = _ai[j];
					float aimj = _ai[i-j];
					_ai[j] = aj - ki*aimj;
					_ai[i-j] = aimj - ki*aj;
				}
				E = (1-ki*ki)*E;
			}
			// calculate new output
			for (i = 0; i < (int)blockSize; ++i) {
				float acc = _pSamples[i+start];
				int minip = i;
				if (_p < minip) {
					minip = _p;
				}

				for (j = i+1; j <= _p; ++j) {
					acc -= _ai[j]*_Xo[_p + i-j];
				}
				for (j = 1; j <= minip; ++j) {
					acc -= _ai[j]*_pSamples[i-j+start];
				}
				_whitened[i+start] = acc;
			}
			// save last few frames of input
			for (i = 0; i <= _p; ++i) {
				_Xo[i] = _pSamples[blockSize-1-_p+i+start];
			}
		}

		public float[] WhitenedSamples
		{
			get 
			{ 
				return _whitened; 
			}
		}

		public uint NumSamples 
		{ 
			get
			{
				return _NumSamples; 
			}
		}
	}
}