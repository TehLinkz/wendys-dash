using System;
using System.IO;

namespace WendysDash;

internal sealed class SmokeFixture : IDisposable
{
    public const string Map = "// Synthetic fixture\nGame_Player.prototype.isDashing=function(){return false;};\n";
    public string Root { get; }
    public string Target { get; }
    public SmokeFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "wendys-dash-smoke-" + Guid.NewGuid().ToString("N"));
        Target = Path.Combine(Root, "package.nw", "js", "plugins", "MapScene.js");
        Directory.CreateDirectory(Path.GetDirectoryName(Target));
        File.WriteAllText(Target, Map);
        File.WriteAllText(Path.Combine(Root, "package.nw", "js", "rmmz_objects.js"), "Game_Player.prototype.updateDashing=function(){this._dashing=true;};\nGame_CharacterBase.prototype.realMoveSpeed=function(){return this._moveSpeed + (this.isDashing() ? 1 : 0);};");
        File.WriteAllText(Path.Combine(Root, "package.nw", "js", "plugins", "InputAdjustments.js"), "// synthetic input");
    }
    public void Dispose()
    {
        string full = Path.GetFullPath(Root);
        if (Path.GetDirectoryName(full) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) || !Path.GetFileName(full).StartsWith("wendys-dash-smoke-", StringComparison.Ordinal)) throw new IOException("Unexpected fixture cleanup path.");
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }
}
