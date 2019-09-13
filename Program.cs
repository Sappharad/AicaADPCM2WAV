using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace aicaadpcm2wav
{
    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length < 2)
            {
                Console.WriteLine("Usage: aicaadpcm2wav inputFile outputFile [-start=] [-length=] [-freq=]");
                Console.WriteLine("Outputs 22050hz WAV by default. If outputFile extension is .pcm you get raw PCM instead.");
                Console.WriteLine();
                Console.WriteLine("The following arguments are optional:");
                Console.WriteLine("  -start=### specify a start offset");
                Console.WriteLine("  -length=### specify a length to convert");
                Console.WriteLine("  -freq=### override the default frequency for WAV output");
                return;
            }
            string inFile = args[0];
            string outFile = args[1];
            uint start = 0;
            uint length = 0;
            uint frequency = 22050;
            for (int i = 2; i < args.Length; i++)
            {
                int vLoc = args[i].IndexOf('=') + 1;
                bool valid = (vLoc > 0);
                if (valid)
                {
                    string vVal = args[i].Substring(vLoc);
                    int vNumber;
                    if (vVal.StartsWith("0x", StringComparison.Ordinal))
                    {
                        if (!int.TryParse(vVal.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out vNumber))
                        {
                            valid = false;
                        }
                    }
                    else if(!int.TryParse(vVal, out vNumber))
                    {
                        valid = false;
                    }
                    if(vNumber < 0)
                    {
                        valid = false;
                    }
                    else if (args[i].StartsWith("-start", StringComparison.Ordinal))
                    {
                        start = (uint)vNumber;
                    }
                    else if (args[i].StartsWith("-length", StringComparison.Ordinal))
                    {
                        length = (uint)vNumber;
                    }
                    else if (args[i].StartsWith("-freq", StringComparison.Ordinal))
                    {
                        frequency = (uint)vNumber;
                    }
                }
                if(!valid)
                {
                    Console.WriteLine($"Argument {args[i]} is not understood. Exiting...");
                    return;
                }
            }
            if(frequency < 8000 || frequency > 48000)
            {
                Console.WriteLine("WAV Frequency should be between 8000 and 48000. Example: 44100");
                return;
            }
            if (File.Exists(inFile))
            {
                byte[] data = File.ReadAllBytes(inFile);
                if(start > data.Length || (length > 0 && start+length > data.Length))
                {
                    Console.WriteLine("Data range specified is larger than the input file. Can't do anything with that.");
                    return;
                }
                //If we made it this far, we can actually do the work now!
                if (length == 0) length = (uint)(data.Length - start);
                data = adpcm2pcm(data, start, length);
                if (!outFile.EndsWith(".pcm"))
                {
                    data = AddWavHeader(data, frequency);
                }
                File.WriteAllBytes(outFile, data);
                Console.WriteLine("Done!");
            }
         
        }

        #region AICA ADPCM decoding
        static readonly int[] diff_lookup = {
            1,3,5,7,9,11,13,15,
            -1,-3,-5,-7,-9,-11,-13,-15,
        };

        static int[] index_scale = {
            0x0e6, 0x0e6, 0x0e6, 0x0e6, 0x133, 0x199, 0x200, 0x266
        };

        private static byte[] adpcm2pcm(byte[] input, uint src, uint length)
        {
            byte[] dst = new byte[length * 4];
            int dstLoc = 0;
            int cur_quant = 0x7f;
            int cur_sample = 0;
            bool highNybble = false;

            while (dstLoc < dst.Length)
            {
                int shift1 = highNybble ? 4 : 0;
                int delta = (input[src] >> shift1) & 0xf;

                int x = cur_quant * diff_lookup[delta & 15];
                x = cur_sample + ((int)(x + ((uint)x >> 29)) >> 3);
                cur_sample = (x < -32768) ? -32768 : ((x > 32767) ? 32767 : x);
                cur_quant = (cur_quant * index_scale[delta & 7]) >> 8;
                cur_quant = (cur_quant < 0x7f) ? 0x7f : ((cur_quant > 0x6000) ? 0x6000 : cur_quant);

                dst[dstLoc++] = (byte)(cur_sample & 0xFF);
                dst[dstLoc++] = (byte)((cur_sample >> 8) & 0xFF);

                highNybble = !highNybble;
                if (!highNybble)
                {
                    src++;
                }
            }
            return dst;
        }
        #endregion

        #region WAV stuff
        public static byte[] AddWavHeader(byte[] input, uint frequency, byte bitDepth = 16)
        {
            byte[] output = new byte[input.Length + 44];
            Array.Copy(Encoding.ASCII.GetBytes("RIFF"), 0, output, 0, 4);
            WriteUint(4, (uint)output.Length - 8, output);
            Array.Copy(Encoding.ASCII.GetBytes("WAVE"), 0, output, 8, 4);
            Array.Copy(Encoding.ASCII.GetBytes("fmt "), 0, output, 12, 4);
            WriteUint(16, 16, output); //Header size
            output[20] = 1; //PCM
            output[22] = 1; //1 channel
            WriteUint(24, frequency, output); //Sample Rate
            WriteUint(28, (uint)(frequency * (bitDepth / 8)), output); //Bytes per second
            output[32] = (byte)(bitDepth >> 3); //Bytes per sample
            output[34] = bitDepth; //Bits per sample
            Array.Copy(Encoding.ASCII.GetBytes("data"), 0, output, 36, 4);
            WriteUint(40, (uint)output.Length, output); //Date size
            Array.Copy(input, 0, output, 44, input.Length);

            return output;
        }

        public static byte[] ChangeBitDepth16to32(byte[] input)
        {
            byte[] output = new byte[input.Length * 2];
            //Expand by repeating. 0x9876 becomes 0x98769876 which should be equivalent to the original amplitude.

            for (int i = 0; i < input.Length; i += 2)
            {
                output[(i * 2) + 0] = input[i];
                output[(i * 2) + 1] = input[i + 1];
                output[(i * 2) + 2] = input[i];
                output[(i * 2) + 3] = input[i + 1];
            }

            return output;
        }

        private static void WriteUint(uint offset, uint value, byte[] destination)
        {
            for (int i = 0; i < 4; i++)
            {
                destination[offset + i] = (byte)(value & 0xFF);
                value >>= 8;
            }
        }
        #endregion
    }
}
