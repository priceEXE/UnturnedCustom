////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
#if UNITY_EDITOR || DEVELOPMENT_BUILD
#define LOG_INVENTORY_RPC_FAILURES
#endif // UNITY_EDITOR || DEVELOPMENT_BUILD

using SDG.NetPak;
using SDG.NetTransport;
using Steamworks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Unturned.SystemEx;
namespace SDG.Unturned
{
	public delegate void InventoryResized(byte page, byte newWidth, byte newHeight);
	public delegate void InventoryUpdated(byte page, byte index, ItemJar jar);
	public delegate void InventoryAdded(byte page, byte index, ItemJar jar);
	public delegate void InventoryRemoved(byte page, byte index, ItemJar jar);
	public delegate void InventoryStored();
	public delegate void InventoryStateUpdated();

	public delegate void DropItemRequestHandler(PlayerInventory inventory, Item item, ref bool shouldAllow);

	public partial class PlayerInventory : PlayerCaller
	{
		public static readonly ushort[] LOADOUT = { };//{253, 380, 381, 381, 381, 381, 381, 31, 31, 35, 322, 36, 36, 36, 36, 36, 36, 33, 33, 33, 33, 32, 34, 319, 443, 443, 443, 443, 442, 442, 442, 442, 316};
		public static readonly ushort[] HORDE = { 97, 98, 98, 98 };
		public static readonly ushort[][] SKILLSETS_SERVER = new ushort[11][] { new ushort[0] { }, new ushort[0] { }, new ushort[0] { }, new ushort[0] { }, new ushort[0] { }, new ushort[0] { }, new ushort[0] { }, new ushort[0] { }, new ushort[0] { }, new ushort[0] { }, new ushort[0] { } };
		public static readonly ushort[][] SKILLSETS_CLIENT = new ushort[11][]
		{
			new ushort[2] {180, 214}, // none
			new ushort[3] {233, 234, 241}, // fire
			new ushort[3] {223, 224, 225}, // police
			new ushort[2] {1171, 1172}, // army
			new ushort[3] {242, 243, 244}, // farm
			new ushort[3] {510, 511, 509}, // fish
			new ushort[2] {211, 213}, // camp
			new ushort[3] {232, 2, 240}, // work
			new ushort[3] {230, 231, 239}, // chef
			new ushort[2] {1156, 1157}, // thief
			new ushort[2] {311, 312} // medic
		};
		public static readonly ushort[][] SKILLSETS_HERO = new ushort[11][]
		{
			new ushort[2] {180, 214}, // none
			new ushort[4] {233, 234, 241, 104}, // fire
			new ushort[6] {223, 224, 225, 10, 112, 99}, // police
			new ushort[6] {1171, 1172, 1169, 334, 297, 1027}, // army
			new ushort[5] {242, 243, 244, 101, 1034}, // farm
			new ushort[3] {510, 511, 509}, // fish
			new ushort[3] {211, 213, 16}, // camp
			new ushort[4] {232, 2, 240, 138}, // work
			new ushort[4] {230, 231, 239, 137}, // chef
			new ushort[5] {1156, 1157, 434, 122, 1036}, // thief
			new ushort[2] {311, 312} // medic
		};

		public static readonly byte SAVEDATA_VERSION = 5;

		public static readonly byte SLOTS = 2;
		public static readonly byte PAGES = 9;
		public static readonly byte BACKPACK = 3;
		public static readonly byte VEST = 4;
		public static readonly byte SHIRT = 5;
		public static readonly byte PANTS = 6;
		public static readonly byte STORAGE = 7;
		public static readonly byte AREA = 8;
		// 0	Primary
		// 1 	Secondary
		// 2	Hands
		// 3	Backpack
		// 4	Vest
		// 5	Shirt
		// 6	Pants
		// 7 	Storage

		public static ushort[] loadout = LOADOUT;

		public static ushort[][] skillsets = SKILLSETS_SERVER;

		//private ItemJar primary;
		//private ItemJar secondary;
		public Items[] items
		{
			get;
			private set;
		}

		/// <summary>
		/// Every time the inventory changes this number is incremented.
		/// While a little messy, the idea is to prevent inventory checks from happening every frame.
		/// </summary>
		protected int receivedUpdateIndex;

		/// <summary>
		/// Should be called every time something changes in the inventory.
		/// </summary>
		protected void incrementUpdateIndex()
		{
			receivedUpdateIndex++;
		}

		/// <summary>
		/// Helper to prevent checking the inventory every frame for systems that don't use events.
		/// </summary>
		public bool doesSearchNeedRefresh(ref int index)
		{
			if (index == receivedUpdateIndex)
			{
				return false;
			}
			else
			{
				index = receivedUpdateIndex;
				return true;
			}
		}

		public bool isStoring;
		public bool isStorageTrunk;
		public InteractableStorage storage;

		/// <summary>
		/// Did owner call askInventory yet?
		/// Prevents duplicate tell_X RPCs from being sent to owner prior to initial sync.
		/// Ideally should be cleaned up with netcode refactor. (Client should not need to ask server for initial state.)
		/// </summary>
		private bool ownerHasInventory;

		public bool shouldInventoryStopGestureCloseStorage => !isStorageTrunk;

		public bool shouldInteractCloseStorage => !isStorageTrunk;

		public bool shouldStorageOpenDashboard => !isStorageTrunk;

		public InventoryResized onInventoryResized;
		public InventoryUpdated onInventoryUpdated;
		public InventoryAdded onInventoryAdded;
		public InventoryRemoved onInventoryRemoved;
		public InventoryStored onInventoryStored;
		public InventoryStateUpdated onInventoryStateUpdated;
		public DropItemRequestHandler onDropItemRequested;

