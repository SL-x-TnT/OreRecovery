using Newtonsoft.Json;
using Solnet.Programs;
using Solnet.Programs.Abstract;
using Solnet.Programs.Utilities;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Models;
using Solnet.Wallet;
using Solnet.Wallet.Utilities;
using System.Text;

namespace OreRecovery
{
    internal class Program
    {
        static PublicKey _programId = new PublicKey("DdWBvLQxbyPcry5G8T3arkgcsYYRzYfdZiCXTevxT6vP");
        static PublicKey _oreTreasury = new PublicKey("Dh5ZkjGD8EVujR7C8mxMyYaE2LRVarJ9W6bMofTgNJFP");
        static PublicKey _orev2Id = new PublicKey("oreV2ZymfyeXgNgBdqMkumTqqAprVqgBWQfoYkrtKWQ");
        static PublicKey _oreMint = new PublicKey("oreoU2P8bN6jkk3jbaiVxYnG1dCXcYxwhwyK9jSybcp");
        static IRpcClient _client = ClientFactory.GetClient("https://api.mainnet-beta.solana.com");

        static async Task Main(string[] args)
        {
            if (!File.Exists("id.txt"))
            {
                Console.WriteLine("Save private key in a file called 'id.txt' in the same directory and restart");
                Console.ReadLine();

                return;
            }

            string walletText = File.ReadAllText("id.txt");

            //Json Wallet
            byte[] walletKeyPair = new byte[0];

            if (walletText.Trim().StartsWith("["))
            {
                walletKeyPair = JsonConvert.DeserializeObject<byte[]>(walletText);
            }
            else
            {
                Base58Encoder encoder = new Base58Encoder();
                walletKeyPair = encoder.DecodeData(walletText.Trim());
            }

            if (walletKeyPair == null || walletKeyPair.Length != 64)
            {
                Console.WriteLine("Invalid seed wallet");
                Console.ReadLine();

                return;
            }

            Wallet wallet = new Wallet(walletKeyPair, seedMode: SeedMode.Bip39);

            Account acc = wallet.Account;

            Console.WriteLine($"Found wallet: {acc.PublicKey}");

            PublicKey shareAccount = null;

            Console.Write("Enter share account key or RPC url to search: ");
            string accOrRPC = Console.ReadLine();

            //RPC, try finding
            if (Uri.TryCreate(accOrRPC.Trim(), UriKind.Absolute, out Uri result))
            {
                Console.WriteLine($"Searching program accounts for member key...");

                _client = ClientFactory.GetClient(result.AbsoluteUri); //Switch RPC

                Solnet.Rpc.Core.Http.RequestResult<List<AccountKeyPair>> programAccountResult = await _client.GetProgramAccountsAsync(_programId);

                if (!programAccountResult.WasSuccessful)
                {
                    Console.WriteLine($"Failed to pull program accounts with RPC. Reason: {programAccountResult.Reason}");
                    Console.ReadLine();

                    return;
                }


                //Find wallet
                foreach (var account in programAccountResult.Result)
                {
                    byte[] data = Convert.FromBase64String(account.Account.Data[0]);

                    if (data.Length != 88)
                    {
                        continue;
                    }

                    Member member = Member.ReadFrom(data);

                    if (member.Pool == acc.PublicKey)
                    {
                        shareAccount = new PublicKey(account.PublicKey);

                        Console.WriteLine($"Found member account: {shareAccount}");
                        break;
                    }
                }
            }
            else
            {
                Base58Encoder encoder = new Base58Encoder();
                byte[] keyData = encoder.DecodeData(accOrRPC.Trim());

                if (keyData.Length != 32)
                {
                    Console.WriteLine($"Invalid member account key");
                    Console.ReadLine();

                    return;
                }

                shareAccount = new PublicKey(keyData);
            }

            PublicKey.TryFindProgramAddress(new List<byte[]> { Encoding.UTF8.GetBytes("proof"), acc.PublicKey.KeyBytes }, _orev2Id, out PublicKey mainWalletProof, out byte nonce);
            PublicKey.TryFindProgramAddress(new List<byte[]> { Encoding.UTF8.GetBytes("proof"), shareAccount.KeyBytes }, _orev2Id, out PublicKey shareAccountProof, out byte _);

            var shareAccountOreToken = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(shareAccount, _oreMint);
            var mainWalletOreToken = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(acc.PublicKey, _oreMint);
            var treasuryOreToken = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(_oreTreasury, _oreMint);


            //Look at proof account
            var accountInfoResult = await _client.GetMultipleAccountsAsync(new List<string> { shareAccountProof, mainWalletOreToken });

            if (!accountInfoResult.WasSuccessful)
            {
                Console.WriteLine($"Failed to pull proof account information");
                Console.ReadLine();

                return;
            }

            Proof proof = Proof.ReadFrom(Convert.FromBase64String(accountInfoResult.Result.Value[0].Data[0]));

            if (proof.Balance == 0)
            {
                Console.WriteLine("Found no ore");

                return;
            }

            TokenAccountInfo tokenAccount = new TokenAccountInfo();

            if (accountInfoResult.Result.Value[1] == null)
            {
                Console.WriteLine("Found no ore associated token account");
                Console.ReadLine();
            }
            else
            {
                tokenAccount = TokenAccountInfo.ReadFrom(Convert.FromBase64String(accountInfoResult.Result.Value[1].Data[0]));
            }

            Console.WriteLine($"Found {tokenAccount.Amount / Math.Pow(10, 11)} ore currently in wallet");
            Console.WriteLine($"Found {proof.Balance / Math.Pow(10, 11)} ore");
            Console.WriteLine();

            //Build instruction
            byte[] transaction = await BuildTransaction(acc, mainWalletOreToken, shareAccount, shareAccountProof, shareAccountOreToken, mainWalletProof, treasuryOreToken, nonce);

            var results = await _client.SimulateTransactionAsync(transaction, commitment: Solnet.Rpc.Types.Commitment.Processed, accountsToReturn: new List<string> { mainWalletOreToken });

            Console.WriteLine($"Simulation results: ");

            if(!results.WasSuccessful)
            {
                Console.WriteLine($"Failed. Reason: {results.Reason}");
                Console.ReadLine();

                return;
            }

            if(results.Result.Value.Error != null)
            {
                Console.WriteLine($"Failed. Reason: {results.Result.Value.Error.InstructionError.CustomError}");
                Console.ReadLine();

                return;
            }

            var newTokenAccount = TokenAccountInfo.ReadFrom(Convert.FromBase64String(results.Result.Value.Accounts[0].Data[0]));

            Console.WriteLine($"Will receive: {(newTokenAccount.Amount - tokenAccount.Amount) / Math.Pow(10, 11)} ore");
            Console.WriteLine("Hit enter to send transaction");
            Console.ReadLine();

            Solnet.Rpc.Core.Http.RequestResult<string> transactionResult = await _client.SendTransactionAsync(transaction);

            Console.WriteLine($"Transaction hash: {transactionResult.Result}");

            Console.ReadLine();
        }


