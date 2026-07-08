////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
/// <summary>
/// 拆分的玩家仓库网络传输部分
/// </summary>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
#define LOG_INVENTORY_RPC_FAILURES
#endif // UNITY_EDITOR || DEVELOPMENT_BUILD

using SDG.NetPak;
using SDG.NetTransport;
using Steamworks;
using UnityEngine;
using Unturned.SystemEx;
namespace SDG.Unturned
{
	public partial class PlayerInventory
	{

		#region 网络服务

		private static readonly ServerInstanceMethod<byte, byte, byte, byte, byte, byte, byte> SendDragItem = ServerInstanceMethod<byte, byte, byte, byte, byte, byte, byte>.Get(typeof(PlayerInventory), nameof(ReceiveDragItem));
		[SteamCall(ESteamCallValidation.ONLY_FROM_OWNER, ratelimitHz = 10, legacyName = nameof(askDragItem))]
		public void ReceiveDragItem(byte page_0, byte x_0, byte y_0, byte page_1, byte x_1, byte y_1, byte rot_1)
		{
			if (player.equipment.checkSelection(page_0, x_0, y_0))
			{
				if (player.equipment.isBusy)
				{
					return;
				}

				player.equipment.dequip();
			}
			else if (player.equipment.checkSelection(page_1, x_1, y_1))
			{
				if (player.equipment.isBusy)
				{
					return;
				}

				player.equipment.dequip();
			}

			if (page_0 < 0 || page_0 >= PAGES - 1)
			{
				return;
			}

			if (items[page_0] == null)
			{
				return;
			}

			byte index = items[page_0].getIndex(x_0, y_0);

			if (index == 255)
			{
				return;
			}

			if (page_1 < 0 || page_1 >= PAGES - 1)
			{
				return;
			}

			if (items[page_1] == null)
			{
				return;
			}

			if (getItemCount(page_1) >= 200)
			{
				return;
			}

			ItemJar jar = items[page_0].getItem(index);

			if (jar == null)
			{
				return;
			}

			if (!checkSpaceDrag(page_1, x_0, y_0, jar.rot, x_1, y_1, rot_1, jar.size_x, jar.size_y, page_0 == page_1))
			{
				return;
			}

			ItemAsset asset = jar.GetAsset();

			if (asset == null)
			{
				return;
			}

			if (page_1 < SLOTS && asset.slot.canEquipInPage(page_1) == false)
			{
				return;
			}

			if (page_1 < SLOTS)
			{
				rot_1 = 0;
			}

			removeItem(page_0, index);
			items[page_1].addItem(x_1, y_1, rot_1, jar.item);

			if (page_0 < SLOTS)
			{
				player.equipment.sendSlot(page_0);
			}

			if (page_1 < SLOTS)
			{
				player.equipment.sendSlot(page_1);
			}
		}