		#region 本地类之间的数据接口

		public byte getWidth(byte page)
		{
			if (page < items.Length)
			{
				return items[page].width;
			}
			else
			{
				return 0;
			}
		}

		public byte getHeight(byte page)
		{
			if (page < items.Length)
			{
				return items[page].height;
			}
			else
			{
				return 0;
			}
		}

		public byte getItemCount(byte page)
		{
			if (page < items.Length)
			{
				return items[page].getItemCount();
			}
			else
			{
				return 0;
			}
		}

		public ItemJar getItem(byte page, byte index)
		{
			if (page < items.Length)
			{
				return items[page].getItem(index);
			}
			else
			{
				return null;
			}
		}

		public byte getIndex(byte page, byte x, byte y)
		{
			if (page < items.Length)
			{
				return items[page].getIndex(x, y);
			}
			else
			{
				return byte.MaxValue;
			}
		}

		public byte findIndex(byte page, byte x, byte y, out byte find_x, out byte find_y)
		{
			find_x = 255;
			find_y = 255;

			return items[page].findIndex(x, y, out find_x, out find_y);
		}

		public void updateAmount(byte page, byte index, byte newAmount)
		{
			if (page >= PAGES || items == null || items[page] == null)
			{
				return;
			}

			items[page].updateAmount(index, newAmount);
		}

		public void updateQuality(byte page, byte index, byte newQuality)
		{
			if (page >= PAGES || items == null || items[page] == null)
			{
				return;
			}

			items[page].updateQuality(index, newQuality);

			ItemJar jar = items[page].getItem(index);
			if (jar != null && player.equipment.checkSelection(page, jar.x, jar.y))
			{
				player.equipment.quality = newQuality;
			}
		}

		public void updateState(byte page, byte index, byte[] newState)
		{
			if (page >= PAGES || items == null || items[page] == null)
			{
				return;
			}

			items[page].updateState(index, newState);
		}

		#endregion

		#region 辅助方法

		public void SearchContents(in PlayerInventorySearchParameters parameters)
		{
			Profiler.BeginSample("PlayerInventory.SearchContents");
			byte startPageInclusive = parameters.IncludeEquipmentSlots ? (byte) 0 : SLOTS;
			byte endPageInclusive = parameters.IncludeActiveStorageContainer ? STORAGE : (byte) (STORAGE - 1);
			for (byte page = startPageInclusive; page <= endPageInclusive; ++page)
			{
				Items pageItems = items[page];
				if (pageItems == null)
				{
					// Doesn't seem like this happens in older code? Good to be safe.
					continue;
				}

				pageItems.SearchContents(in parameters);

				if (parameters.MaxResultsCount > 0 && parameters.Results.Count >= parameters.MaxResultsCount)
				{
					break;
				}
			}
			Profiler.EndSample();
		}

		/// <summary>
		/// Intended as nearly a drop-in replacement for <see cref="PlayerInventory.search(List{InventorySearch}, EItemType)"/>.
		/// </summary>
		public void FindItemsByType(List<PlayerInventorySearchResultV2> results, EItemType type)
		{
			PlayerInventorySearchParameters searchParameters = new PlayerInventorySearchParameters()
			{
				Results = results,
				IncludeEquipmentSlots = false,
				IncludeActiveStorageContainer = false,
				ItemType = type,
				IncludeEmpty = false,
				IncludeMaxQuality = true,
			};
			SearchContents(in searchParameters);
		}

		/// <summary>
		/// Intended as nearly a drop-in replacement for <see cref="PlayerInventory.search(EItemType, ushort[], bool)"/>.
		/// </summary>
		public void FindAttachmentsByCaliber(List<PlayerInventorySearchResultV2> results,
			EItemType itemType, ushort[] anyCaliberIds, bool includeUnspecifiedCaliber)
		{
			PlayerInventorySearchParameters searchParameters = new PlayerInventorySearchParameters()
			{
				Results = results,
				IncludeEquipmentSlots = false,
				IncludeActiveStorageContainer = false,
				ItemType = itemType,
				IncludeEmpty = false,
				IncludeMaxQuality = true,
				AnyCaliberIds = anyCaliberIds,
				IncludeUnspecifiedCaliber = includeUnspecifiedCaliber,
			};
			SearchContents(in searchParameters);
		}

		/// <summary>
		/// Intended as nearly a drop-in replacement for <see cref="PlayerInventory.search(List{InventorySearch}, EItemType, ushort, bool)"/>.
		/// </summary>
		public void FindAttachmentsByCaliber(List<PlayerInventorySearchResultV2> results,
			EItemType itemType, ushort caliberId, bool includeUnspecifiedCaliber)
		{
			PlayerInventorySearchParameters searchParameters = new PlayerInventorySearchParameters()
			{
				Results = results,
				IncludeEquipmentSlots = false,
				IncludeActiveStorageContainer = false,
				ItemType = itemType,
				IncludeEmpty = false,
				IncludeMaxQuality = true,
				CaliberId = caliberId,
				IncludeUnspecifiedCaliber = includeUnspecifiedCaliber,
			};
			SearchContents(in searchParameters);
		}

		/// <summary>
		/// Intended as nearly a drop-in replacement for <see cref="PlayerInventory.search(List{InventorySearch}, ushort, bool, bool)"/>.
		/// </summary>
		public void FindItemsByAsset(List<PlayerInventorySearchResultV2> results,
			CachingBcAssetRef assetRef, bool includeEmpty, bool includeMaxQuality)
		{
			PlayerInventorySearchParameters searchParameters = new PlayerInventorySearchParameters()
			{
				Results = results,
				IncludeEquipmentSlots = false,
				IncludeActiveStorageContainer = false,
				AssetRef = assetRef,
				IncludeEmpty = includeEmpty,
				IncludeMaxQuality = includeMaxQuality,
			};
			SearchContents(in searchParameters);
		}

