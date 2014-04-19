using System;
using System.Collections.Generic;
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

            var namesList = @"_c:\Games\World of Warcraft Beta\listfiles\finallist2.txt";

            var rootFile = @"_c:\Users\TOM_RUS\Documents\Visual Studio 2013\Projects\CASCNames\CASCNames\root";

            RootHandler root = new RootHandler(namesList, rootFile);

            if (args.Length < 2 || !File.Exists(args[0]))
            {
                Console.WriteLine("Usage: <input file> <output dir> [raw:true|false]");
                return;
            }

            bool raw = args.Length > 2 ? bool.Parse(args[2]) : false;

            if (!Directory.Exists(args[1] + "\\unnamed"))
                Directory.CreateDirectory(args[1] + "\\unnamed");

            StreamWriter logger = new StreamWriter("log.txt", true);

            string dataFile = args[0];

            using (var file = new FileStream(dataFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var br = new BinaryReader(file, Encoding.ASCII))
            {
                while (br.BaseStream.Position != br.BaseStream.Length)
                {
                    long startPos = br.BaseStream.Position;

                    if (raw)
                    {
                        BLTEHandler h = new BLTEHandler(br, raw, 0);

                        try
                        {
                            h.ExtractData(args[1], "temp.blte");
                        }
                        catch (FileExistsException e)
                        {
                            Console.WriteLine("{0} already exists", e.Message);
                        }

                        File.Move(h.Name, Path.GetDirectoryName(h.Name) + "\\" + h.Hash + Path.GetExtension(h.Name));
                    }
                    else
                    {
                        byte[] unkHash = br.ReadBytes(16);
                        int size = br.ReadInt32();
                        byte[] unkData1 = br.ReadBytes(2);
                        byte[] unkData2 = br.ReadBytes(8);

                        BLTEHandler h = new BLTEHandler(br, raw, size);

                        try
                        {
                            h.ExtractData(args[1], "temp.blte");
                        }
                        catch (FileExistsException e)
                        {
                            Console.WriteLine("{0} already exists! (0)", e.Message);
                            logger.WriteLine("{0} already exists! (0)", e.Message);
                        }

                        var md5 = h.HashBytes;
                        var md5String = md5.ToHexString();

                        logger.WriteLine("{6} {5:X8} {0} {1:X8} {2} {3} {4}", unkHash.ToHexString(), size, unkData1.ToHexString(), unkData2.ToHexString(), md5String, startPos, Path.GetFileName(dataFile));

                        var nn = root.GetNamesForMD5(md5);

                        if (nn != null)
                        {
                            foreach (var n in nn)
                            {
                                logger.WriteLine("{0}", n);

                                var n2 = Path.Combine(args[1], n);

                                if (n.StartsWith("unnamed\\"))
                                    n2 += Path.GetExtension(h.Name);

                                var dir = Path.GetDirectoryName(n2);

                                if (!Directory.Exists(dir))
                                    Directory.CreateDirectory(dir);

                                if (!File.Exists(n2))
                                    File.Copy(h.Name, n2);
                                else
                                {
                                    Console.WriteLine("File {0} ({1}) already exists! (1)", n2, n);
                                    logger.WriteLine("File {0} ({1}) already exists! (1)", n2, n);

                                    var n3 = Path.Combine(args[1], "name_collision\\", n);

                                    if (n.StartsWith("unnamed\\"))
                                        n3 += Path.GetExtension(h.Name);

                                    var dir2 = Path.GetDirectoryName(n3);

                                    if (!Directory.Exists(dir2))
                                        Directory.CreateDirectory(dir2);

                                    if (!File.Exists(n3))
                                        File.Copy(h.Name, n3);
                                    else
                                    {
                                        for (int i = 0; i < 100000; ++i)
                                        {
                                            if (!File.Exists(n3 + "_" + i))
                                            {
                                                File.Copy(h.Name, n3 + "_" + i);
                                                break;
                                            }
                                        }
                                    }
                                }
                            }

                            File.Delete(h.Name);
                        }
                        else
                        {
                            logger.WriteLine("Found a file that isn't present in root file: {0}", md5String + Path.GetExtension(h.Name));

                            var dir = Path.Combine(args[1], "unreferenced\\");

                            if (!Directory.Exists(dir))
                                Directory.CreateDirectory(dir);

                            var p1 = Path.Combine(dir, md5String + Path.GetExtension(h.Name));

                            if (!File.Exists(p1))
                                File.Move(h.Name, p1);
                            else
                                File.Delete(h.Name);
                        }
                    }
                }
            }

            logger.Flush();
            logger.Close();
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