		private static readonly ServerInstanceMethod<byte, byte, byte, byte, byte, byte, byte, byte> SendSwapItem = ServerInstanceMethod<byte, byte, byte, byte, byte, byte, byte, byte>.Get(typeof(PlayerInventory), nameof(ReceiveSwapItem));
		/// <summary>
		/// Swap coordinates of two existing items.
		/// Rotation is provided to handle differently shaped items e.g. a 1x2 item with a 2x1 item. 
		/// </summary>
		[SteamCall(ESteamCallValidation.ONLY_FROM_OWNER, ratelimitHz = 10, legacyName = nameof(askSwapItem))]
		public void ReceiveSwapItem(byte page_0, byte x_0, byte y_0, byte rot_0, byte page_1, byte x_1, byte y_1, byte rot_1)
		{
			//UnturnedLog.info("askSwapItem page_0: {0}, x_0: {1}, y_0: {2}, rot_0: {3}, page_1: {4}, x_1: {5}, y_1: {6}, rot_1: {7}", page_0, x_0, y_0, rot_0, page_1, x_1, y_1, rot_1);

			if (player.equipment.checkSelection(page_0, x_0, y_0))
			{
				if (player.equipment.isBusy)
				{
					return;
				}

				player.equipment.dequip();
			}
			else if (player.equipment.checkSelection(page_1, x_1, y_1))
			{
				if (player.equipment.isBusy)
				{
					return;
				}

				player.equipment.dequip();
			}

			if (page_0 == page_1 && x_0 == x_1 && y_0 == y_1 && rot_0 == rot_1)
			{
				LogRPCFailure("askSwapItem identical coordinates");
				return;
			}

			if (page_0 < 0 || page_0 >= PAGES - 1)
			{
				LogRPCFailure("askSwapItem invalid page_0");
				return;
			}

			if (items[page_0] == null)
			{
				LogRPCFailure("askSwapItem null page_0");
				return;
			}

			byte index_0 = items[page_0].getIndex(x_0, y_0);

			if (index_0 == 255)
			{
				LogRPCFailure("askSwapItem invalid index_0");
				return;
			}

			if (page_1 < 0 || page_1 >= PAGES - 1)
			{
				LogRPCFailure("askSwapItem invalid page_1");
				return;
			}

			if (items[page_1] == null)
			{
				LogRPCFailure("askSwapItem null page_1");
				return;
			}

			byte index_1 = items[page_1].getIndex(x_1, y_1);

			if (index_1 == 255)
			{
				LogRPCFailure("askSwapItem invalid index_1");
				return;
			}

			ItemJar jar_0 = items[page_0].getItem(index_0);

			if (jar_0 == null)
			{
				LogRPCFailure("askSwapItem null jar_0");
				return;
			}

			ItemJar jar_1 = items[page_1].getItem(index_1);

			if (jar_1 == null)
			{
				LogRPCFailure("askSwapItem null jar_1");
				return;
			}

			if (jar_0 == jar_1)
			{
				LogRPCFailure("askSwapItem jar_0 == jar_1 would duplicate the item, askDragItem should have been called");
				return;
			}

			if (!checkSpaceSwap(page_0, x_0, y_0, jar_0.size_x, jar_0.size_y, jar_0.rot, jar_1.size_x, jar_1.size_y, rot_0))
			{
				LogRPCFailure("askSwapItem first swap failed");
				return;
			}

			if (!checkSpaceSwap(page_1, x_1, y_1, jar_1.size_x, jar_1.size_y, jar_1.rot, jar_0.size_x, jar_0.size_y, rot_1))
			{
				LogRPCFailure("askSwapItem second swap failed");
				return;
			}

			ItemAsset asset_0 = jar_0.GetAsset();

			if (asset_0 == null)
			{
				LogRPCFailure("askSwapItem null asset_0");
				return;
			}

			if (page_1 < SLOTS && asset_0.slot.canEquipInPage(page_1) == false)
			{
				LogRPCFailure("askSwapItem asset_0 cannot equip in page_1");
				return;
			}

			ItemAsset asset_1 = jar_1.GetAsset();

			if (asset_1 == null)
			{
				LogRPCFailure("askSwapItem null asset_1");
				return;
			}

			if (page_0 < SLOTS && asset_1.slot.canEquipInPage(page_0) == false)
			{
				LogRPCFailure("askSwapItem asset_1 cannot equip in page_0");
				return;
			}

			removeItem(page_0, index_0);

			if (page_0 == page_1 && index_1 > index_0)
			{
				index_1--;
			}

			removeItem(page_1, index_1);

			// Items in equipment slots should not be rotated.
			if (page_0 < SLOTS)
			{
				rot_0 = 0;
			}
			if (page_1 < SLOTS)
			{
				rot_1 = 0;
			}

			items[page_0].addItem(x_0, y_0, rot_0, jar_1.item);
			items[page_1].addItem(x_1, y_1, rot_1, jar_0.item);

			if (page_0 < SLOTS)
			{
				player.equipment.sendSlot(page_0);
			}

			if (page_1 < SLOTS)
			{
				player.equipment.sendSlot(page_1);
			}
		}

		public void sendDragItem(byte page_0, byte x_0, byte y_0, byte page_1, byte x_1, byte y_1, byte rot_1)
		{
			SendDragItem.Invoke(GetNetId(), ENetReliability.Unreliable, page_0, x_0, y_0, page_1, x_1, y_1, rot_1);
		}