		/// <summary>
		/// Intended as nearly a drop-in replacement for <see cref="PlayerInventory.has(ushort)"/>.
		/// </summary>
		public void FindFirstItemByAsset(List<PlayerInventorySearchResultV2> results, CachingBcAssetRef assetRef)
		{
			PlayerInventorySearchParameters searchParameters = new PlayerInventorySearchParameters()
			{
				Results = results,
				IncludeEquipmentSlots = true,
				IncludeActiveStorageContainer = true,
				MaxResultsCount = 1,
				AssetRef = assetRef,
				IncludeEmpty = false,
				IncludeMaxQuality = true,
			};
			SearchContents(in searchParameters);
		}

		/// <summary>
		/// Intended as nearly a drop-in replacement for <see cref="PlayerInventory.has(ushort)"/>.
		/// This variant wraps FindFirstItemByAsset and manages the results list for you.
		/// Only use result if true is returned, otherwise it's invalid.
		/// </summary>
		public bool FindFirstItemByAsset(CachingBcAssetRef assetRef, out PlayerInventorySearchResultV2 result)
		{
			using (ScopedPlayerInventorySearchResultPool scopedSearch = new ScopedPlayerInventorySearchResultPool())
			{
				FindFirstItemByAsset(scopedSearch.PooledResults, assetRef);
				if (scopedSearch.PooledResults.Count > 0)
				{
					result = scopedSearch.PooledResults[0];
					return true;
				}
				else
				{
					result = default;
					return false;
				}
			}
		}

		/// <summary>
		/// Intended as nearly a drop-in replacement for <see cref="PlayerInventory.has(ushort)"/>.
		/// This variant wraps FindFirstItemByAsset and manages the results list for you.
		/// </summary>
		public bool HasItemByAsset(CachingBcAssetRef assetRef)
		{
			using (ScopedPlayerInventorySearchResultPool scopedSearch = new ScopedPlayerInventorySearchResultPool())
			{
				FindFirstItemByAsset(scopedSearch.PooledResults, assetRef);
				return scopedSearch.PooledResults.Count > 0;
			}
		}

		public bool tryAddItem(Item item, byte x, byte y, byte page, byte rot)
		{
			if (page >= PAGES - 1)
			{
				return false;
			}

			if (item == null)
			{
				return false;
			}

			ItemAsset asset = item.GetAsset();

			if (asset == null || asset.isPro)
			{
				return false;
			}

			if (page < SLOTS && asset.slot.canEquipInPage(page) == false)
			{
				return false;
			}

			if (page < SLOTS)
			{
				rot = 0;
			}

			if (x == 255 && y == 255)
			{
				if (!items[page].tryAddItem(item))
				{
					return false;
				}
			}
			else
			{
				if (items[page].getItemCount() >= 200)
				{
					return false;
				}

				if (!items[page].checkSpaceEmpty(x, y, asset.size_x, asset.size_y, rot))
				{
					return false;
				}

				items[page].addItem(x, y, rot, item);
			}

			if (page < SLOTS)
			{
				player.equipment.sendSlot(page);
			}

			return true;
		}

		public bool tryAddItem(Item item, bool auto)
		{
			return tryAddItem(item, auto, true);
		}

		public bool tryAddItem(Item item, bool auto, bool playEffect)
		{
			return tryAddItemAuto(item, auto, auto, auto, playEffect);
		}

		/// <summary>
		/// Helper for tryAddItemAuto.
		/// </summary>
		private bool tryAddItemEquip(Item item, byte page)
		{
			if (items[page].tryAddItem(item))
			{
				player.equipment.sendSlot(page);

				if (player.equipment.HasValidUseable == false)
				{
					player.equipment.ServerEquip(page, 0, 0);
				}

				return true;
			}

			return false;
		}

		public bool tryAddItemAuto(Item item, bool autoEquipWeapon, bool autoEquipUseable, bool autoEquipClothing, bool playEffect)
		{
			if (item == null)
			{
				return false;
			}

			ItemAsset asset = item.GetAsset();

			if (asset == null || asset.isPro)
			{
				return false;
			}

			if (autoEquipWeapon && asset.canPlayerEquip)
			{
				// We try to equip as secondary before trying to equip as primary
				// because when picking up a pistol and then a rifle we want the pistol
				// to have gone into the secondary slot so that we can equip the rifle.
				if (asset.slot.canEquipAsSecondary())
				{
					if (tryAddItemEquip(item, 1))
					{
						return true;
					}
				}

				if (asset.slot.canEquipAsPrimary())
				{
					if (tryAddItemEquip(item, 0))
					{
						return true;
					}
				}
			}

			if (autoEquipClothing)
			{
				if (player.clothing.hatAsset == null && asset.type == EItemType.HAT)
				{
					player.clothing.askWearHat(item.id, item.quality, item.state, playEffect);

					return true;
				}
				else if (player.clothing.shirtAsset == null && asset.type == EItemType.SHIRT)
				{
					player.clothing.askWearShirt(item.id, item.quality, item.state, playEffect);

					return true;
				}
				else if (player.clothing.pantsAsset == null && asset.type == EItemType.PANTS)
				{
					player.clothing.askWearPants(item.id, item.quality, item.state, playEffect);

					return true;
				}
				else if (player.clothing.backpackAsset == null && asset.type == EItemType.BACKPACK)
				{
					player.clothing.askWearBackpack(item.id, item.quality, item.state, playEffect);

					return true;
				}
				else if (player.clothing.vestAsset == null && asset.type == EItemType.VEST)
				{
					player.clothing.askWearVest(item.id, item.quality, item.state, playEffect);

					return true;
				}
				else if (player.clothing.maskAsset == null && asset.type == EItemType.MASK)
				{
					player.clothing.askWearMask(item.id, item.quality, item.state, playEffect);

					return true;
				}
				else if (player.clothing.glassesAsset == null && asset.type == EItemType.GLASSES)
				{
					player.clothing.askWearGlasses(item.id, item.quality, item.state, playEffect);

					return true;
				}
			}

			for (byte page = SLOTS; page < PAGES - 2; page++)
			{
				if (items[page].tryAddItem(item))
				{
					if (autoEquipUseable)
					{
						if (!player.equipment.HasValidUseable && asset.slot.canEquipInPage(page) && asset.canPlayerEquip)
						{
							ItemJar jar = items[page].getItem((byte) (items[page].getItemCount() - 1));

							player.equipment.ServerEquip(page, jar.x, jar.y);
						}
					}

					return true;
				}
			}

			return false;
		}

