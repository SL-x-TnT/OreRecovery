using Solnet.Wallet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OreRecovery
{
    public class Share
    {
        /// <summary>
        /// The authority of this share account.
        /// </summary>
        public PublicKey Authority { get; set; }

        /// <summary>
        /// The stake balance the authority has deposited and may unstake.
        /// </summary>
        public ulong Balance { get; set; }

        /// <summary>
        /// The mint this share account is associated with.
        /// </summary>
        public PublicKey Mint { get; set; }

        /// <summary>
        /// The pool this share account is associated with.
        /// </summary>
        public PublicKey Pool { get; set; }

        public Share(PublicKey authority, ulong balance, PublicKey mint, PublicKey pool)
        {
            Authority = authority;
            Balance = balance;
            Mint = mint;
            Pool = pool;
        }

        /// <summary>
        /// Reads a Share instance from a span of bytes (like deserializing a Solana account).
        /// Assumes the layout is 32 bytes for each PubKey and 8 bytes for the u64 balance.
        /// </summary>
        public static Share ReadFrom(ReadOnlySpan<byte> data)
        {
            if (data.Length < 32 + 8 + 32 + 32)
                throw new ArgumentException("Data too short to read Share");

            var authority = new PublicKey(data.Slice(0, 32).ToArray());
            var balance = BitConverter.ToUInt64(data.Slice(32, 8));
            var mint = new PublicKey(data.Slice(40, 32).ToArray());
            var pool = new PublicKey(data.Slice(72, 32).ToArray());

            return new Share(authority, balance, mint, pool);
        }
    }
}
