﻿using System;
using MiningCore.Blockchain.Bitcoin;
using MiningCore.Configuration;
using MiningCore.Crypto;
using MiningCore.Crypto.Hashing.Algorithms;
using MiningCore.Crypto.Hashing.Special;
using MiningCore.Extensions;
using MiningCore.Stratum;
using MiningCore.Tests.Util;
using NBitcoin;
using Newtonsoft.Json;
using Xunit;

namespace MiningCore.Tests.Blockchain.Bitcoin
{
	public class BitcoinJobTests : TestBase
	{
		readonly PoolConfig poolConfig = new PoolConfig();
		readonly ClusterConfig clusterConfig = new ClusterConfig();
		private readonly IDestination poolAddressDestination = BitcoinUtils.AddressToScript("mjn3q42yxr9yLA3gyseHCZCHEptZC31PEh");

		protected readonly IHashAlgorithm sha256d = new Sha256D();
		protected readonly IHashAlgorithm sha256dReverse = new DigestReverser(new Sha256D());

		[Fact]
		public void BitcoinJob_Should_Accept_Valid_Share()
		{
			var worker = new StratumClient<BitcoinWorkerContext>
			{
				Context = new BitcoinWorkerContext
				{
					Difficulty = 0.5,
					ExtraNonce1 = "01000058",
				}
			};

			var bt = JsonConvert.DeserializeObject<MiningCore.Blockchain.Bitcoin.DaemonResponses.BlockTemplate>(
				"{\"Version\":536870912,\"PreviousBlockhash\":\"000000000909578519b5be7b37fdc53b2923817921c43108a907b72264da76bb\",\"CoinbaseValue\":5000000000,\"Target\":\"7fffff0000000000000000000000000000000000000000000000000000000000\",\"NonceRange\":\"00000000ffffffff\",\"CurTime\":1508869874,\"Bits\":\"207fffff\",\"Height\":14,\"Transactions\":[],\"CoinbaseAux\":{\"Flags\":\"0b2f454231362f414431322f\"},\"default_witness_commitment\":null}");

			var job = new BitcoinJob<MiningCore.Blockchain.Bitcoin.DaemonResponses.BlockTemplate>();

			// set clock to job creation time
			var clock = new MockMasterClock { CurrentTime = DateTimeOffset.FromUnixTimeSeconds(1508869874).UtcDateTime };

			job.Init(bt, "1", poolConfig, clusterConfig, clock, poolAddressDestination, BitcoinNetworkType.RegTest,
				new BitcoinExtraNonceProvider(), false, 1, sha256d, sha256d, sha256dReverse);

			// set clock to submission time
			clock.CurrentTime = DateTimeOffset.FromUnixTimeSeconds(1508869907).UtcDateTime;

			var share = job.ProcessShare(worker, "01000000", "59ef86f2", "8d84ae6a");

			Assert.NotNull(share);
			Assert.True(share.IsBlockCandidate);
			Assert.Equal(share.BlockHash, "000000000fccf11cd0b7d9057441e430c320384b95b034bd28092c4553594b4a");
			Assert.Equal(share.BlockHex, "00000020bb76da6422b707a90831c421798123293bc5fd377bbeb51985570909000000008677145722cbe6f1ebec19fecc724cab5487f3292a69f6908bd512f645bb0635f286ef59ffff7f206aae848d0101000000010000000000000000000000000000000000000000000000000000000000000000ffffffff295e0c0b2f454231362f414431322f04f286ef590801000058010000000c2f4d696e696e67436f72652f000000000100f2052a010000001976a9142ebb5cccf9a6bb927661d2953655c43c04accc3788ac00000000");
			Assert.Equal(share.BlockHeight, 14);
			Assert.Equal(share.BlockReward, 50);
			Assert.Equal(share.StratumDifficulty, 0.5);
		}

		[Fact]
		public void BitcoinJob_Should_Not_Accept_Invalid_Share()
		{
			var worker = new StratumClient<BitcoinWorkerContext>
			{
				Context = new BitcoinWorkerContext
				{
					Difficulty = 0.5,
					ExtraNonce1 = "01000058",
				}
			};

			var bt = JsonConvert.DeserializeObject<MiningCore.Blockchain.Bitcoin.DaemonResponses.BlockTemplate>(
				"{\"Version\":536870912,\"PreviousBlockhash\":\"000000000909578519b5be7b37fdc53b2923817921c43108a907b72264da76bb\",\"CoinbaseValue\":5000000000,\"Target\":\"7fffff0000000000000000000000000000000000000000000000000000000000\",\"NonceRange\":\"00000000ffffffff\",\"CurTime\":1508869874,\"Bits\":\"207fffff\",\"Height\":14,\"Transactions\":[],\"CoinbaseAux\":{\"Flags\":\"0b2f454231362f414431322f\"},\"default_witness_commitment\":null}");

			var job = new BitcoinJob<MiningCore.Blockchain.Bitcoin.DaemonResponses.BlockTemplate>();

			// set clock to job creation time
			var clock = new MockMasterClock { CurrentTime = DateTimeOffset.FromUnixTimeSeconds(1508869874).UtcDateTime };

			job.Init(bt, "1", poolConfig, clusterConfig, clock, poolAddressDestination, BitcoinNetworkType.RegTest,
				new BitcoinExtraNonceProvider(), false, 1, sha256d, sha256d, sha256dReverse);

			// set clock to submission time
			clock.CurrentTime = DateTimeOffset.FromUnixTimeSeconds(1508869907).UtcDateTime;

			// invalid extra-nonce 2
			Assert.Throws<StratumException>(() => job.ProcessShare(worker, "02000000", "59ef86f2", "8d84ae6a"));

			// invalid time
			Assert.Throws<StratumException>(() => job.ProcessShare(worker, "01000000", "69ef86f2", "8d84ae6a"));

			// invalid nonce
			Assert.Throws<StratumException>(() => job.ProcessShare(worker, "01000000", "59ef86f2", "ad84be6a"));

			// valid share data but invalid submission time
			clock.CurrentTime = DateTimeOffset.FromUnixTimeSeconds(1408869907).UtcDateTime;
			Assert.Throws<StratumException>(() => job.ProcessShare(worker, "01000000", "59ef86f2", "8d84ae6a"));
		}
	}
}