		public void forceAddItem(Item item, bool auto)
		{
			forceAddItemAuto(item, auto, auto, auto);
		}

		public void forceAddItemAuto(Item item, bool autoEquipWeapon, bool autoEquipUseable, bool autoEquipClothing)
		{
			forceAddItemAuto(item, autoEquipWeapon, autoEquipUseable, autoEquipClothing, true);
		}

		public void forceAddItem(Item item, bool auto, bool playEffect)
		{
			if (!tryAddItemAuto(item, auto, auto, auto, playEffect))
			{
				ItemManager.dropItem(item, transform.position, false, true, true);
			}
		}

		public void forceAddItemAuto(Item item, bool autoEquipWeapon, bool autoEquipUseable, bool autoEquipClothing, bool playEffect)
		{
			if (!tryAddItemAuto(item, autoEquipWeapon, autoEquipUseable, autoEquipClothing, playEffect))
			{
				ItemManager.dropItem(item, transform.position, false, true, true);
			}
		}

		public void replaceItems(byte page, Items replacement)
		{
			items[page] = replacement;
		}

		public void removeItem(byte page, byte index)
		{
			items[page].removeItem(index);
		}

		public bool checkSpaceEmpty(byte page, byte x, byte y, byte size_x, byte size_y, byte rot)
		{
			if (page < 0 || page >= PAGES)
			{
				return false;
			}

			return items[page].checkSpaceEmpty(x, y, size_x, size_y, rot);
		}

		public bool checkSpaceDrag(byte page, byte old_x, byte old_y, byte oldRot, byte new_x, byte new_y, byte newRot, byte size_x, byte size_y, bool checkSame)
		{
			if (page < 0 || page >= PAGES)
			{
				return false;
			}

			return items[page].checkSpaceDrag(old_x, old_y, oldRot, new_x, new_y, newRot, size_x, size_y, checkSame);
		}

		/// <summary>
		/// Given an item coordinate (page, x, y) could a new item take the place of an old (existing) item without
		/// overlapping other item(s) space? Always true for equipment slots (page less than SLOTS).
		/// For example if oldSize is (1, 2) rot 0, and newSize is (2, 1) rot 1, then they can swap.
		/// </summary>
		public bool checkSpaceSwap(byte page, byte x, byte y, byte oldSize_X, byte oldSize_Y, byte oldRot, byte newSize_X, byte newSize_Y, byte newRot)
		{
			if (page < 0 || page >= PAGES)
			{
				return false;
			}

			return items[page].checkSpaceSwap(x, y, oldSize_X, oldSize_Y, oldRot, newSize_X, newSize_Y, newRot);
		}

		public bool tryFindSpace(byte page, byte size_x, byte size_y, out byte x, out byte y, out byte rot)
		{
			x = 0;
			y = 0;
			rot = 0;

			if (page < 0 || page >= PAGES)
			{
				return false;
			}

			return items[page].tryFindSpace(size_x, size_y, out x, out y, out rot);
		}

		public bool tryFindSpace(byte size_x, byte size_y, out byte page, out byte x, out byte y, out byte rot)
		{
			x = 0;
			y = 0;
			rot = 0;

			for (page = SLOTS; page < PAGES - 1; page++)
			{
				if (items[page].tryFindSpace(size_x, size_y, out x, out y, out rot))
				{
					return true;
				}
			}

			return false;
		}

		#endregion

		public void updateItems(byte page, Items newItems)
		{
			if (items[page] != null)
			{
				items[page].onItemsResized -= onItemsResized;
				items[page].onItemUpdated -= onItemUpdated;
				items[page].onItemAdded -= onItemAdded;
				items[page].onItemRemoved -= onItemRemoved;
				items[page].onStateUpdated -= onItemStateUpdated;
			}

			if (newItems != null)
			{
				items[page] = newItems;
				items[page].onItemsResized += onItemsResized;
				items[page].onItemUpdated += onItemUpdated;
				items[page].onItemAdded += onItemAdded;
				items[page].onItemRemoved += onItemRemoved;
				items[page].onStateUpdated += onItemStateUpdated;
			}
			else
			{
				items[page] = new Items(STORAGE);
				items[page].onItemsResized += onItemsResized;
				items[page].onItemUpdated += onItemUpdated;
				items[page].onItemAdded += onItemAdded;
				items[page].onItemRemoved += onItemRemoved;
				items[page].onStateUpdated += onItemStateUpdated;

				onInventoryResized?.Invoke(page, 0, 0);
			}
		}

