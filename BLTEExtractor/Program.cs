using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace BLTEExtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            //new MNDXHandler(@"d:\heroes_out2\90C07A5A3E609FFA1007AF142F76794E.mndx");
            //return;

            if (args.Length < 2 || !File.Exists(args[0]))
            {
                Console.WriteLine("Usage: <input file> <output dir> [raw:true|false]");
                return;
            }

            bool raw = args.Length > 2 ? bool.Parse(args[2]) : false;

            using (var file = new FileStream(args[0], FileMode.Open, FileAccess.Read, FileShare.Read))
            //using (var file = File.OpenRead(args[0], ))
            using (var br = new BinaryReader(file, Encoding.ASCII))
            {
                while (br.BaseStream.Position != br.BaseStream.Length)
                {
                    if (raw)
                    {
                        BLTEHandler h = new BLTEHandler(br, raw, 0);
                        h.ExtractData(args[1], "temp.blte");

                        File.Move(h.Name, Path.GetDirectoryName(h.Name) + "\\" + h.Hash + Path.GetExtension(h.Name));
                    }
                    else
                    {
                        byte[] unkHash = br.ReadBytes(16);
                        int size = br.ReadInt32();
                        byte[] unkData = br.ReadBytes(10);

                        BLTEHandler h = new BLTEHandler(br, raw, size);
                        h.ExtractData(args[1], unkHash.ToHexString());
                    }
                }
            }
        }
    }

    static class Extensions
    {
        public static int ReadInt32BE(this BinaryReader reader)
        {
            return BitConverter.ToInt32(reader.ReadBytes(4).Reverse().ToArray(), 0);
        }

        public static short ReadInt16BE(this BinaryReader reader)
        {
            return BitConverter.ToInt16(reader.ReadBytes(2).Reverse().ToArray(), 0);
        }

        public static string ToHexString(this byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", string.Empty);
            //var str = String.Empty;
            //for (var i = 0; i < data.Length; ++i)
            //    str += data[i].ToString("X2", CultureInfo.InvariantCulture);
            //return str;
        }

        public static bool VerifyHash(this byte[] hash, byte[] other)
        {
            for (var i = 0; i < hash.Length; ++i)
            {
                if (hash[i] != other[i])
                    return false;
            }
            return true;
        }
    }

    public static class CStringExtensions
    {
        /// <summary> Reads the NULL terminated string from 
        /// the current stream and advances the current position of the stream by string length + 1.
        /// <seealso cref="BinaryReader.ReadString"/>
        /// </summary>
        public static string ReadCString(this BinaryReader reader)
        {
            return reader.ReadCString(Encoding.UTF8);
        }

        /// <summary> Reads the NULL terminated string from 
        /// the current stream and advances the current position of the stream by string length + 1.
        /// <seealso cref="BinaryReader.ReadString"/>
        /// </summary>
        public static string ReadCString(this BinaryReader reader, Encoding encoding)
        {
            try
            {
                var bytes = new List<byte>();
                byte b;
                while ((b = reader.ReadByte()) != 0)
                    bytes.Add(b);
                return encoding.GetString(bytes.ToArray());
            }
            catch (EndOfStreamException)
            {
                return String.Empty;
            }
        }

        public static void WriteCString(this BinaryWriter writer, string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            writer.Write(bytes);
            writer.Write((byte)0);
        }
    }
}