        #region Transaction

        private static async Task<byte[]> BuildTransaction(Account account, PublicKey mainWalletOreToken, PublicKey shareAccount,
            PublicKey shareAccountProof, PublicKey shareAccountOreToken, PublicKey mainWalletProof, PublicKey treasuryOreToken, byte nonce)
        {
            var blockHashResult = await _client.GetLatestBlockHashAsync(Solnet.Rpc.Types.Commitment.Finalized);

            var tx = new TransactionBuilder().SetRecentBlockHash(blockHashResult.Result.Value.Blockhash);
            tx.SetFeePayer(account.PublicKey);

            foreach (var instruction in GetComputeBudgetRatio(100000, 100))
            {
                tx.AddInstruction(instruction);
            }

            tx.AddInstruction(BuildInstruction(account, mainWalletOreToken, shareAccount, shareAccountProof, shareAccountOreToken, mainWalletProof, treasuryOreToken, nonce));

            tx.AddSignature(new byte[64]);

            return tx.Build(account);

        }

        private static TransactionInstruction BuildInstruction(PublicKey signer, PublicKey mainWalletOreToken, PublicKey shareAccount, 
            PublicKey shareAccountProof, PublicKey shareAccountOreToken, PublicKey mainWalletProof, PublicKey treasuryOreToken, byte nonce)
        {
            List<AccountMeta> keys = new()
            {
                AccountMeta.Writable(signer, true),
                AccountMeta.Writable(mainWalletOreToken, false),
                AccountMeta.Writable(signer, true),
                AccountMeta.Writable(signer, true),
                AccountMeta.Writable(shareAccount, false),
                AccountMeta.Writable(shareAccountProof, false),
                AccountMeta.Writable(shareAccountOreToken, false),
                AccountMeta.ReadOnly(_oreMint, false),
                AccountMeta.Writable(mainWalletProof, false),
                AccountMeta.ReadOnly(_oreTreasury, false),
                AccountMeta.Writable(treasuryOreToken, false),
                AccountMeta.ReadOnly(_orev2Id, false),
                AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
                AccountMeta.ReadOnly(TokenProgram.ProgramIdKey, false),
                AccountMeta.ReadOnly(AssociatedTokenAccountProgram.ProgramIdKey, false),
                AccountMeta.ReadOnly(new PublicKey("SysvarS1otHashes111111111111111111111111111"), false),



            };

            byte[] data = new byte[2];
            data[0] = 4;
            data[1] = nonce;

            return new TransactionInstruction
            {
                ProgramId = _programId,
                Keys = keys,
                Data = data
            };
        }

        private static List<TransactionInstruction> GetComputeBudgetRatio(uint computeUnits, uint ratio)
        {
            List<TransactionInstruction> instructions = new List<TransactionInstruction>();

            var methodBuffer = new byte[5];
            methodBuffer.WriteU8(2, 0);
            methodBuffer.WriteU32(computeUnits, 1);

            var computeBudgetTransaction = new TransactionInstruction
            {
                ProgramId = new PublicKey("ComputeBudget111111111111111111111111111111"),
                Data = methodBuffer,
                Keys = new List<AccountMeta>()
            };

            instructions.Add(computeBudgetTransaction);

            if (ratio > 0)
            {
                var methodBuffer2 = new byte[9];
                methodBuffer2.WriteU8(3, 0);
                methodBuffer2.WriteU32(ratio, 1);

                var computeBudgetTransaction2 = new TransactionInstruction
                {
                    ProgramId = new PublicKey("ComputeBudget111111111111111111111111111111"),
                    Data = methodBuffer2,
                    Keys = new List<AccountMeta>()
                };

                instructions.Add(computeBudgetTransaction2);
            }

            return instructions;
        }

        #endregion
    }
}
