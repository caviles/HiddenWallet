﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;

namespace HiddenWallet.FullSpv.MemPool
{
	public class MemPoolBehavior : NodeBehavior
	{
		const int MAX_INV_SIZE = 50000;

        public MemPoolJob MemPoolJob { get; }
        public MemPoolBehavior(MemPoolJob memPoolJob)
        {
            MemPoolJob = memPoolJob ?? throw new ArgumentNullException(nameof(memPoolJob));
        }

		protected override void AttachCore()
		{
			AttachedNode.MessageReceived += AttachedNode_MessageReceivedAsync;
		}

		protected override void DetachCore()
		{
			AttachedNode.MessageReceived -= AttachedNode_MessageReceivedAsync;
		}

		private async void AttachedNode_MessageReceivedAsync(Node node, IncomingMessage message)
		{
			if (MemPoolJob.ForcefullyStopped) return;
			if (!MemPoolJob.Enabled) return;

			try
			{
				if (message.Message.Payload is TxPayload txPayload)
				{
					ProcessTxPayload(txPayload);
					return;
				}

				if (message.Message.Payload is InvPayload invPayload)
				{
                    await ProcessInvAsync(node, invPayload);
					return;
				}
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Ignoring {nameof(MemPoolJob)} exception:");
				Debug.WriteLine(ex);
				return;
			}
		}

		private async Task ProcessInvAsync(Node node, InvPayload invPayload)
		{
			if (invPayload.Inventory.Count > MAX_INV_SIZE) return;

			var send = new GetDataPayload();
			foreach (var inv in invPayload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_TX)))
			{
				if (MemPoolJob.Transactions.Contains(inv.Hash))
					continue;

				send.Inventory.Add(inv);
			}

			if (node.IsConnected)
				await node.SendMessageAsync(send);
		}

		private void ProcessTxPayload(TxPayload transactionPayload)
		{
			Transaction tx = transactionPayload.Object;
			MemPoolJob.TryAddNewTransaction(tx);
		}

		public override object Clone()
		{
			return new MemPoolBehavior(MemPoolJob);
		}
	}
}