		private void GrantSkillsetLoadout(LevelAsset.DefaultLoadoutItem[] loadout)
		{
			foreach (LevelAsset.DefaultLoadoutItem item in loadout)
			{
				ItemAsset itemAsset = item.ResolveAsset(OnGetLevelSkillsetLoadoutSpawnErrorContext);
				if (itemAsset == null)
				{
					continue;
				}

				for (int amount = 0; amount < item.amount; ++amount)
				{
					tryAddItem(new Item(itemAsset, item.origin), true, false);
				}
			}
		}

		private void bestowLoadout()
		{
			if (loadout != null && loadout.Length > 0)
			{
				for (int index = 0; index < loadout.Length; index++)
				{
					tryAddItem(new Item(loadout[index], EItemOrigin.ADMIN), true, false);
				}
			}
			else if (Level.info != null)
			{
				bool hasLevelLoadoutOverrides = Level.getAsset()?.HasSkillsetLoadoutsOverride ?? false;
				bool useLevelLoadoutOverrides = hasLevelLoadoutOverrides;
				if (skillsets != SKILLSETS_CLIENT && skillsets != SKILLSETS_SERVER)
				{
					// Has been changed from default.
					useLevelLoadoutOverrides = false;
				}

				int skillsetIndex = (int) channel.owner.skillset;
				if (useLevelLoadoutOverrides)
				{
					LevelAsset.DefaultLoadoutItem[] skillsetLoadout = Level.getAsset().GetSkillsetLoadoutOrNull(channel.owner.skillset);
					if (!skillsetLoadout.IsNullOrEmpty())
					{
						GrantSkillsetLoadout(skillsetLoadout);
					}
				}
				else if (skillsets != null && skillsets[skillsetIndex] != null && skillsets[skillsetIndex].Length > 0)
				{
					for (int index = 0; index < skillsets[skillsetIndex].Length; index++)
					{
						tryAddItem(new Item(skillsets[skillsetIndex][index], EItemOrigin.WORLD), true, false);
					}
				}
				else if (Level.info.type == ELevelType.HORDE)
				{
					for (int index = 0; index < HORDE.Length; index++)
					{
						tryAddItem(new Item(HORDE[index], EItemOrigin.ADMIN), true, false);
					}
				}
			}

			if (Level.info != null)
			{
				foreach (ArenaLoadout loadout in Level.info.configData.Spawn_Loadouts)
				{
					for (ushort amount = 0; amount < loadout.Amount; amount++)
					{
						ushort itemID = SpawnTableTool.ResolveLegacyId(loadout.Table_ID, EAssetType.ITEM, OnGetSpawnLoadoutErrorContext);
						if (itemID != 0)
						{
							tryAddItemAuto(new Item(itemID, true), true, false, true, false);
						}
					}
				}
			}
		}

		private string OnGetLevelSkillsetLoadoutSpawnErrorContext()
		{
			return "level skillset loadout";
		}

		private string OnGetSpawnLoadoutErrorContext()
		{
			return "level config spawn loadout";
		}

		private void onShirtUpdated(ushort id, byte quality, byte[] state)
		{
			if (id != 0)
			{
				ItemBagAsset asset = Assets.find(EAssetType.ITEM, id) as ItemBagAsset;

				if (asset != null)
				{
					items[SHIRT].resize(asset.width, asset.height);
				}
			}
			else
			{
				items[SHIRT].resize(0, 0);
			}
		}

		private void onPantsUpdated(ushort id, byte quality, byte[] state)
		{
			if (id != 0)
			{
				ItemBagAsset asset = Assets.find(EAssetType.ITEM, id) as ItemBagAsset;

				if (asset != null)
				{
					items[PANTS].resize(asset.width, asset.height);
				}
			}
			else
			{
				items[PANTS].resize(0, 0);
			}
		}

		private void onBackpackUpdated(ushort id, byte quality, byte[] state)
		{
			if (id != 0)
			{
				ItemBagAsset asset = Assets.find(EAssetType.ITEM, id) as ItemBagAsset;

				if (asset != null)
				{
					items[BACKPACK].resize(asset.width, asset.height);
				}
			}
			else
			{
				items[BACKPACK].resize(0, 0);
			}
		}

		private void onVestUpdated(ushort id, byte quality, byte[] state)
		{
			if (id != 0)
			{
				ItemBagAsset asset = Assets.find(EAssetType.ITEM, id) as ItemBagAsset;

				if (asset != null)
				{
					items[VEST].resize(asset.width, asset.height);
				}
			}
			else
			{
				items[VEST].resize(0, 0);
			}
		}

		/// <summary>
		/// Called from player movement to close storage that has moved away.
		/// </summary>
		public void closeDistantStorage()
		{
			if (!isStoring)
				return;

			// Trunk storage is accessed while sitting in the vehicle,
			// so we'll never be too far away.
			if (isStorageTrunk)
				return;

			if (storage == null)
				return;

			if (!storage.shouldCloseWhenOutsideRange)
				return;

			Vector3 storagePosition = storage.transform.position;
			Vector3 playerPosition = transform.position;
			float distanceSquared = (playerPosition - storagePosition).sqrMagnitude;

			// Constant is also used in BarricadeManager for max interact with storage distance.
			const float maxDistanceSquared = 400;

			if (distanceSquared > maxDistanceSquared)
			{
				closeStorage();
			}
		}

		/// <summary>
		/// Serverside open a storage crate and notify client. 
		/// </summary>
		public void openStorage(InteractableStorage newStorage)
		{
			if (isStoring)
			{
				// Already had some storage open so notify it closed.
				closeStorage();
			}

			newStorage.isOpen = true;
			newStorage.opener = player;

			isStoring = true;
			isStorageTrunk = false;
			storage = newStorage;

			updateItems(STORAGE, storage.items);
			sendStorage();
		}

