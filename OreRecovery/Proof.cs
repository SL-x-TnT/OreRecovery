using Solnet.Wallet;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OreRecovery
{
    public class Proof
    {
        public PublicKey Authority { get; set; }
        public ulong Balance { get; set; }
        public byte[] Challenge { get; set; }
        public byte[] LastHash { get; set; }
        public long LastHashAt { get; set; }
        public long LastStakeAt { get; set; }
        public PublicKey Miner { get; set; }
        public ulong TotalHashes { get; set; }
        public ulong TotalRewards { get; set; }

        public static Proof ReadFrom(ReadOnlySpan<byte> data)
        {
            if (data.Length < 8 + 32 + 8 + 32 + 32 + 8 + 8 + 32 + 8 + 8)
                throw new ArgumentException("Invalid account data length for Proof.");

            // Skip first 8 bytes (Anchor discriminator)
            data = data.Slice(8);

            int offset = 0;

            return new Proof
            {
                Authority = new PublicKey(Slice(data, 32).ToArray()),
                Balance = BinaryPrimitives.ReadUInt64LittleEndian(Slice(data, 8)),
                Challenge = Slice(data, 32).ToArray(),
                LastHash = Slice(data, 32).ToArray(),
                LastHashAt = BinaryPrimitives.ReadInt64LittleEndian(Slice(data, 8)),
                LastStakeAt = BinaryPrimitives.ReadInt64LittleEndian(Slice(data, 8)),
                Miner = new PublicKey(Slice(data, 32).ToArray()),
                TotalHashes = BinaryPrimitives.ReadUInt64LittleEndian(Slice(data, 8)),
                TotalRewards = BinaryPrimitives.ReadUInt64LittleEndian(Slice(data, 8)),
            };

            ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> d, int length)
            {
                var part = d.Slice(offset, length);
                offset += length;
                return part;
            }
        }
    }
}
