using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace BLTEExtractor
{
    class FileExistsException : Exception
    {
        public FileExistsException(string message) : base(message) { }
    }

    class BLTEChunk
    {
        public int compSize;
        public int decompSize;
        public byte[] hash;
        public byte[] data;
    }

    class BLTEHandler
    {
        BinaryReader reader;
        string saveName;
        MD5 md5 = MD5.Create();
        bool raw;
        int size;

        public string Name
        {
            get { return saveName; }
        }

        public string Hash
        {
            get
            {
                using (var f = File.OpenRead(saveName))
                {
                    return md5.ComputeHash(f).ToHexString();
                }
            }
        }

        public BLTEHandler(BinaryReader br, bool raw, int size)
        {
            this.reader = br;
            this.raw = raw;
            this.size = size;
        }

        public void ExtractData(string path, string name)
        {
            int magic = reader.ReadInt32(); // BLTE (raw)

            if (magic != 0x45544c42)
            {
                throw new InvalidDataException("BLTEHandler: magic");
            }

            int frameHeaderSize = reader.ReadInt32BE();
            int chunkCount = 0;
            int totalSize = 0;

            if (frameHeaderSize == 0)
            {
                totalSize = size - 38;

                long pos = reader.BaseStream.Position;

                reader.BaseStream.Position += totalSize + 30;

                if (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    magic = reader.ReadInt32();
                    reader.BaseStream.Position = pos;

                    if (magic != 0x45544c42)
                    {
                        while (reader.BaseStream.Position < reader.BaseStream.Length - 4)
                        {
                            magic = reader.ReadInt32();

                            if (magic != 0x45544c42)
                            {
                                reader.BaseStream.Position -= 3;
                            }
                            else
                            {
                                totalSize = (int)reader.BaseStream.Position - (int)pos - (4 + 4 + 10 + 16);
                                reader.BaseStream.Position = pos;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    reader.BaseStream.Position = pos;
                }
            }
            else
            {
                byte unk1 = reader.ReadByte(); // byte

                if (unk1 != 0x0F)
                    throw new InvalidDataException("unk1 != 0x0F");

                byte v1 = reader.ReadByte();
                byte v2 = reader.ReadByte();
                byte v3 = reader.ReadByte();
                chunkCount = v1 << 16 | v2 << 8 | v3 << 0; // 3-byte
            }

            if (chunkCount < 0)
            {
                throw new InvalidDataException(String.Format("Possible error ({0}) at offset: 0x" + reader.BaseStream.Position.ToString("X"), chunkCount));
            }

            BLTEChunk[] chunks = new BLTEChunk[chunkCount];

            for (int i = 0; i < chunkCount; ++i)
            {
                chunks[i] = new BLTEChunk();
                chunks[i].compSize = reader.ReadInt32BE();
                chunks[i].decompSize = reader.ReadInt32BE();
                chunks[i].hash = reader.ReadBytes(16);

                Console.WriteLine("Chunk {0}: hash {1}, size1 {2}, size2 {3}", i, chunks[i].hash.ToHexString(), chunks[i].compSize, chunks[i].decompSize);
            }

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            string ext = "txt";

            using (var f = File.Open(path + "\\" + name, FileMode.CreateNew))
            {
                if (chunkCount == 0)
                {
                    Console.WriteLine("Writing 1 chunk(s) for {0}...", name);

                    //totalSize = !this.raw ? (totalSize - 30) : totalSize;
                    byte[] soloData = reader.ReadBytes(totalSize);

                    switch (soloData[0])
                    {
                        //case 0x45: // E
                        //    break;
                        //case 0x46: // F
                        //    break;
                        case 0x4E: // N
                            {
                                if (ext == "txt")
                                {
                                    ext = GetFileExt(soloData, 1);
                                }

                                f.Write(soloData, 1, totalSize);
                            }
                            break;
                        case 0x5A: // Z
                            {
                                byte[] dec = Decompress(soloData);

                                if (ext == "txt")
                                {
                                    ext = GetFileExt(dec, 0);
                                }

                                f.Write(dec, 0, dec.Length);
                            }
                            break;
                        default:
                            Console.WriteLine("Unknown byte {0:X2} at 0x{1}", soloData[0], reader.BaseStream.Position.ToString("X"));
                            Console.ReadKey();
                            break;
                    }
                }
                else
                {
                    Console.WriteLine("Writing {0} chunk(s) for {1}...", chunkCount, name);

                    for (int i = 0; i < chunkCount; ++i)
                    {
                        //Console.WriteLine("size: " + chunks[i].compSize + " : " + chunks[i].compSize);
                        //Console.WriteLine("starting reading at 0x{0:X8}", reader.BaseStream.Position);
                        chunks[i].data = reader.ReadBytes(chunks[i].compSize);

                        byte[] hh = md5.ComputeHash(chunks[i].data);

                        Console.WriteLine("chunk hash: {0}", chunks[i].hash.ToHexString());

                        if (!hh.VerifyHash(chunks[i].hash))
                        {
                            throw new InvalidDataException("MD5 missmatch!");
                        }

                        switch (chunks[i].data[0])
                        {
                            case 0x4E:
                                {
                                    if (ext == "txt" && (i == 0 || i == chunkCount - 1))
                                    {
                                        ext = GetFileExt(chunks[i].data, 1);
                                    }

                                    if (chunks[i].data.Length - 1 != chunks[i].decompSize)
                                    {
                                        throw new InvalidDataException("Possible error (1) !");
                                    }

                                    f.Write(chunks[i].data, 1, chunks[i].decompSize);
                                }
                                break;
                            case 0x5A:
                                {
                                    byte[] dec = Decompress(chunks[i].decompSize, chunks[i].data);

                                    if (ext == "txt" && (i == 0 || i == chunkCount - 1))
                                    {
                                        ext = GetFileExt(dec, 0);
                                    }

                                    f.Write(dec, 0, dec.Length);
                                }
                                break;
                            default:
                                Console.WriteLine("Unknown byte {0:X2} at {1:X8}", chunks[i].data[0], reader.BaseStream.Position - chunks[i].data.Length);
                                Console.ReadKey();
                                break;
                        }
                    }
                }
            }

            //if (ext != "txt")
            //{
            //    if (!Directory.Exists(path + "\\" + ext))
            //        Directory.CreateDirectory(path + "\\" + ext);

            //    saveName = path + "\\" + ext + "\\" + name + "." + ext;
            //}
            //else

            saveName = path + "\\" + "unnamed\\" + name + "." + ext;

            if (!File.Exists(saveName))
                File.Move(path + "\\" + name, saveName);
            else
                throw new FileExistsException(saveName);
        }

        private byte[] Decompress(int size, byte[] data)
        {
            var output = new byte[size];

            using (var dStream = new DeflateStream(new MemoryStream(data, 3, data.Length - 3), CompressionMode.Decompress))
            {
                dStream.Read(output, 0, output.Length);
            }

            return output;
        }

        private byte[] Decompress(byte[] data)
        {
            MemoryStream outS = new MemoryStream();

            byte[] buf = new byte[512];

            using (var dStream = new DeflateStream(new MemoryStream(data, 3, data.Length - 3), CompressionMode.Decompress))
            {
                int len;
                while ((len = dStream.Read(buf, 0, buf.Length)) > 0)
                {
                    // Write the data block to the decompressed output stream
                    outS.Write(buf, 0, len);
                }
            }

            return outS.ToArray();
        }

        private string GetFileExt(byte[] data, int start)
        {
            if (data.Length >= 18 && data[data.Length - 18] == 0x54 && data[data.Length - 17] == 0x52 && data[data.Length - 16] == 0x55 && data[data.Length - 15] == 0x45 && data[data.Length - 14] == 0x56)
                return "tga";
            else if (data[start + 0] == 0x44 && data[start + 1] == 0x44 && data[start + 2] == 0x53)
                return "dds";
            else if (data[start + 0] == 0x4D && data[start + 1] == 0x5A && data[start + 2] == 0x90)
                return "dll";
            else if (data[start + 0] == 0x4D && data[start + 1] == 0x5A && data[start + 2] == 0x92)
                return "dll";
            else if (data[start + 0] == 0x43 && data[start + 1] == 0x46 && data[start + 2] == 0x58)
                return "cfx";
            else if (data[start + 0] == 0x4D && data[start + 1] == 0x50 && data[start + 2] == 0x51)
                return "mpq";
            else if (data[start + 0] == 0x43 && data[start + 1] == 0x57 && data[start + 2] == 0x53)
                return "cws";
            else if (data[start + 0] == 0x46 && data[start + 1] == 0x57 && data[start + 2] == 0x53)
                return "fws";
            else if (data[start + 0] == 0x3C && data[start + 1] == 0x3F && data[start + 2] == 0x78 && data[start + 3] == 0x6D && data[start + 4] == 0x6C)
                return "xml";
            else if (data[start + 0] == 0xEF && data[start + 1] == 0xBB && data[start + 2] == 0xBF && data[start + 3] == 0x3C && data[start + 4] == 0x3F && data[start + 5] == 0x78 && data[start + 6] == 0x6D && data[start + 7] == 0x6C)
                return "xml";
            else if (data[start + 0] == 0x62 && data[start + 1] == 0x70 && data[start + 2] == 0x6C && data[start + 3] == 0x69 && data[start + 4] == 0x73 && data[start + 5] == 0x74)
                return "bplist";
            else if (data[start + 0] == 0x46 && data[start + 1] == 0x41 && data[start + 2] == 0x43 && data[start + 3] == 0x45)
                return "face";
            else if (data[start + 0] == 0x63 && data[start + 1] == 0x64 && data[start + 2] == 0x65 && data[start + 3] == 0x73)
                return "cdes";
            else if (data[start + 0] == 0xCA && data[start + 1] == 0xFE && data[start + 2] == 0xBA && data[start + 3] == 0xBE)
                return "o";
            else if (data[start + 0] == 0xCE && data[start + 1] == 0xFA && data[start + 2] == 0xED && data[start + 3] == 0xFE)
                return "o";
            else if (data[start + 0] == 0x4F && data[start + 1] == 0x67 && data[start + 2] == 0x67 && data[start + 3] == 0x53)
                return "ogg";
            else if (data[start + 0] == 0x52 && data[start + 1] == 0x49 && data[start + 2] == 0x46 && data[start + 3] == 0x46)
                return "wav";
            else if (data[start + 0] == 0x89 && data[start + 1] == 0x50 && data[start + 2] == 0x4E && data[start + 3] == 0x47)
                return "png";
            else if (data[start + 0] == 0x52 && data[start + 1] == 0x54 && data[start + 2] == 0x58 && data[start + 3] == 0x54)
                return "rtxt";
            else if (data[start + 0] == 0x4C && data[start + 1] == 0x46 && data[start + 2] == 0x43 && data[start + 3] == 0x54)
                return "lfct";
            else if (data[start + 0] == 0x4D && data[start + 1] == 0x41 && data[start + 2] == 0x53 && data[start + 3] == 0x4B)
                return "mask";
            else if (data[start + 0] == 0x53 && data[start + 1] == 0x4D && data[start + 2] == 0x41 && data[start + 3] == 0x50)
                return "smap";
            else if (data[start + 0] == 0x48 && data[start + 1] == 0x4D && data[start + 2] == 0x41 && data[start + 3] == 0x50)
                return "hmap";
            else if (data[start + 0] == 0x43 && data[start + 1] == 0x4C && data[start + 2] == 0x49 && data[start + 3] == 0x46)
                return "clif";
            else if (data[start + 0] == 0x69 && data[start + 1] == 0x63 && data[start + 2] == 0x6E && data[start + 3] == 0x73)
                return "icns";
            else if (data[start + 0] == 0x6F && data[start + 1] == 0x72 && data[start + 2] == 0x65 && data[start + 3] == 0x48)
                return "oreh";
            else if (data[start + 0] == 0x57 && data[start + 1] == 0x41 && data[start + 2] == 0x54 && data[start + 3] == 0x52)
                return "watr";
            else if (data[start + 0] == 0x44 && data[start + 1] == 0x4C && data[start + 2] == 0x46 && data[start + 3] == 0x54)
                return "dlft";
            else if (data[start + 0] == 0x56 && data[start + 1] == 0x54 && data[start + 2] == 0x43 && data[start + 3] == 0x4C)
                return "vtcl";
            else if (data[start + 0] == 0x34 && data[start + 1] == 0x33 && data[start + 2] == 0x44 && data[start + 3] == 0x4D)
                return "m3";
            else if (data[start + 0] == 0x49 && data[start + 1] == 0x70 && data[start + 2] == 0x61 && data[start + 3] == 0x4D)
                return "ipam";
            else if (data[start + 0] == 0x48 && data[start + 1] == 0x52 && data[start + 2] == 0x44 && data[start + 3] == 0x54)
                return "hrdt";
            else if (data[start + 0] == 0x4D && data[start + 1] == 0x4E && data[start + 2] == 0x44 && data[start + 3] == 0x58)
                return "mndx";
            else if (data[start + 0] == 0x4F && data[start + 1] == 0x54 && data[start + 2] == 0x54 && data[start + 3] == 0x4F)
                return "otf";
            else if (data[start + 0] == 0x53 && data[start + 1] == 0x50 && data[start + 2] == 0x58 && data[start + 3] == 0x47)
                return "spxg";
            else if (data[start + 0] == 0x53 && data[start + 1] == 0x56 && data[start + 2] == 0x58 && data[start + 3] == 0x47)
                return "svxg";
            // wow
            else if (data[start + 0] == 0x42 && data[start + 1] == 0x4C && data[start + 2] == 0x50 && data[start + 3] == 0x32)
                return "blp";
            else if (data[start + 0] == 0x48 && data[start + 1] == 0x53 && data[start + 2] == 0x58 && data[start + 3] == 0x47)
                return "bls";
            else if (data[start + 0] == 0x57 && data[start + 1] == 0x44 && data[start + 2] == 0x42 && data[start + 3] == 0x43)
                return "dbc";
            else if (data[start + 0] == 0x4D && data[start + 1] == 0x44 && data[start + 2] == 0x32 && data[start + 3] == 0x30)
                return "m2";
            else if (data[start + 0] == 0x53 && data[start + 1] == 0x4B && data[start + 2] == 0x49 && data[start + 3] == 0x4E)
                return "skin";
            else if (data[start + 0] == 0x57 && data[start + 1] == 0x44 && data[start + 2] == 0x42 && data[start + 3] == 0x32)
                return "db2";
            else if (data[start + 0] == 0x4D && data[start + 1] == 0x4E && data[start + 2] == 0x50 && data[start + 3] == 0x5A)
                return "sbt";
            else if (data[start + 0] == 0x52 && data[start + 1] == 0x56 && data[start + 2] == 0x42 && data[start + 3] == 0x4D)
                return "blob";
            else if (data[start + 0] == 0x52 && data[start + 1] == 0x56 && data[start + 2] == 0x58 && data[start + 3] == 0x54)
                return "tex";
            else if (data[start + 0] == 0x52 && data[start + 1] == 0x45 && data[start + 2] == 0x56 && data[start + 3] == 0x4D)
            {
                if (data[start + 12] == 0x52 && data[start + 13] == 0x44 && data[start + 14] == 0x48 && data[start + 15] == 0x4D)
                    return "adt";
                if (data[start + 12] == 0x58 && data[start + 13] == 0x44 && data[start + 14] == 0x4D && data[start + 15] == 0x4D)
                    return "adt";
                if (data[start + 12] == 0x50 && data[start + 13] == 0x4D && data[start + 14] == 0x41 && data[start + 15] == 0x4D)
                    return "adt";
                if (data[start + 12] == 0x44 && data[start + 13] == 0x48 && data[start + 14] == 0x4F && data[start + 15] == 0x4D)
                    return "wmo";
                if (data[start + 12] == 0x50 && data[start + 13] == 0x47 && data[start + 14] == 0x4F && data[start + 15] == 0x4D)
                    return "wmo";
                if (data[start + 12] == 0x4F && data[start + 13] == 0x4D && data[start + 14] == 0x57 && data[start + 15] == 0x4D)
                    return "wdt";
                if (data[start + 12] == 0x44 && data[start + 13] == 0x48 && data[start + 14] == 0x50 && data[start + 15] == 0x4D)
                    return "wdt";
                return "wmo_adt";
            }
            else if (data[start + 0] == 0x4D && data[start + 1] == 0x4E && data[start + 2] == 0x50 && data[start + 3] == 0x46)
                return "mnpf";
            else if (data[start + 0] == 0x52 && data[start + 1] == 0x49 && data[start + 2] == 0x46 && data[start + 3] == 0x46)
                return "avi";
            else if (data[start + 0] == 0x49 && data[start + 1] == 0x44 && data[start + 2] == 0x33)
                return "mp3";
            else
                return "txt";
        }
    }
}