		/// <summary>
		/// Serverside grant access to car trunk storage and notify client.
		/// </summary>
		public void openTrunk(Items trunkItems)
		{
			if (isStoring)
			{
				// Already had some storage open so notify it closed.
				closeStorage();
			}

			isStoring = true;
			isStorageTrunk = true;
			storage = null; // storage is used to close crate

			updateItems(STORAGE, trunkItems);
			sendStorage();
		}

		/// <summary>
		/// Serverside revoke trunk access and notify client.
		/// </summary>
		public void closeTrunk()
		{
			if (!isStorageTrunk)
				return; // Plugin already revoked access, or perhaps opened a different storage.

			closeStorageAndNotifyClient();
		}

		/// <summary>
		/// Called on both client and server, as well as by storage itself when destroyed.
		/// </summary>
		public void closeStorage()
		{
			if (!isStoring)
				return;

			isStoring = false;
			isStorageTrunk = false;
			if (storage != null)
			{
				if (Provider.isServer)
				{
					storage.isOpen = false;
					storage.opener = null;
				}
				storage = null;
			}

			updateItems(STORAGE, null);
		}

		public void closeStorageAndNotifyClient()
		{
			if (isStoring)
			{
				closeStorage();
				sendStorage();
			}
		}

		private void onLifeUpdated(bool isDead)
		{
			if (Provider.isServer || channel.IsLocalPlayer)
			{
				if (isDead)
				{
					closeStorage();
				}
			}

			if (Provider.isServer)
			{
				bool loseWeapons = player.life.wasPvPDeath ? Provider.modeConfigData.Players.Lose_Weapons_PvP : Provider.modeConfigData.Players.Lose_Weapons_PvE;
				if (loseWeapons)
				{
					if (isDead)
					{
						items[0].resize(0, 0);
						items[1].resize(0, 0);
					}
					else
					{
						items[0].resize(1, 1);
						items[1].resize(1, 1);
					}
				}

				bool loseClothes = player.life.wasPvPDeath ? Provider.modeConfigData.Players.Lose_Clothes_PvP : Provider.modeConfigData.Players.Lose_Clothes_PvE;
				if (loseClothes)
				{
					if (isDead)
					{
						for (byte page = SLOTS; page < PAGES - 2; page++)
						{
							items[page].resize(0, 0);
						}
					}
					else
					{
						items[2].resize(5, 3);

						bestowLoadout();
					}
				}
				else
				{
					if (isDead)
					{
						float loseItems = player.life.wasPvPDeath ? Provider.modeConfigData.Players.Lose_Items_PvP : Provider.modeConfigData.Players.Lose_Items_PvE;

						for (byte page = SLOTS; page < PAGES - 2; page++)
						{
							if (items[page].getItemCount() > 0)
							{
								for (int index = items[page].getItemCount() - 1; index >= 0; index--)
								{
									if (Random.value < loseItems)
									{
										ItemJar jar = items[page].getItem((byte) index);
										ItemManager.dropItem(jar.item, transform.position, false, true, true);

										items[page].removeItem((byte) index);
									}
								}
							}
						}
					}
				}
			}
		}

		private void onItemsResized(byte page, byte newWidth, byte newHeight)
		{
			if (!channel.IsLocalPlayer && Provider.isServer && ownerHasInventory)
			{
				SendSize.Invoke(GetNetId(), ENetReliability.Reliable, channel.GetOwnerTransportConnection(), page, newWidth, newHeight);
			}

			onInventoryResized?.Invoke(page, newWidth, newHeight);

			incrementUpdateIndex();
		}

		private void onItemUpdated(byte page, byte index, ItemJar jar)
		{
			onInventoryUpdated?.Invoke(page, index, jar);

			incrementUpdateIndex();
		}

		private void onItemAdded(byte page, byte index, ItemJar jar)
		{
			if (!channel.IsLocalPlayer && Provider.isServer && ownerHasInventory)
			{
				sendItemAdd(page, jar);
			}

			onInventoryAdded?.Invoke(page, index, jar);

			incrementUpdateIndex();
		}

		private void onItemRemoved(byte page, byte index, ItemJar jar)
		{
			if (Provider.isServer)
			{
				if (!channel.IsLocalPlayer && ownerHasInventory)
				{
					sendItemRemove(page, jar);
				}

				if (player.equipment.checkSelection(page, jar.x, jar.y))
				{
					player.equipment.dequip();
				}
			}

			onInventoryRemoved?.Invoke(page, index, jar);

			incrementUpdateIndex();
		}

		private void onItemDiscarded(byte page, byte index, ItemJar jar)
		{
			bool shouldSpawnInteractableItem = true;
			if (player.life.isDead)
			{
				ItemAsset asset = jar.GetAsset();
				if (asset == null || !asset.shouldDropOnDeath)
				{
					shouldSpawnInteractableItem = false;
				}
			}

			if (Provider.isServer)
			{
				if (!channel.IsLocalPlayer && ownerHasInventory)
				{
					sendItemRemove(page, jar);
				}

				if (player.equipment.checkSelection(page, jar.x, jar.y))
				{
					player.equipment.dequip();
				}

				onInventoryRemoved?.Invoke(page, index, jar);

				if (shouldSpawnInteractableItem)
				{
					ItemManager.dropItem(jar.item, transform.position, false, true, true);
				}
			}

			incrementUpdateIndex();
		}

		private void onItemStateUpdated()
		{
			onInventoryStateUpdated?.Invoke();

			incrementUpdateIndex();
		}

		private void OnDestroy()
		{
			closeStorage();
		}

