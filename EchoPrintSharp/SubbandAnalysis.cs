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
//using MathNet.Numerics.LinearAlgebra;
//using MathNet.Numerics.LinearAlgebra.float;

namespace EchoPrintSharp
{
	public class SubbandAnalysis
	{
		private const int C_LEN = 128;
		private const int SUBBANDS = 8;
		private const int M_ROWS = 8;
		private const int M_COLS = 16;
		private const float M_PI = 3.1415926536f;

		private float[] _pSamples;
		private int _NumSamples;
		private int _NumFrames;
		private float[,] _Mi;
		private float[,] _Mr;
		private float[,] _Data;

		static float[] C = {
			0.000000477f,  0.000000954f,  0.000001431f,  0.000002384f,  0.000003815f,  0.000006199f,  0.000009060f,  0.000013828f,
			0.000019550f,  0.000027657f,  0.000037670f,  0.000049591f,  0.000062943f,  0.000076771f,  0.000090599f,  0.000101566f,
			-0.000108242f, -0.000106812f, -0.000095367f, -0.000069618f, -0.000027180f,  0.000034332f,  0.000116348f,  0.000218868f,
			0.000339031f,  0.000472546f,  0.000611782f,  0.000747204f,  0.000866413f,  0.000954151f,  0.000994205f,  0.000971317f,
			-0.000868797f, -0.000674248f, -0.000378609f,  0.000021458f,  0.000522137f,  0.001111031f,  0.001766682f,  0.002457142f,
			0.003141880f,  0.003771782f,  0.004290581f,  0.004638195f,  0.004752159f,  0.004573822f,  0.004049301f,  0.003134727f,
			-0.001800537f, -0.000033379f,  0.002161503f,  0.004756451f,  0.007703304f,  0.010933399f,  0.014358521f,  0.017876148f,
			0.021372318f,  0.024725437f,  0.027815342f,  0.030526638f,  0.032754898f,  0.034412861f,  0.035435200f,  0.035780907f,
			-0.035435200f, -0.034412861f, -0.032754898f, -0.030526638f, -0.027815342f, -0.024725437f, -0.021372318f, -0.017876148f,
			-0.014358521f, -0.010933399f, -0.007703304f, -0.004756451f, -0.002161503f,  0.000033379f,  0.001800537f,  0.003134727f,
			-0.004049301f, -0.004573822f, -0.004752159f, -0.004638195f, -0.004290581f, -0.003771782f, -0.003141880f, -0.002457142f,
			-0.001766682f, -0.001111031f, -0.000522137f, -0.000021458f,  0.000378609f,  0.000674248f,  0.000868797f,  0.000971317f,
			-0.000994205f, -0.000954151f, -0.000866413f, -0.000747204f, -0.000611782f, -0.000472546f, -0.000339031f, -0.000218868f,
			-0.000116348f, -0.000034332f,  0.000027180f,  0.000069618f,  0.000095367f,  0.000106812f,  0.000108242f,  0.000101566f,
			-0.000090599f, -0.000076771f, -0.000062943f, -0.000049591f, -0.000037670f, -0.000027657f, -0.000019550f, -0.000013828f,
			-0.000009060f, -0.000006199f, -0.000003815f, -0.000002384f, -0.000001431f, -0.000000954f, -0.000000477f, 0f};

		public int NumFrames
		{ 
			get
			{
				return _NumFrames; 
			}
		}
			
		public int NumBands
		{
			get
			{ 
				return SUBBANDS; 
			}
		}

		public float[,] Data
		{
			get
			{ 
				return _Data; 
			}
		}

		public SubbandAnalysis(float[] pSamples)
		{			
			_pSamples = pSamples;
			_NumSamples = _pSamples.Length;
			Init();
		}

		private void Init() 
		{
			// Calculate the analysis filter bank coefficients
			_Mr = new float[M_ROWS, M_COLS];
			_Mi = new float[M_ROWS, M_COLS];
			for (int i = 0; i < M_ROWS; ++i) {
				for (int k = 0; k < M_COLS; ++k) {
					_Mr[i,k] = (float)Math.Cos((double)(2*i + 1)*(k-4)*(M_PI/16.0f));
					_Mi[i,k] = (float)Math.Sin((double)(2*i + 1)*(k-4)*(M_PI/16.0f));
				}
			}
		}

		public void Compute() {
			int t, i, j;

			float[] Z = new float[C_LEN];
			float[] Y = new float[M_COLS];

			_NumFrames = (_NumSamples - C_LEN + 1)/SUBBANDS;
			if (_NumFrames <= 0)
			{
				Debug.WriteLine("NumSampes has to be > 0");
				return;					
			}

			_Data = new float[SUBBANDS, _NumFrames];

			for (t = 0; t < _NumFrames; ++t) {
				for (i = 0; i < C_LEN; ++i) {
					Z[i] = _pSamples[t*SUBBANDS + i] * C[i];
				}

				for (i = 0; i < M_COLS; ++i) {
					Y[i] = Z[i];
				}
				for (i = 0; i < M_COLS; ++i) {
					for (j = 1; j < M_ROWS; ++j) {
						Y[i] += Z[i + M_COLS*j];
					}
				}
				for (i = 0; i < M_ROWS; ++i) {
					float Dr = 0, Di = 0;
					for (j = 0; j < M_COLS; ++j) {
						Dr += _Mr[i,j] * Y[j];
						Di -= _Mi[i,j] * Y[j];
					}
					_Data[i, t] = Dr*Dr + Di*Di;
				}
			}
		}
	}
}

