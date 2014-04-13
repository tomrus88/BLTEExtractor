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
        public int unk;
        public byte[] MD5;
        public ulong Hash;
    }

    class RootHandler
    {
        static ByteArrayComparer comparer = new ByteArrayComparer();
        Dictionary<ulong, List<FileDesc>> hashes = new Dictionary<ulong, List<FileDesc>>();
        Dictionary<byte[], List<string>> hashes2 = new Dictionary<byte[], List<string>>(comparer);
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
                        uint unk1 = sr.ReadUInt32();
                        uint unk2 = sr.ReadUInt32();

                        //Console.WriteLine("Root: count {0}, unk1 {1}, unk2 {2}", count, unk1, unk2);

                        //if (unk1 != 1)
                        //    Console.WriteLine();

                        FileDesc[] arr1 = new FileDesc[count];

                        for (var i = 0; i < count; ++i)
                        {
                            arr1[i] = new FileDesc();
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
                                continue;
                            }

                            if (!hashes.ContainsKey(hash))
                            {
                                hashes[hash] = new List<FileDesc>();
                                hashes[hash].Add(arr1[i]);
                            }
                            else
                                hashes[hash].Add(arr1[i]);

                            if (!hashes2.ContainsKey(md5))
                            {
                                hashes2[md5] = new List<string>();
                                hashes2[md5].Add(names[hash]);
                            }
                            else
                                hashes2[md5].Add(names[hash]);
                        }
                    }

                    Console.WriteLine("We have {0} unnamed files!", numUnnamed);
                }
            }
        }

        public List<string> GetNamesForMD5(byte[] md5)
        {
            if (hashes2.ContainsKey(md5))
                return hashes2[md5];
            return null;
        }

        public List<FileDesc> GetFileDescForHash(ulong hash)
        {
            if (hashes.ContainsKey(hash))
                return hashes[hash];
            return null;
        }
    }
}