		internal void InitializePlayer()
		{
			//primary = null;
			//secondary = null;

			items = new Items[PAGES];
			for (byte index = 0; index < PAGES - 1; index++)
			{
				items[index] = new Items(index);

				items[index].onItemsResized += onItemsResized;
				items[index].onItemUpdated += onItemUpdated;
				items[index].onItemAdded += onItemAdded;
				items[index].onItemRemoved += onItemRemoved;
				items[index].onStateUpdated += onItemStateUpdated;
			}

			if (channel.IsLocalPlayer || Provider.isServer)
			{
				player.life.onLifeUpdated += onLifeUpdated;
			}

			if (Provider.isServer)
			{
				player.clothing.onShirtUpdated += onShirtUpdated;
				player.clothing.onPantsUpdated += onPantsUpdated;
				player.clothing.onBackpackUpdated += onBackpackUpdated;
				player.clothing.onVestUpdated += onVestUpdated;

				for (byte index = 0; index < PAGES - 1; index++)
				{
					items[index].onItemDiscarded = onItemDiscarded;
				}

				load();
			}
		}

		private bool wasLoadCalled;

		public void load()
		{
			wasLoadCalled = true;

			if (PlayerSavedata.fileExists(channel.owner.playerID, "/Player/Inventory.dat") && Level.info.type == ELevelType.SURVIVAL)
			{
				Block block = PlayerSavedata.readBlock(channel.owner.playerID, "/Player/Inventory.dat", 0);
				byte version = block.readByte();

				if (version > 3)
				{
					for (byte page = 0; page < PAGES - 2; page++)
					{
						items[page].loadSize(block.readByte(), block.readByte());
						byte count = block.readByte();

						for (byte index = 0; index < count; index++)
						{
							byte x = block.readByte();
							byte y = block.readByte();
							byte rot = 0;
							if (version > 4)
							{
								rot = block.readByte();
							}

							ushort id = block.readUInt16();
							byte amount = block.readByte();
							byte quality = block.readByte();
							byte[] state = block.readByteArray();

							ItemAsset asset = Assets.find(EAssetType.ITEM, id) as ItemAsset;

							if (asset != null)
							{
								items[page].loadItem(x, y, rot, new Item(id, amount, quality, state));
							}
						}
					}
				}
				else
				{
					items[0].loadSize(1, 1);
					items[1].loadSize(1, 1);
					items[2].loadSize(5, 3);

					items[BACKPACK].loadSize(0, 0);
					items[VEST].loadSize(0, 0);
					items[SHIRT].loadSize(0, 0);
					items[PANTS].loadSize(0, 0);
					items[STORAGE].loadSize(0, 0);

					bestowLoadout();
				}
			}
			else
			{
				items[0].loadSize(1, 1);
				items[1].loadSize(1, 1);
				items[2].loadSize(5, 3);

				items[BACKPACK].loadSize(0, 0);
				items[VEST].loadSize(0, 0);
				items[SHIRT].loadSize(0, 0);
				items[PANTS].loadSize(0, 0);
				items[STORAGE].loadSize(0, 0);

				bestowLoadout();
			}
		}

		public void save()
		{
			if (!wasLoadCalled)
				return;

			bool loseWeapons = player.life.wasPvPDeath ? Provider.modeConfigData.Players.Lose_Weapons_PvP : Provider.modeConfigData.Players.Lose_Weapons_PvE;
			bool loseClothes = player.life.wasPvPDeath ? Provider.modeConfigData.Players.Lose_Clothes_PvP : Provider.modeConfigData.Players.Lose_Clothes_PvE;

			if (player.life.isDead && loseWeapons && loseClothes)
			{
				if (PlayerSavedata.fileExists(channel.owner.playerID, "/Player/Inventory.dat"))
				{
					PlayerSavedata.deleteFile(channel.owner.playerID, "/Player/Inventory.dat");
				}
			}
			else
			{
				Block block = new Block();
				block.writeByte(SAVEDATA_VERSION);

				for (byte page = 0; page < PAGES - 2; page++)
				{
					byte width;
					byte height;
					byte itemCount;

					// Should we zero this page in the save file?
					bool losePage;
					if (player.life.isDead)
					{
						if (page < SLOTS)
						{
							losePage = loseWeapons;
						}
						else
						{
							losePage = loseClothes;
						}
					}
					else
					{
						losePage = false;
					}

					if (items[page] == null || losePage)
					{
						width = 0;
						height = 0;
						itemCount = 0;
					}
					else
					{
						width = items[page].width;
						height = items[page].height;
						itemCount = items[page].getItemCount();
					}

					block.writeByte(width);
					block.writeByte(height);
					block.writeByte(itemCount);

					for (byte index = 0; index < itemCount; index++)
					{
						ItemJar jar = items[page].getItem(index);
						// If jar is somehow null we write zeroed data to keep size valid. Loading handles skipping the item.

						block.writeByte(jar != null ? jar.x : (byte) 0);
						block.writeByte(jar != null ? jar.y : (byte) 0);
						block.writeByte(jar != null ? jar.rot : (byte) 0);

						block.writeUInt16(jar != null ? jar.item.id : (ushort) 0);
						block.writeByte(jar != null ? jar.item.amount : (byte) 0);
						block.writeByte(jar != null ? jar.item.quality : (byte) 0);
						block.writeByteArray(jar != null ? jar.item.state : new byte[0]);
					}
				}

				PlayerSavedata.writeBlock(channel.owner.playerID, "/Player/Inventory.dat", block);
			}
		}


/// <summary>
/// 未来对弃用api的处理
/// 如果本项目未来做到对未转变者底层代码的完全重写，不应保留任何对旧版本的兼容
/// 直接使用新版api即可
/// </summary>
/// 
		#region OBSOLETE
		[System.Obsolete]
		public List<InventorySearch> search(EItemType type, ushort[] calibers)
		{
			return search(type, calibers, true);
		}

