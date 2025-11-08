using Solnet.Wallet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OreRecovery
{
    public class Member
    {
        /// <summary>
        /// The member id.
        /// </summary>
        public ulong Id { get; set; }

        /// <summary>
        /// The pool this member belongs to.
        /// </summary>
        public PublicKey Pool { get; set; }

        /// <summary>
        /// The authority allowed to claim this balance.
        /// </summary>
        public PublicKey Authority { get; set; }

        /// <summary>
        /// The current balance amount which may be claimed.
        /// </summary>
        public ulong Balance { get; set; }

        /// <summary>
        /// The total balance this member has earned in the lifetime of their participation in the pool.
        /// </summary>
        public ulong TotalBalance { get; set; }

        public Member(ulong id, PublicKey pool, PublicKey authority, ulong balance, ulong totalBalance)
        {
            Id = id;
            Pool = pool;
            Authority = authority;
            Balance = balance;
            TotalBalance = totalBalance;
        }

        /// <summary>
        /// Reads a Member instance from a span of bytes (deserializing a Solana account).
        /// Layout: 8 bytes for u64, 32 bytes for each PubKey, 8 bytes for each u64 balance.
        /// Total: 8 + 32 + 32 + 8 + 8 = 88 bytes.
        /// </summary>
        public static Member ReadFrom(ReadOnlySpan<byte> data)
        {
            if (data.Length < 88)
                throw new ArgumentException("Data too short to read Member");

            ulong id = BitConverter.ToUInt64(data.Slice(0, 8));
            var pool = new PublicKey(data.Slice(8, 32).ToArray());
            var authority = new PublicKey(data.Slice(40, 32).ToArray());
            ulong balance = BitConverter.ToUInt64(data.Slice(72, 8));
            ulong totalBalance = BitConverter.ToUInt64(data.Slice(80, 8));

            return new Member(id, pool, authority, balance, totalBalance);
        }
    }
}
