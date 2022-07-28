// Part of Remote Turret Control Mod
// Copyright 2022 Marcel Greter

using HarmonyLib;
using System;
using System.Reflection;

class NetPackageServerAction : NetPackage
{

    string fqfn = null;
    object[] args = null;
    int entityId = -1;
    int requestId = -1;

    public NetPackageServerAction Setup(string fqfn, object[] args,
        int entityId = -1, int requestId = -1)
    {
        this.fqfn = fqfn;
        this.args = args;
        this.entityId = entityId;
        this.requestId = requestId;
        return this;
    }

    public override void write(PooledBinaryWriter bw)
    {
        base.write(bw);
        bw.Write(fqfn);
        bw.Write(entityId);
        bw.Write(requestId);
        HarmonySerializer.Freeze(bw, args);
    }

    public override void read(PooledBinaryReader br)
    {
        fqfn = br.ReadString();
        entityId = br.ReadInt32();
        requestId = br.ReadInt32();
        args = (object[])HarmonySerializer.Thaw(br);
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        var types = new Type[args.Length];
        for (var i = 0; i < args.Length; i++)
            types[i] = args[i].GetType();
        if (!NetServerAction.AllowedFunctions.Contains(fqfn))
            throw new Exception("Method not white-listed " + fqfn);
        MethodInfo method = AccessTools.Method(fqfn, types);
        if (method == null) throw new Exception("Method not found " + fqfn);
        if (!method.IsStatic) throw new Exception("Only Static methods allowed " + fqfn);
        try
        {
            object rv = method.Invoke(null, args);
            var pkg = NetPackageManager.GetPackage<NetPackageServerAnswer>().Setup(rv, requestId);
            ConnectionManager.Instance.SendPackage(pkg, _attachedToEntityId: entityId);
        }
        catch (Exception ex)
        {
            var pkg = NetPackageManager.GetPackage<NetPackageServerAnswer>().Setup(ex, requestId);
            ConnectionManager.Instance.SendPackage(pkg, _attachedToEntityId: entityId);
            throw ex;
        }
    }

    public override int GetLength() => 21;

}
