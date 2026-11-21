using New7z.Core;

internal static class CopyPlanTests
{
    internal static Task RunAsync(string root,List<string> checks)
    {
        var destination=Path.Combine(root,"copy-plan");Directory.CreateDirectory(destination);
        var entries=new List<ArchiveEntry>
        {
            new ArchiveEntry{Index=0,Path="设计稿/说明.txt"},
            new ArchiveEntry{Index=1,Path="设计稿/封面.png"},
            new ArchiveEntry{Index=2,Path="封面.png"}
        };
        var outputs=ArchiveEngine.ResolveOutputs(destination,entries);
        if(!outputs[0].Equals(Path.Combine(destination,"设计稿","说明.txt"),StringComparison.OrdinalIgnoreCase))throw new Exception("copy plan lost the archive subfolder: "+outputs[0]);
        if(outputs.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=outputs.Count)throw new Exception("copy plan reused an output path.");
        checks.Add("copy plan preserves archive subfolders and unique outputs");

        var collision=ArchiveEngine.ResolveOutputs(destination,[new ArchiveEntry{Path="README.txt"},new ArchiveEntry{Path="readme.txt"},new ArchiveEntry{Path="Readme.txt"}]);
        if(!collision[1].EndsWith("readme (2).txt",StringComparison.OrdinalIgnoreCase)||!collision[2].EndsWith("Readme (3).txt",StringComparison.OrdinalIgnoreCase))
            throw new Exception("copy plan did not number case-insensitive name collisions: "+string.Join(" | ",collision));
        checks.Add("copy plan numbers case-insensitive file name collisions");

        var folders=ArchiveEngine.ResolveOutputs(destination,[new ArchiveEntry{Path="Docs",IsDirectory=true},new ArchiveEntry{Path="docs",IsDirectory=true}]);
        if(!folders[1].EndsWith("docs (2)",StringComparison.OrdinalIgnoreCase))throw new Exception("copy plan did not number folder name collisions: "+folders[1]);
        checks.Add("copy plan numbers folder name collisions");

        var escaped=false;try{ArchiveEngine.ResolveOutputs(destination,[new ArchiveEntry{Path="../escape.txt"}]);}catch(IOException){escaped=true;}
        if(!escaped)throw new Exception("copy plan accepted a path outside the destination.");
        checks.Add("copy plan rejects paths that escape the destination folder");

        // Clipboard roots are the selected rows; a selected folder keeps its own path while
        // its children only supply the content extracted under it.
        var rows=new List<ArchiveEntry>
        {
            new ArchiveEntry{Index=uint.MaxValue,Path="设计稿",IsDirectory=true},
            new ArchiveEntry{Index=2,Path="封面.png"}
        };
        var selected=new List<ArchiveEntry>
        {
            new ArchiveEntry{Index=uint.MaxValue,Path="设计稿",IsDirectory=true},
            new ArchiveEntry{Index=0,Path="设计稿/说明.txt"},
            new ArchiveEntry{Index=1,Path="设计稿/封面.png"},
            new ArchiveEntry{Index=2,Path="封面.png"}
        };
        var planned=ArchiveEngine.ResolveOutputs(destination,selected);
        var byPath=new Dictionary<string,string>(StringComparer.Ordinal);
        for(var i=0;i<selected.Count;i++)byPath[selected[i].NormalizedPath]=planned[i];
        var copyRoots=rows.Select(row=>byPath[row.NormalizedPath]).ToArray();
        if(copyRoots.Length!=2||!copyRoots[0].EndsWith("设计稿",StringComparison.OrdinalIgnoreCase)||!copyRoots[1].EndsWith("封面.png",StringComparison.OrdinalIgnoreCase))
            throw new Exception("copy roots did not keep the selected rows: "+string.Join(" | ",copyRoots));
        checks.Add("copy roots keep the selected folder and file instead of their children");
        return Task.CompletedTask;
    }
}
