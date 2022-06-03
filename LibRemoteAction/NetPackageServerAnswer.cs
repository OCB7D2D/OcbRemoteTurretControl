// Part of Remote Turret Control Mod
// Copyright 2022 Marcel Greter

using System;

class NetPackageServerAnswer : NetPackage
{

    private object rv = null;
    private bool error = false;
    private int reqid = int.MinValue;

    public NetPackageServerAnswer Setup(Exception rv, int reqid)
    {
        this.rv = rv.ToString();
        this.error = true;
        this.reqid = reqid;
        return this;
    }

    public NetPackageServerAnswer Setup(object rv, int reqid)
    {
        this.rv = rv;
        this.error = false;
        this.reqid = reqid;
        return this;
    }

    public override void write(PooledBinaryWriter bw)
    {
        base.write(bw);
        bw.Write(reqid); // identifier
        bw.Write(error); // identifier
        HarmonySerializer.Freeze(bw, rv);
    }

    public override void read(PooledBinaryReader br)
    {
        reqid = br.ReadInt32();
        error = br.ReadBoolean();
        rv = HarmonySerializer.Thaw(br);
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        NetServerAction.OnServerAnswer(reqid, rv, error);
    }

    public override int GetLength() => 21;

}