		/// <summary>
		/// Swap coordinates of two existing items.
		/// Rotation is provided to handle differently shaped items e.g. a 1x2 item with a 2x1 item. 
		/// </summary>
		public void sendSwapItem(byte page_0, byte x_0, byte y_0, byte rot_0, byte page_1, byte x_1, byte y_1, byte rot_1)
		{
			//UnturnedLog.info("sendSwapItem page_0: {0}, x_0: {1}, y_0: {2}, rot_0: {3}, page_1: {4}, x_1: {5}, y_1: {6}, rot_1: {7}", page_0, x_0, y_0, rot_0, page_1, x_1, y_1, rot_1);
			SendSwapItem.Invoke(GetNetId(), ENetReliability.Unreliable, page_0, x_0, y_0, rot_0, page_1, x_1, y_1, rot_1);
		}

		private static readonly ServerInstanceMethod<byte, byte, byte> SendDropItem = ServerInstanceMethod<byte, byte, byte>.Get(typeof(PlayerInventory), nameof(ReceiveDropItem));
		[SteamCall(ESteamCallValidation.ONLY_FROM_OWNER, ratelimitHz = 10, legacyName = nameof(askDropItem))]
		public void ReceiveDropItem(byte page, byte x, byte y)
		{
			if (player.equipment.checkSelection(page, x, y))
			{
				if (player.equipment.isBusy)
				{
					return;
				}

				player.equipment.dequip();
			}

			if (page < 0 || page >= PAGES - 1)
			{
				return;
			}

			if (items == null)
			{
				return;
			}

			if (items[page] == null)
			{
				return;
			}

			byte index = items[page].getIndex(x, y);

			if (index == 255)
			{
				return;
			}

			ItemJar jar = items[page].getItem(index);

			if (jar == null || jar.item == null)
			{
				return;
			}

			ItemAsset asset = jar.GetAsset();
			if (asset == null)
			{
				return;
			}

			bool shouldAllow = asset.allowManualDrop;
			onDropItemRequested?.Invoke(this, jar.item, ref shouldAllow);

			if (!shouldAllow)
			{
				return;
			}

			ItemManager.dropItem(jar.item, transform.position + (transform.forward * 0.5f), true, true, false);
			removeItem(page, index);

			if (page < SLOTS)
			{
				player.equipment.sendSlot(page);
			}
		}

		public void sendDropItem(byte page, byte x, byte y)
		{
			SendDropItem.Invoke(GetNetId(), ENetReliability.Unreliable, page, x, y);
		}

		private static readonly ClientInstanceMethod<byte, byte, byte> SendUpdateAmount = ClientInstanceMethod<byte, byte, byte>.Get(typeof(PlayerInventory), nameof(ReceiveUpdateAmount));
		[SteamCall(ESteamCallValidation.ONLY_FROM_SERVER, legacyName = nameof(tellUpdateAmount))]
		public void ReceiveUpdateAmount(byte page, byte index, byte amount)
		{
			updateAmount(page, index, amount);
		}

		private static readonly ClientInstanceMethod<byte, byte, byte> SendUpdateQuality = ClientInstanceMethod<byte, byte, byte>.Get(typeof(PlayerInventory), nameof(ReceiveUpdateQuality));
		[SteamCall(ESteamCallValidation.ONLY_FROM_SERVER, legacyName = nameof(tellUpdateQuality))]
		public void ReceiveUpdateQuality(byte page, byte index, byte quality)
		{
			updateQuality(page, index, quality);
		}

		private static readonly ClientInstanceMethod<byte, byte, byte[]> SendUpdateInvState = ClientInstanceMethod<byte, byte, byte[]>.Get(typeof(PlayerInventory), nameof(ReceiveUpdateInvState));
		[SteamCall(ESteamCallValidation.ONLY_FROM_SERVER, legacyName = nameof(tellUpdateInvState))]
		public void ReceiveUpdateInvState(byte page, byte index, byte[] state)
		{
			updateState(page, index, state);
		}

