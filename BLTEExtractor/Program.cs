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
            var hasher = new Jenkins96();

            Dictionary<ulong, string> names = new Dictionary<ulong, string>();

            var namesList = @"c:\Games\World of Warcraft Beta\listfiles\listfile.txt";

            if (File.Exists(namesList))
            {
                string[] lines = File.ReadAllLines(namesList);

                foreach (var line in lines)
                {
                    ulong hash = hasher.ComputeHash(line);

                    names[hash] = line;
                }
            }

            Dictionary<string, List<string>> hashes = new Dictionary<string, List<string>>();

            var rootFile = @"c:\Users\TOM_RUS\Documents\Visual Studio 2013\Projects\ListFileCreator\ListFileCreator\bin\2A7E73225A96EADCD5917D061A3AAE51_root.txt";

            if (File.Exists(rootFile))
            {
                using (var fs = new FileStream(rootFile, FileMode.Open))
                using (var sr = new BinaryReader(fs))
                {
                    int numUnnamed = 0;

                    while (sr.BaseStream.Position < sr.BaseStream.Length)
                    {
                        uint count = sr.ReadUInt32();
                        uint unk1 = sr.ReadUInt32();
                        uint unk2 = sr.ReadUInt32();

                        uint[] arr1 = new uint[count];

                        for (var i = 0; i < count; ++i)
                            arr1[i] = sr.ReadUInt32();

                        for (var i = 0; i < count; ++i)
                        {
                            string md5 = sr.ReadBytes(16).ToHexString().ToLower();
                            ulong hash = sr.ReadUInt64();

                            if (!names.ContainsKey(hash))
                            {
                                numUnnamed++;
                                Console.WriteLine("No name for hash: {0:X16}", hash);
                                continue;
                            }

                            if (!hashes.ContainsKey(md5))
                            {
                                hashes[md5] = new List<string>();
                                hashes[md5].Add(names[hash]);
                            }
                            else
                                hashes[md5].Add(names[hash]);
                        }
                    }

                    Console.WriteLine("We have {0} unnamed files!", numUnnamed);
                }
            }

            //SOUND\Creature\DraenorWolf\FX_FW_WolfHowl_Wet_01.OGG
            //1C F6 F2 36 6E 1F CB CE
            //SOUND\Creature\DraenorWolf\FX_FW_WolfHowl_Wet_05.OGG
            //43 4E C2 80 8A 82 EB 1C
            //Fonts\2002B.ttf
            //4B 8B F9 4C EC 73 B4 04
            //FONTS\ARHEI.TTF
            //C9 15 78 4E 97 AC B6 CD
            //var hash1 = hasher.ComputeHash("SOUND\\Creature\\DraenorWolf\\FX_FW_WolfHowl_Wet_01.OGG");
            //var hash2 = hasher.ComputeHash("SOUND\\Creature\\DraenorWolf\\FX_FW_WolfHowl_Wet_05.OGG");
            //var hash3 = hasher.ComputeHash("Fonts\\2002B.ttf");
            //var hash4 = hasher.ComputeHash("FONTS\\ARHEI.TTF");
            //new MNDXHandler(@"d:\heroes_out2\90C07A5A3E609FFA1007AF142F76794E.mndx");
            //return;

            if (args.Length < 2 || !File.Exists(args[0]))
            {
                Console.WriteLine("Usage: <input file> <output dir> [raw:true|false]");
                return;
            }

            bool raw = args.Length > 2 ? bool.Parse(args[2]) : false;

            if (!Directory.Exists(args[1] + "\\unnamed"))
                Directory.CreateDirectory(args[1] + "\\unnamed");

            StreamWriter logger = new StreamWriter("log.txt", true);

            using (var file = new FileStream(args[0], FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var br = new BinaryReader(file, Encoding.ASCII))
            {
                while (br.BaseStream.Position != br.BaseStream.Length)
                {
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

                        var md5 = h.Hash;

                        var name = Path.GetDirectoryName(h.Name) + "\\" + md5 + Path.GetExtension(h.Name);

                        File.Move(h.Name, name);

                        logger.WriteLine("{0} {1:X8} {2} {3} {4}", unkHash.ToHexString(), size, unkData1.ToHexString(), unkData2.ToHexString(), md5);

                        if (hashes.ContainsKey(md5.ToLower()))
                        {
                            var nn = hashes[md5.ToLower()];

                            if (nn.Count == 0)
                            {
                                Console.WriteLine("No name for {0}", md5);
                                logger.WriteLine("No name for {0}", md5);
                            }

                            foreach (var n in nn)
                            {
                                logger.WriteLine("{0}", n);

                                var n2 = Path.Combine(args[1], n);

                                var dir = Path.GetDirectoryName(n2);

                                if (!Directory.Exists(dir))
                                    Directory.CreateDirectory(dir);

                                if (!File.Exists(n2))
                                    File.Copy(name, n2);
                                else
                                {
                                    Console.WriteLine("File {0} ({1}) already exists! (1)", name, n);
                                    logger.WriteLine("File {0} ({1}) already exists! (1)", name, n);

                                    var n3 = Path.Combine(args[1], "name_collision\\", n);

                                    var dir2 = Path.GetDirectoryName(n3);

                                    if (!Directory.Exists(dir2))
                                        Directory.CreateDirectory(dir2);

                                    File.Copy(name, n3);
                                }
                            }

                            File.Delete(name);
                        }
                        else
                        {
                            logger.WriteLine("Found a file that isn't present in root file: {0}", name);

                            var dir = Path.Combine(args[1], "unreferenced\\");

                            if (!Directory.Exists(dir))
                                Directory.CreateDirectory(dir);

                            var p1 = Path.Combine(dir, md5 + Path.GetExtension(h.Name));

                            File.Move(name, p1);
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
