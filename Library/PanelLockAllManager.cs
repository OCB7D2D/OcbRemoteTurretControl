// Part of Remote Turret Control Mod
// Copyright 2022 Marcel Greter

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

static class PanelLockAllManager
{

	static readonly FieldInfo FieldLockedTileEntities = AccessTools.Field(typeof(GameManager), "lockedTileEntities");
	static readonly MethodInfo MethodOpenTileEntityUi = AccessTools.Method(typeof(GameManager), "OpenTileEntityUi");
	static readonly MethodInfo MethodOpenTileEntityAllowed = AccessTools.Method(typeof(GameManager), "OpenTileEntityAllowed");

	public static bool IsTileLockedByLivingEntity(World world, Dictionary<TileEntity, int> locked, TileEntity tileEntity)
	{
		if (!locked.ContainsKey(tileEntity)) return false;
		Entity te = world.GetEntity(locked[tileEntity]);
		if (te is EntityAlive entity) return !entity.IsDead();
		return false;
	}

	public static void TEUnlockServerAll(World world,
		List<Tuple<int, Vector3i, int>> entries)
	{

		if (entries == null) return;
		if (entries.Count == 0) return;
		var gmgr = world.GetGameManager() as GameManager;
		if (gmgr == null) return;

		if (!(FieldLockedTileEntities.GetValue(gmgr) is
			Dictionary<TileEntity, int> locked)) return;

		if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
		{
			foreach (var entry in entries)
			{
				TileEntity tile;
				if (entry.Item3 == -1)
				{
					tile = world.GetTileEntity(entry.Item1, entry.Item2);
				}
				else
				{
					tile = world.GetTileEntity(entry.Item3);
					if (tile == null) gmgr.ClearTileEntityLockForClient(entry.Item3);
				}
				if (tile == null) return;
				locked.Remove(tile);
			}
		}
		else
		{
			SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
				NetPackageManager.GetPackage<NetPackageTELockAll>().Setup(
					NetPackageTELockAll.TELockType.UnlockServerPanel,
					entries, -1));
		}
	}

	public static void TELockServerAll(
		World world,
		List<Tuple<int, Vector3i, int>> entries,
		int _entityIdThatOpenedIt,
		string _customUi = null)
	{

		if (entries == null) return;
		if (entries.Count == 0) return;
		var gmgr = world.GetGameManager();
		if (gmgr == null) return;

		if (!(FieldLockedTileEntities.GetValue(gmgr) is
			Dictionary<TileEntity, int> locked)) return;

		foreach (KeyValuePair<TileEntity, int> lockedTileEntity in locked)
		{
			// We already have a look somehow, nothing to do?
			if (_entityIdThatOpenedIt == lockedTileEntity.Value) return;
		}

		if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
		{
			var acquired = new List<TileEntity>();
			var main = entries[0]; // entries[i]
			foreach (var entry in entries)
			{
				TileEntity tile = entry.Item3 != -1 ? world.GetTileEntity(entry.Item3) : world.GetTileEntity(entry.Item1, entry.Item2);
				if (tile == null || !(bool)MethodOpenTileEntityAllowed.Invoke(gmgr, new object[] { _entityIdThatOpenedIt, tile, _customUi })) break;
				if (IsTileLockedByLivingEntity(world, locked, tile)) break;
				locked[tile] = _entityIdThatOpenedIt;
				acquired.Add(tile);
			}
			if (acquired.Count == entries.Count)
			{
				MethodOpenTileEntityUi.Invoke(gmgr, new object[] { _entityIdThatOpenedIt, acquired[0], _customUi });
				SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
					NetPackageManager.GetPackage<NetPackageTELock>().Setup(
						NetPackageTELock.TELockType.AccessClient,
						main.Item1, main.Item2, main.Item3,
						_entityIdThatOpenedIt, _customUi),
					true);
			}
			else
			{
				// Reset already acquired locks
				foreach (var entity in acquired)
				{
					locked.Remove(entity);
				}
				if ((world.GetEntity(_entityIdThatOpenedIt) as EntityPlayerLocal) == null)
				{
					SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
						NetPackageManager.GetPackage<NetPackageTELock>().Setup(
							NetPackageTELock.TELockType.DeniedAccess,
							main.Item1, main.Item2, main.Item3, _entityIdThatOpenedIt, _customUi),
						_attachedToEntityId: _entityIdThatOpenedIt);
				}
				else
				{
					gmgr.TEDeniedAccessClient(main.Item1, main.Item2, main.Item3, _entityIdThatOpenedIt);
				}
			}
		}
		else
		{
			SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager
				.GetPackage<NetPackageTELockAll>().Setup(
					NetPackageTELockAll.TELockType.LockServerPanel,
					entries, _entityIdThatOpenedIt, _customUi));
		}
	}

}