		private static readonly ClientInstanceMethod<byte, byte, byte, byte, ushort, byte, byte, byte[]> SendItemAdd
			= ClientInstanceMethod<byte, byte, byte, byte, ushort, byte, byte, byte[]>.Get(typeof(PlayerInventory), nameof(ReceiveItemAdd));
		[SteamCall(ESteamCallValidation.ONLY_FROM_SERVER, legacyName = nameof(tellItemAdd))]
		public void ReceiveItemAdd(byte page, byte x, byte y, byte rot, ushort id, byte amount, byte quality, byte[] state)
		{
			if (page >= PAGES || items == null || items[page] == null)
			{
				return;
			}

			items[page].addItem(x, y, rot, new Item(id, amount, quality, state));
		}

		private static readonly ClientInstanceMethod<byte, byte, byte> SendItemRemove = ClientInstanceMethod<byte, byte, byte>.Get(typeof(PlayerInventory), nameof(ReceiveItemRemove));
		[SteamCall(ESteamCallValidation.ONLY_FROM_SERVER, legacyName = nameof(tellItemRemove))]
		public void ReceiveItemRemove(byte page, byte x, byte y)
		{
			if (page >= PAGES || items == null || items[page] == null)
			{
				return;
			}

			byte index = items[page].getIndex(x, y);

			if (index == 255)
			{
				return;
			}

			items[page].removeItem(index);
		}

		private static readonly ClientInstanceMethod<byte, byte, byte> SendSize = ClientInstanceMethod<byte, byte, byte>.Get(typeof(PlayerInventory), nameof(ReceiveSize));
		[SteamCall(ESteamCallValidation.ONLY_FROM_SERVER, legacyName = nameof(tellSize))]
		public void ReceiveSize(byte page, byte newWidth, byte newHeight)
		{
			if (page >= PAGES || items == null || items[page] == null)
			{
				return;
			}

			items[page].resize(newWidth, newHeight);
		}

		private static readonly ClientInstanceMethod SendStoraging = ClientInstanceMethod.Get(typeof(PlayerInventory), nameof(ReceiveStoraging));
		[SteamCall(ESteamCallValidation.ONLY_FROM_SERVER)]
		public void ReceiveStoraging(in ClientInvocationContext context)
		{
			NetPakReader reader = context.reader;

			reader.ReadBit(out isStorageTrunk);

			byte newWidth;
			reader.ReadUInt8(out newWidth);
			byte newHeight;
			reader.ReadUInt8(out newHeight);
			items[STORAGE].resize(newWidth, newHeight);

			byte count;
			reader.ReadUInt8(out count);
			for (byte index = 0; index < count; index++)
			{
				byte x;
				reader.ReadUInt8(out x);
				byte y;
				reader.ReadUInt8(out y);
				byte rot;
				reader.ReadUInt8(out rot);

				ushort itemId;
				reader.ReadUInt16(out itemId);
				byte amount;
				reader.ReadUInt8(out amount);
				byte quality;
				reader.ReadUInt8(out quality);

				byte stateLength;
				reader.ReadUInt8(out stateLength);
				byte[] state = new byte[stateLength];
				reader.ReadBytes(state);

				items[STORAGE].addItem(x, y, rot, new Item(itemId, amount, quality, state));
			}

			isStoring = items[STORAGE].height > 0;

			if (isStoring)
			{
				onInventoryStored?.Invoke();
			}
		}

		private static readonly ClientInstanceMethod SendInventory = ClientInstanceMethod.Get(typeof(PlayerInventory), nameof(ReceiveInventory));
		[SteamCall(ESteamCallValidation.ONLY_FROM_SERVER)]
		public void ReceiveInventory(in ClientInvocationContext context)
		{
			Player.isLoadingInventory = false;

			NetPakReader reader = context.reader;

			for (byte page = 0; page < PAGES - 2; page++)
			{
				byte newWidth;
				reader.ReadUInt8(out newWidth);
				byte newHeight;
				reader.ReadUInt8(out newHeight);
				items[page].resize(newWidth, newHeight);

				byte count;
				reader.ReadUInt8(out count);

				for (byte index = 0; index < count; index++)
				{
					byte x;
					reader.ReadUInt8(out x);
					byte y;
					reader.ReadUInt8(out y);
					byte rot;
					reader.ReadUInt8(out rot);

					ushort assetId;
					reader.ReadUInt16(out assetId);
					byte amount;
					reader.ReadUInt8(out amount);
					byte quality;
					reader.ReadUInt8(out quality);

					byte stateLength;
					reader.ReadUInt8(out stateLength);
					byte[] state = new byte[stateLength];
					reader.ReadBytes(state);

					items[page].addItem(x, y, rot, new Item(assetId, amount, quality, state));
				}
			}
		}

