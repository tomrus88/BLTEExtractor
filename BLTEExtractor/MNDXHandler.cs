using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BLTEExtractor
{
    class MARHdr
    {
        public int Index;
        public long Offset;
        public long Size;
    }

    class MNDXHandler
    {
        public MNDXHandler(string path)
        {
            using (var file = File.OpenRead(path))
            using (var br = new BinaryReader(file, Encoding.ASCII))
            {
                int magic = br.ReadInt32();

                if (magic != 0x58444e4d) // MNDX
                {
                    throw new InvalidDataException("MNDXHandler: magic");
                }

                int unk1 = br.ReadInt32(); // 2 (may be version)
                int unk2 = br.ReadInt32(); // 2 (may be version)
                int build1 = br.ReadInt32(); // 29108 (client build)
                int build2 = br.ReadInt32(); // 29108 (client build)
                int marHdrOffset = br.ReadInt32(); // 48 (MAR HDR start offset)
                int marBlockCount = br.ReadInt32(); // 3 (MAR blocks count)
                int marHdrSize = br.ReadInt32(); // 20 (MAR HDR size: 4+8+8)
                int hashBlockOffset = br.ReadInt32(); // 108 (hash block start offset)
                int hashCount = br.ReadInt32(); // 40850 (count of hash blocks)
                int flagHashCount = br.ReadInt32(); // 35726 (count of hashes with 0x80000000 flag)
                int hashBlockSize = br.ReadInt32(); // 24 (size of single hash block: 4+16+4)

                MARHdr[] mar = new MARHdr[marBlockCount];

                for (int i = 0; i < marBlockCount; ++i)
                {
                    br.BaseStream.Position = marHdrOffset + i * marHdrSize;

                    mar[i] = new MARHdr();
                    mar[i].Index = br.ReadInt32(); // 0, 1, 2 (MAR index)
                    mar[i].Size = br.ReadInt64(); // 4028, 388372, 278612 (size of MAR block)
                    mar[i].Offset = br.ReadInt64(); // MAR block offset 0x0EF61C, 0x0F05D8, 0x14F2EC

                    br.BaseStream.Position = mar[i].Offset;
                    byte[] data = br.ReadBytes((int)mar[i].Size);
                    var marh = new MARHandler(data, i);
                    File.WriteAllBytes("MAR" + i + ".dat", data); // dump
                }

                StreamWriter fs = new StreamWriter("hashes.txt", false);
                Dictionary<string, List<int[]>> hashes = new Dictionary<string, List<int[]>>();

                int flaggedHashes = 0;

                br.BaseStream.Position = hashBlockOffset;

                for (int i = 0; i < hashCount; ++i)
                {
                    int unkVal1 = br.ReadInt32();
                    string hash = br.ReadBytes(16).ToHexString();
                    int unkVal2 = br.ReadInt32();
                    if (!hashes.ContainsKey(hash))
                        hashes[hash] = new List<int[]>();

                    if ((unkVal1 & 0x80000000) != 0)
                        flaggedHashes++;

                    hashes[hash].Add(new int[] { unkVal1, unkVal2 });
                }

                int j = 1;
                fs.WriteLine("Unique file hashes: {0}", hashes.Count);
                fs.WriteLine("Flagged hashes: {0}", flaggedHashes);

                foreach (var h in hashes)
                {
                    fs.WriteLine("{0} - {1}: ", j, h.Key);
                    foreach (var o in h.Value)
                    {
                        fs.WriteLine("    {0:X8} - {1:X8}", o[0], o[1]);
                    }
                    j++;
                }

                fs.Close();
            }
        }
    }
}
