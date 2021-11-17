using Concentus.Oggfile;
using Concentus.Structs;
using EchoPrintSharp;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using Ionic.Zlib;
using System.Text;

namespace TestEchoPrintSharp
{
    public class Tests
    {
        private Int16[] opusData;       // at 11025 samples per second
        private Int16[] opusData48k;    // at 48000 samples per second
        private int opusLength;
        private int opusLength48k;

        private Int16[] pcmData;       // at 11025 samples per second
        private int pcmLength;      

        [SetUp]
        public void Setup()
        {
            // --- opus encoded data from resource ---

            opusData = new Int16[1000000];
            opusData48k = new Int16[5000000];
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "TestEchoPrintSharp.Resources.NIN-999999-11025.opus";
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                // setup decoder
                OpusDecoder decoder = OpusDecoder.Create(48000, 1);
                OpusOggReadStream oggIn = new OpusOggReadStream(decoder, stream);

                // store decoded PCM data in opusData
                opusLength48k = 0;
                while (oggIn.HasNextPacket)
                {
                    short[] packet = oggIn.DecodeNextPacket();
                    if (packet != null)
                    {
                        for (int i = 0; i < packet.Length && opusLength48k < opusData48k.Length; i++)
                        {
                            opusData48k[opusLength48k++] = packet[i];
                        }
                    }
                }

                // downsampling got 11025 samples per second
                for (opusLength = 0; opusLength < opusData.Length; opusLength++)
                {
                    opusData[opusLength] = opusData48k[opusLength * 1920 / 441];
                }
            }

            // --- PCM data from resource ---


            resourceName = "TestEchoPrintSharp.Resources.NIN-999999-11025.wav";

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

                pcmLength = (int)Math.Min(dataSize / 2, 1000000); // max 30 seconds
                pcmData = new Int16[pcmLength];
                for (int i = 0; i < pcmLength; i++)
                {
                    pcmData[i] = reader.ReadInt16();
                }
            }
        }

        [Test]
        public void OpusDecoderGeneratedSomeData()
        {
            Assert.Greater(opusLength, 500000);
        }

        [Test]
        public void OpusDataFingerprintingGeneratedSomeData()
        {
            string opusFingerprint;

            var echoPrint = new CodeGen();
            opusFingerprint = echoPrint.Generate(opusData);
            Assert.Greater(opusFingerprint.Length, 5000);
        }

        [Test]
        public void PcmReaderGeneratedSomeData()
        {
            Assert.Greater(pcmLength, 500000);
        }

        [Test]
        public void PcmDataFingerprintingGeneratedSomeData()
        {
            string pcmFingerprint;

            var echoPrint = new CodeGen();
            pcmFingerprint = echoPrint.Generate(pcmData);
            Assert.Greater(pcmFingerprint.Length, 5000);
        }

        /// <summary>
        /// compares the wavs pcm data with the output of the opus codec
        /// </summary>
        [Test]
        public void WavOpusSimilarity()
        {
            string opusFingerprint;
            string pcmFingerprint;

            // cut out 30 second from the middle of the file
            var opusDataSection = new short[330750];
            var pcmDataSection = new short[330750];
            Buffer.BlockCopy(opusData, 330750, opusDataSection, 0, 330750);
            Buffer.BlockCopy(pcmData, 340775, pcmDataSection, 0, 330750);       // starts 1 sec later than opus section

            // generate fingerprints
            var echoPrint = new CodeGen();
            opusFingerprint = echoPrint.Generate(opusDataSection);
            pcmFingerprint = echoPrint.Generate(pcmDataSection);
             
            byte[] unbase64edOpusFingerprint = Convert.FromBase64String(opusFingerprint.Replace('-', '+').Replace('_', '/'));
            string unzippedOpusFingerprint = ZlibStream.UncompressString(unbase64edOpusFingerprint);
            byte[] unbase64edPcmFingerprint = Convert.FromBase64String(pcmFingerprint.Replace('-', '+').Replace('_', '/'));
            string unzippedPcmFingerprint = ZlibStream.UncompressString(unbase64edPcmFingerprint);

            // compute Damereau-Levenshein Distance in absence of actual fingerprint matching algorithm
            int similarity = ComputeLevenshteinDistance(unzippedOpusFingerprint, unzippedPcmFingerprint);
            float averageDifferencePerCharacter = (float)similarity / (unzippedOpusFingerprint.Length + unzippedPcmFingerprint.Length / 2);
            Assert.Less(averageDifferencePerCharacter, 0.4);
            Assert.Greater(averageDifferencePerCharacter, 0.001);
        }

        private static int ComputeLevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s))
            {
                if (string.IsNullOrEmpty(t))
                    return 0;
                return t.Length;
            }

            if (string.IsNullOrEmpty(t))
            {
                return s.Length;
            }

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            // initialize the top and right of the table to 0, 1, 2, ...
            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 1; j <= m; d[0, j] = j++) ;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    int min1 = d[i - 1, j] + 1;
                    int min2 = d[i, j - 1] + 1;
                    int min3 = d[i - 1, j - 1] + cost;
                    d[i, j] = Math.Min(Math.Min(min1, min2), min3);
                }
            }
            return d[n, m];
        }
    }
}