		internal void SendInitialPlayerState(SteamPlayer client)
		{
			if (channel.IsLocalPlayer) // Singleplayer
			{
				Player.isLoadingInventory = false;

				for (byte page = 0; page < PAGES - 2; page++)
				{
					onInventoryResized?.Invoke(page, items[page].width, items[page].height);

					for (byte index = 0; index < items[page].getItemCount(); index++)
					{
						ItemJar jar = items[page].getItem(index);
						onItemAdded(page, index, jar);
					}
				}
			}
			else if (client == channel.owner)
			{
				SendInventory.Invoke(GetNetId(), ENetReliability.Reliable, client.transportConnection, SendInventory_Write);
			}
			ownerHasInventory = true;
		}

		private void SendInventory_Write(NetPakWriter writer)
		{
			for (byte page = 0; page < PAGES - 2; page++)
			{
				writer.WriteUInt8(items[page].width);
				writer.WriteUInt8(items[page].height);
				writer.WriteUInt8(items[page].getItemCount());

				for (byte index = 0; index < items[page].getItemCount(); index++)
				{
					ItemJar jar = items[page].getItem(index);
					writer.WriteUInt8(jar.x);
					writer.WriteUInt8(jar.y);
					writer.WriteUInt8(jar.rot);
					writer.WriteUInt16(jar.item.id);
					writer.WriteUInt8(jar.item.amount);
					writer.WriteUInt8(jar.item.quality);
					writer.WriteUInt8((byte) jar.item.state.Length);
					writer.WriteBytes(jar.item.state);
				}
			}
		}

		public void sendStorage()
		{
			if (channel.IsLocalPlayer)
			{
				onInventoryResized(STORAGE, items[STORAGE].width, items[STORAGE].height);

				if (items[STORAGE].height > 0)
				{
					onInventoryStored?.Invoke();
				}

				for (byte index = 0; index < items[STORAGE].getItemCount(); index++)
				{
					ItemJar jar = items[STORAGE].getItem(index);
					onItemAdded(STORAGE, index, jar);
				}
			}
			else
			{
				SendStoraging.Invoke(GetNetId(), ENetReliability.Reliable, channel.owner.transportConnection, SendStoraging_Write);
			}
		}

		private void SendStoraging_Write(NetPakWriter writer)
		{
			writer.WriteBit(isStorageTrunk);
			writer.WriteUInt8(items[STORAGE].width);
			writer.WriteUInt8(items[STORAGE].height);
			writer.WriteUInt8(items[STORAGE].getItemCount());

			for (byte index = 0; index < items[STORAGE].getItemCount(); index++)
			{
				ItemJar jar = items[STORAGE].getItem(index);
				writer.WriteUInt8(jar.x);
				writer.WriteUInt8(jar.y);
				writer.WriteUInt8(jar.rot);
				writer.WriteUInt16(jar.item.id);
				writer.WriteUInt8(jar.item.amount);
				writer.WriteUInt8(jar.item.quality);
				writer.WriteUInt8((byte) jar.item.state.Length);
				writer.WriteBytes(jar.item.state);
			}
		}

		#endregion

		public void sendUpdateAmount(byte page, byte x, byte y, byte amount)
		{
			byte index = getIndex(page, x, y);

			updateAmount(page, index, amount);

			if (!channel.IsLocalPlayer && ownerHasInventory)
			{
				SendUpdateAmount.Invoke(GetNetId(), ENetReliability.Reliable, channel.GetOwnerTransportConnection(), page, index, amount);
			}
		}