		[System.Obsolete]
		public void search(List<InventorySearch> search, EItemType type, ushort caliber)
		{
			this.search(search, type, caliber, true);
		}

		[System.Obsolete("Please use the overload taking a pre-allocated list instead (better for performance)")]
		public List<InventorySearch> search(EItemType type)
		{
			List<InventorySearch> list = new List<InventorySearch>();
			search(list, type);
			return list;
		}

		[System.Obsolete("Please use the overload taking a pre-allocated list instead (better for performance)")]
		public List<InventorySearch> search(ushort id, bool findEmpty, bool findHealthy)
		{
			List<InventorySearch> list = new List<InventorySearch>();
			search(list, id, findEmpty, findHealthy);
			return list;
		}

		/// <summary>
		/// Please use SearchContents instead! To perform an equivalent search:
		/// • Set IncludeEquipmentSlots to false.
		/// • Set IncludeActiveStorageContainer to false.
		/// • Set ItemType to type.
		/// • Set IncludeEmpty to false.
		/// • Set IncludeMaxQuality to true.
		/// OR use the nearly drop-in replacement FindItemsByType.
		/// </summary>
		[System.Obsolete("Please use new search API instead! Refer to this method's comment for more information.")]
		public void search(List<InventorySearch> search, EItemType type)
		{
			for (byte page = SLOTS; page < PAGES - 2; page++)
			{
				items[page].search(search, type);
			}
		}

		/// <summary>
		/// Please use SearchContents instead! To perform an equivalent search:
		/// • Set IncludeEquipmentSlots to false.
		/// • Set IncludeActiveStorageContainer to false.
		/// • Set ItemType to type.
		/// • Set IncludeEmpty to false.
		/// • Set IncludeMaxQuality to true.
		/// • Set AnyCaliberIds to calibers.
		/// • Set IncludeUnspecifiedCaliber to allowZeroCaliber.
		/// OR use the nearly drop-in replacement FindAttachmentsByCaliber.
		/// </summary>
		[System.Obsolete("Please use new search API instead! Refer to this method's comment for more information.")]
		public List<InventorySearch> search(EItemType type, ushort[] calibers, bool allowZeroCaliber)
		{
			List<InventorySearch> list = new List<InventorySearch>();
			foreach (ushort caliber in calibers)
			{
				search(list, type, caliber, allowZeroCaliber);
			}

			return list;
		}

		/// <summary>
		/// Please use SearchContents instead! To perform an equivalent search:
		/// • Set IncludeEquipmentSlots to false.
		/// • Set IncludeActiveStorageContainer to false.
		/// • Set ItemType to type.
		/// • Set IncludeEmpty to false.
		/// • Set IncludeMaxQuality to true.
		/// • Set CaliberId to caliber.
		/// • Set IncludeUnspecifiedCaliber to allowZeroCaliber.
		/// OR use the nearly drop-in replacement FindAttachmentsByCaliber.
		/// </summary>
		[System.Obsolete("Please use new search API instead! Refer to this method's comment for more information.")]
		public void search(List<InventorySearch> search, EItemType type, ushort caliber, bool allowZeroCaliber)
		{
			for (byte page = SLOTS; page < PAGES - 2; page++)
			{
				items[page].search(search, type, caliber, allowZeroCaliber);
			}
		}

		/// <summary>
		/// Please use SearchContents instead! To perform an equivalent search:
		/// • Set IncludeEquipmentSlots to false.
		/// • Set IncludeActiveStorageContainer to false.
		/// • Set AssetRef to id.
		/// • Set IncludeEmpty to findEmpty.
		/// • Set IncludeMaxQuality to findHealthy.
		/// OR use the nearly drop-in replacement FindItemsByAsset.
		/// </summary>
		[System.Obsolete("Please use new search API instead! Refer to this method's comment for more information.")]
		public void search(List<InventorySearch> search, ushort id, bool findEmpty, bool findHealthy)
		{
			for (byte page = SLOTS; page < PAGES - 2; page++)
			{
				items[page].search(search, id, findEmpty, findHealthy);
			}
		}

		[System.Obsolete("Unused in vanilla. Should probably not be used.")]
		public List<InventorySearch> search(List<InventorySearch> search)
		{
			List<InventorySearch> clean = new List<InventorySearch>();

			for (int index = 0; index < search.Count; index++)
			{
				InventorySearch item = search[index];

				bool isValid = true;
				for (int check = 0; check < clean.Count; check++)
				{
					InventorySearch other = clean[check];

					if (other.jar.item.id == item.jar.item.id && other.jar.item.amount == item.jar.item.amount && other.jar.item.quality == item.jar.item.quality)
					{
						isValid = false;
						break;
					}
				}

				if (isValid)
				{
					clean.Add(item);
				}
			}

			return clean;
		}

		/// <summary>
		/// Please use SearchContents instead! To perform an equivalent search:
		/// • Set IncludeEquipmentSlots to true.
		/// • Set IncludeActiveStorageContainer to true.
		/// • Set MaxResultsCount to 1.
		/// • Set AssetRef to id.
		/// • Set IncludeEmpty to false.
		/// • Set IncludeMaxQuality to true.
		/// OR use the nearly drop-in replacements FindFirstItemByAsset or HasItemByAsset.
		/// </summary>
		[System.Obsolete("Please use new search API instead! Refer to this method's comment for more information.")]
		public InventorySearch has(ushort id)
		{
			for (byte page = 0; page < PAGES - 1; page++)
			{
				InventorySearch has = items[page].has(id);

				if (has != null)
				{
					return has;
				}
			}

			return null;
		}
		#endregion OBSOLETE
	}
}
