using New7z.Core;

internal static class StaleEntryTests
{
    internal static async Task RunAsync(string root,List<string> checks)
    {
        var folder=Path.Combine(root,"stale-entries");Directory.CreateDirectory(folder);
        var files=new List<string>();
        foreach(var name in new[]{"a","b","c"}){var file=Path.Combine(folder,name+".txt");await File.WriteAllTextAsync(file,new string(char.ToUpperInvariant(name[0]),4));files.Add(file);}
        var engine=new ArchiveEngine();
        foreach(var format in new[]{"zip","7z"})
        {
            var archive=Path.Combine(folder,"fixture."+format);await engine.CreateAsync(archive,files,format);
            var before=await engine.ListAsync(archive);var selected=before.Single(e=>e.Path=="b.txt");
            await engine.ModifyAsync(archive,[before.Single(e=>e.Path=="a.txt").Index],[]);
            var destination=Path.Combine(folder,"output-"+format);
            var rejected=false;try{await engine.ExtractAsync(archive,[selected],destination);}catch(IOException){rejected=true;}
            if(!rejected||Directory.EnumerateFiles(destination,"*",SearchOption.AllDirectories).Any())throw new Exception("Stale extraction wrote content from a different entry.");
            checks.Add(format+" stale extraction index is rejected before writing any file");
            rejected=false;try{await engine.ReadAsync(archive,selected);}catch(IOException){rejected=true;}
            if(!rejected)throw new Exception("Stale preview read a different entry.");checks.Add(format+" stale preview index is rejected");
            var fresh=(await engine.ListAsync(archive)).Single(e=>e.Path=="b.txt");
            await engine.ExtractAsync(archive,[fresh],destination);
            if(await File.ReadAllTextAsync(Path.Combine(destination,"b.txt"))!="BBBB")throw new Exception("Fresh extraction did not preserve the chosen content.");
            checks.Add(format+" fresh selection extracts the correct file after an archive modification");
        }
        var data=Path.Combine(folder,"large.bin");await File.WriteAllBytesAsync(data,new byte[8*1024*1024]);
        var zip=Path.Combine(folder,"large.zip");await engine.CreateAsync(zip,[data],"zip");
        var entries=await engine.ListAsync(zip);var seen=new List<EngineProgress>();
        await engine.ExtractAsync(zip,entries,Path.Combine(folder,"large-out"),progress:new ImmediateProgress(seen.Add));
        if(!seen.Any(p=>p.IsByteProgress&&p.Total>=8UL*1024*1024)||seen[^1].Completed!=seen[^1].Total)throw new Exception("Extraction did not report byte totals and completion.");
        checks.Add("large file extraction reports byte totals instead of file counts");
    }
    private sealed class ImmediateProgress(Action<EngineProgress> report):IProgress<EngineProgress>{public void Report(EngineProgress progress)=>report(progress);}
}
