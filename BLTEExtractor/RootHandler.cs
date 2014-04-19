using System;
using System.Collections.Generic;
using System.IO;

namespace BLTEExtractor
{
    class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        readonly uint FnvPrime32 = 16777619;
        readonly uint FnvOffset32 = 2166136261;

        public bool Equals(byte[] x, byte[] y)
        {
            if (x.Length != y.Length)
                return false;

            for (int i = 0; i < x.Length; ++i)
                if (x[i] != y[i])
                    return false;

            return true;
        }

        public int GetHashCode(byte[] obj)
        {
            return To32BitFnv1aHash(obj);
        }

        private int To32BitFnv1aHash(byte[] toHash)
        {
            uint hash = FnvOffset32;

            foreach (var chunk in toHash)
            {
                hash ^= chunk;
                hash *= FnvPrime32;
            }

            return unchecked((int)hash);
        }
    }

    class FileDesc
    {
        public BlockDesc block;
        public int unk;
        public byte[] MD5;
        public ulong Hash;

        public override string ToString()
        {
            return String.Format("Block: {0:X8} {1:X8}, File: {2:X8} {3}", block.unk1, block.unk2, unk, MD5.ToHexString());
        }
    }

    class BlockDesc
    {
        public uint unk1;
        public uint unk2;
    }

    class RootHandler
    {
        static ByteArrayComparer comparer = new ByteArrayComparer();
        Dictionary<ulong, List<FileDesc>> hash_to_fileDesc = new Dictionary<ulong, List<FileDesc>>();
        Dictionary<byte[], List<string>> md5_to_names = new Dictionary<byte[], List<string>>(comparer);
        Dictionary<ulong, string> names = new Dictionary<ulong, string>();
        Jenkins96 hasher = new Jenkins96();

        public RootHandler(string namesList, string rootFile)
        {
            if (File.Exists(namesList))
            {
                string[] lines = File.ReadAllLines(namesList);

                foreach (var line in lines)
                {
                    ulong hash = hasher.ComputeHash(line);

                    names[hash] = line;
                }
            }

            if (File.Exists(rootFile))
            {
                using (var fs = new FileStream(rootFile, FileMode.Open))
                using (var sr = new BinaryReader(fs))
                {
                    int numUnnamed = 0;

                    while (sr.BaseStream.Position < sr.BaseStream.Length)
                    {
                        uint count = sr.ReadUInt32();

                        BlockDesc block = new BlockDesc();
                        block.unk1 = sr.ReadUInt32();
                        block.unk2 = sr.ReadUInt32();

                        //Console.WriteLine("Root: count {0}, unk1 {1:X8}, unk2 {2:X8}", count, block.unk1, block.unk2);

                        //if (block.unk1 != 1)
                        //    Console.WriteLine();

                        FileDesc[] arr1 = new FileDesc[count];

                        for (var i = 0; i < count; ++i)
                        {
                            arr1[i] = new FileDesc();
                            arr1[i].block = block;
                            arr1[i].unk = sr.ReadInt32();
                        }

                        for (var i = 0; i < count; ++i)
                        {
                            byte[] md5 = sr.ReadBytes(16);
                            arr1[i].MD5 = md5;

                            ulong hash = sr.ReadUInt64();
                            arr1[i].Hash = hash;

                            if (!names.ContainsKey(hash))
                            {
                                numUnnamed++;
                                //Console.WriteLine("No name for hash: {0:X16}", hash);
                            }

                            if (!hash_to_fileDesc.ContainsKey(hash))
                            {
                                hash_to_fileDesc[hash] = new List<FileDesc>();
                                hash_to_fileDesc[hash].Add(arr1[i]);
                            }
                            else
                                hash_to_fileDesc[hash].Add(arr1[i]);

                            if (!md5_to_names.ContainsKey(md5))
                            {
                                md5_to_names[md5] = new List<string>();

                                if(names.ContainsKey(hash))
                                    md5_to_names[md5].Add(names[hash]);
                                else
                                    AddUniqueDummyName(md5);
                            }
                            else
                            {
                                if (names.ContainsKey(hash))
                                    md5_to_names[md5].Add(names[hash]);
                                else
                                    AddUniqueDummyName(md5);
                            }
                        }
                    }

                    Console.WriteLine("We have {0} unnamed files!", numUnnamed);
                }
            }
        }

        private void AddUniqueDummyName(byte[] md5)
        {
            bool foundName = false;
            string unkName = "unnamed\\" + md5.ToHexString();

            for (int j = 0; j < md5_to_names[md5].Count; ++j)
            {
                if (md5_to_names[md5][j] == unkName)
                {
                    foundName = true;
                    break;
                }
            }
            if (!foundName)
                md5_to_names[md5].Add(unkName);
            else
            {
                for (int k = 0; k < 100000; ++k)
                {
                    bool foundName2 = false;
                    string suffix = "_" + k;

                    for (int j = 0; j < md5_to_names[md5].Count; ++j)
                    {
                        if (md5_to_names[md5][j] == unkName + suffix)
                        {
                            foundName2 = true;
                            break;
                        }
                    }
                    if (!foundName2)
                    {
                        md5_to_names[md5].Add(unkName + suffix);
                        break;
                    }
                }
            }
        }

        public List<string> GetNamesForMD5(byte[] md5)
        {
            if (md5_to_names.ContainsKey(md5))
                return md5_to_names[md5];
            return null;
        }

        public List<FileDesc> GetFileDescForHash(ulong hash)
        {
            if (hash_to_fileDesc.ContainsKey(hash))
                return hash_to_fileDesc[hash];
            return null;
        }
    }
}
