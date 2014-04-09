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
            //SOUND\Creature\DraenorWolf\FX_FW_WolfHowl_Wet_01.OGG
            //1C F6 F2 36 6E 1F CB CE
            //SOUND\Creature\DraenorWolf\FX_FW_WolfHowl_Wet_05.OGG
            //43 4E C2 80 8A 82 EB 1C
            var hasher = new Jenkins96();
            var hash1 = hasher.ComputeHash("SOUND\\Creature\\DraenorWolf\\FX_FW_WolfHowl_Wet_01.OGG");
            var hash2 = hasher.ComputeHash("SOUND\\Creature\\DraenorWolf\\FX_FW_WolfHowl_Wet_05.OGG");

            //new MNDXHandler(@"d:\heroes_out2\90C07A5A3E609FFA1007AF142F76794E.mndx");
            //return;

            if (args.Length < 2 || !File.Exists(args[0]))
            {
                Console.WriteLine("Usage: <input file> <output dir> [raw:true|false]");
                return;
            }

            bool raw = args.Length > 2 ? bool.Parse(args[2]) : false;

            Dictionary<string, List<string>> hashes = new Dictionary<string, List<string>>();

            if (File.Exists(@"c:\Games\World of Warcraft Beta\listfiles\wow_beta3.md5"))
            {
                using (StreamReader sr = new StreamReader(@"c:\Games\World of Warcraft Beta\listfiles\wow_beta3.md5"))
                {
                    string line;

                    while ((line = sr.ReadLine()) != null)
                    {
                        var hash = line.Substring(0, 32);

                        var name = line.Substring(34);

                        if (!hashes.ContainsKey(hash))
                        {
                            hashes[hash] = new List<string>();
                            hashes[hash].Add(name);
                        }
                        else
                            hashes[hash].Add(name);
                    }
                }
            }

            if (!Directory.Exists(args[1] + "\\unnamed"))
                Directory.CreateDirectory(args[1] + "\\unnamed");

            StreamWriter logger = new StreamWriter("log.txt");

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
                            Console.WriteLine("{0} already exists", e.Message);
                            logger.WriteLine("{0} already exists", e.Message);
                        }

                        var md5 = h.Hash;

                        var name = Path.GetDirectoryName(h.Name) + "\\" + md5 + Path.GetExtension(h.Name);

                        File.Move(h.Name, name);

                        logger.WriteLine("{0} {1} {2:D8} {3}", unkHash.ToHexString(), size, unkData1.ToHexString(), unkData2.ToHexString(), md5);

                        if (hashes.ContainsKey(md5.ToLower()))
                        {
                            foreach (var n in hashes[md5.ToLower()])
                            {
                                logger.WriteLine("{0}", n);

                                var n2 = Path.Combine(args[1], n);

                                var dir = Path.GetDirectoryName(n2);

                                if (!Directory.Exists(dir))
                                    Directory.CreateDirectory(dir);

                                File.Copy(name, n2);
                            }

                            File.Delete(name);
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
