using Solnet.Wallet;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OreRecovery
{
    public class TokenAccountInfo
    {
        public PublicKey Mint { get; set; }
        public PublicKey Owner { get; set; }
        public ulong Amount { get; set; }
        public PublicKey? Delegate { get; set; }
        public byte State { get; set; }
        public ulong? IsNative { get; set; }
        public ulong DelegatedAmount { get; set; }
        public PublicKey? CloseAuthority { get; set; }

        public static TokenAccountInfo ReadFrom(ReadOnlySpan<byte> data)
        {
            if (data.Length < 165)
                throw new ArgumentException("Token account data must be at least 165 bytes", nameof(data));

            var account = new TokenAccountInfo();

            account.Mint = new PublicKey(data.Slice(0, 32));
            account.Owner = new PublicKey(data.Slice(32, 32));
            account.Amount = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(64, 8));

            uint delegateOption = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(72, 4));
            if (delegateOption == 1)
                account.Delegate = new PublicKey(data.Slice(76, 32));

            account.State = data[108];

            uint isNativeOption = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(109, 4));
            if (isNativeOption == 1)
                account.IsNative = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(113, 8));

            account.DelegatedAmount = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(121, 8));

            uint closeAuthorityOption = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(129, 4));
            if (closeAuthorityOption == 1)
                account.CloseAuthority = new PublicKey(data.Slice(133, 32));

            return account;
        }
    }
}
