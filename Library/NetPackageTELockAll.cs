// Part of Remote Turret Control Mod
// Copyright 2022 Marcel Greter

using System;
using System.Collections.Generic;

// Locks a TileEntity plus a few more tiles
// Used to lock all connected turrets at once

class NetPackageTELockAll : NetPackage
{
    private TELockType type;
    private List<Tuple<int, Vector3i, int>> entities;
    private int entityIdThatOpenedIt;
    private string customUi;

    public NetPackageTELockAll Setup(
      TELockType _type,
      List<Tuple<int, Vector3i, int>> entities,
      int _entityIdThatOpenedIt,
      string _customUi = null)
    {
        if (entities == null) throw new ArgumentException(
            "Entities list to lock is a null pointer");
        this.entities = entities;
        entityIdThatOpenedIt = _entityIdThatOpenedIt;
        customUi = _customUi ?? "";
        type = _type;
        return this;
    }

    public override void read(PooledBinaryReader _br)
    {
        type = (TELockType)_br.ReadByte();
        ushort amount = _br.ReadUInt16();
        if (entities != null) entities.Clear();
        else entities = new List<Tuple<int, Vector3i, int>>();
        for (ushort i = 0; i < amount; i++)
        {
            entities.Add(new Tuple<int, Vector3i, int>(
                _br.ReadUInt16(),
                StreamUtils.ReadVector3i(_br),
                _br.ReadInt32()
            ));
        }
        entityIdThatOpenedIt = _br.ReadInt32();
        customUi = _br.ReadString();
    }

    public override void write(PooledBinaryWriter _bw)
    {
        base.write(_bw);
        _bw.Write((byte)type);
        _bw.Write((ushort)entities.Count);
        foreach (var entry in entities)
        {
            _bw.Write((ushort)entry.Item1);
            StreamUtils.Write(_bw, entry.Item2);
            _bw.Write(entry.Item3);
        }
        _bw.Write(entityIdThatOpenedIt);
        _bw.Write(customUi);
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        switch (type)
        {
            case TELockType.LockServerPanel:
                PanelLockAllManager.TELockServerAll(_world,
                    entities, entityIdThatOpenedIt, customUi);
                break;
            case TELockType.UnlockServerPanel:
                PanelLockAllManager.TEUnlockServerAll(_world,
                    entities);
                break;
        }
    }

    public override int GetLength() => 21;

    public enum TELockType : byte
    {
        LockServerPanel,
        UnlockServerPanel,
    }

}
