// Part of Remote Turret Control Mod
// Copyright 2022 Marcel Greter

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

static class RemoteTurretUtils
{

	/****************************************************************************/
	/****************************************************************************/

	private static readonly FieldInfo FieldWireDataList = AccessTools
		.Field(typeof(TileEntityPowered), "wireDataList");

	private static readonly Queue<Tuple<Vector3i, int>>
		queue = new Queue<Tuple<Vector3i, int>>();

	private static List<Vector3i> PoweredChildren(TileEntityPowered te)
		=> FieldWireDataList.GetValue(te) as List<Vector3i>;

	public static void CollectTurrets(
		WorldBase world,
		int cIdx,
		Vector3i blockPos,
		List<TileEntityPowered> ControlPanels,
		List<TileEntityPoweredRangedTrap> RemoteTurrets,
		int maxDepth = 5,
		bool skipSelf = true)
	{
		ControlPanels.Clear(); RemoteTurrets.Clear();
		queue.Enqueue(new Tuple<Vector3i, int>(blockPos, 0));
		while (queue.Count > 0)
		{
			var queued = queue.Dequeue();
			var position = queued.Item1;
			var depth = queued.Item2;
			if (world.GetTileEntity(cIdx, position) is TileEntityPowered tep)
			{
				// Skip other remote turret blocks (only collect local turrets)
				if (tep.blockValue.Block is BlockRemoteTurret)
				{
					ControlPanels.Add(tep);
					// Abort if more than one found
					if (skipSelf && depth > 0) continue;
				}
				// Collect if the current child is a powered ranged trap
				else if (tep is TileEntityPoweredRangedTrap tet)
				{
					RemoteTurrets.Add(tet);
				}
				// Check if further children are to deep
				if (depth >= maxDepth) continue;
				// Enqueue all children for further processing
				foreach (Vector3i child in PoweredChildren(tep))
					queue.Enqueue(new Tuple<Vector3i, int>(child, depth + 1));
			}
		}
	}

}
