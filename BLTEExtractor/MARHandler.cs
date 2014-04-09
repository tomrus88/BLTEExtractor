using System;
using System.IO;
using System.Text;

namespace BLTEExtractor
{
    class MARHandler
    {
        public MARHandler(byte[] data, int i)
        {
            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms, Encoding.ASCII))
            {
                int magic = br.ReadInt32();

                if (magic != 0x0052414d) // MAR\0
                {
                    throw new InvalidDataException("MARHandler: magic");
                }

                StreamWriter fs = new StreamWriter(String.Format("names{0}.txt", i + 1), false);

                int[] offsetsStart = new int[] { 0x1AC, 0x245FC, 0x356DC };
                int[] offsetsEnd = new int[] { 0x384, 0x5E0D8, 0x43078 };

                br.BaseStream.Position = offsetsStart[i];

                while (br.BaseStream.Position < offsetsEnd[i])
                {
                    fs.WriteLine("{0}", br.ReadCString());
                }

                fs.Close();
            }
        }
    }
}