		public void sendUpdateQuality(byte page, byte x, byte y, byte quality)
		{
			byte index = getIndex(page, x, y);

			updateQuality(page, index, quality);

			if (!channel.IsLocalPlayer && ownerHasInventory)
			{
				SendUpdateQuality.Invoke(GetNetId(), ENetReliability.Reliable, channel.GetOwnerTransportConnection(), page, index, quality);
			}
		}

		public void sendUpdateInvState(byte page, byte x, byte y, byte[] state)
		{
			byte index = getIndex(page, x, y);

			updateState(page, index, state);

			if (!channel.IsLocalPlayer && ownerHasInventory)
			{
				SendUpdateInvState.Invoke(GetNetId(), ENetReliability.Reliable, channel.GetOwnerTransportConnection(), page, index, state);
			}
		}

		private void sendItemAdd(byte page, ItemJar jar)
		{
			SendItemAdd.Invoke(GetNetId(), ENetReliability.Reliable, channel.GetOwnerTransportConnection(), page, jar.x, jar.y, jar.rot, jar.item.id, jar.item.amount, jar.item.quality, jar.item.state);
		}

		private void sendItemRemove(byte page, ItemJar jar)
		{
			SendItemRemove.Invoke(GetNetId(), ENetReliability.Reliable, channel.GetOwnerTransportConnection(), page, jar.x, jar.y);
		}

/// <summary>
/// 未来对弃用网络api的处理
/// 如果本项目未来做到对未转变者底层代码的完全重写，不应保留任何对旧版本的兼容
/// 直接使用新版的网络api即可
/// </summary>
/// 
		#region OBSOLETE
		[System.Obsolete]
		public void askDragItem(CSteamID steamID, byte page_0, byte x_0, byte y_0, byte page_1, byte x_1, byte y_1, byte rot_1)
		{
			ReceiveDragItem(page_0, x_0, y_0, page_1, x_1, y_1, rot_1);
		}

		[System.Obsolete]
		public void askSwapItem(CSteamID steamID, byte page_0, byte x_0, byte y_0, byte rot_0, byte page_1, byte x_1, byte y_1, byte rot_1)
		{
			ReceiveSwapItem(page_0, x_0, y_0, rot_0, page_1, x_1, y_1, rot_1);
		}

		[System.Obsolete]
		public void askDropItem(CSteamID steamID, byte page, byte x, byte y)
		{
			ReceiveDropItem(page, x, y);
		}

		[System.Obsolete]
		public void tellUpdateAmount(CSteamID steamID, byte page, byte index, byte amount)
		{
			ReceiveUpdateAmount(page, index, amount);
		}

		[System.Obsolete]
		public void tellUpdateQuality(CSteamID steamID, byte page, byte index, byte quality)
		{
			ReceiveUpdateQuality(page, index, quality);
		}

		[System.Obsolete]
		public void tellUpdateInvState(CSteamID steamID, byte page, byte index, byte[] state)
		{
			ReceiveUpdateInvState(page, index, state);
		}

		[System.Obsolete]
		public void tellItemAdd(CSteamID steamID, byte page, byte x, byte y, byte rot, ushort id, byte amount, byte quality, byte[] state)
		{
			ReceiveItemAdd(page, x, y, rot, id, amount, quality, state);
		}

		[System.Obsolete]
		public void tellItemRemove(CSteamID steamID, byte page, byte x, byte y)
		{
			ReceiveItemRemove(page, x, y);
		}

		[System.Obsolete]
		public void tellSize(CSteamID steamID, byte page, byte newWidth, byte newHeight)
		{
			ReceiveSize(page, newWidth, newHeight);
		}

		[System.Obsolete]
		public void tellStoraging(CSteamID steamID)
		{ }

		[System.Obsolete]
		public void tellInventory(CSteamID steamID)
		{ }

		[System.Obsolete]
		public void askInventory(CSteamID steamID)
		{ }

		[System.Diagnostics.Conditional("LOG_INVENTORY_RPC_FAILURES")]
		private void LogRPCFailure(string format, params object[] args)
		{
			UnturnedLog.warn(format, args);
		}
		#endregion OBSOLETE
	}
}
