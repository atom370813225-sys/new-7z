using System.Security.Cryptography;
using New7z.Core;

internal static class VolumeTests
{
    public static async Task RunAsync(string root,List<string> checks)
    {
        void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
        Check(ArchiveVolumes.ParseSize("")==0&&ArchiveVolumes.ParseSize("1.5 GiB")==1610612736&&ArchiveVolumes.ParseSize("100m")==104857600,"volume size parses binary units and fractional values");
        foreach(var invalid in new[]{"0","-1 MiB","1 KB","abc","1,5 GiB","999999999999999999999999999999999 GiB"})
        {
            var refused=false;try{ArchiveVolumes.ParseSize(invalid);}catch(ArgumentException){refused=true;}
            Check(refused,"volume size rejects "+invalid);
        }
        var folder=Path.Combine(root,"分卷 source");Directory.CreateDirectory(Path.Combine(folder,"空目录"));
        var bytes=RandomNumberGenerator.GetBytes(3*1024*1024+127);
        var source=Path.Combine(folder,"随机 data.bin");await File.WriteAllBytesAsync(source,bytes);
        var engine=new ArchiveEngine();const long cap=1024*1024;
        foreach(var format in new[]{"7z","zip","tar"})
        foreach(var encrypted in format=="tar"?new[]{false}:new[]{false,true})
        {
            var password=encrypted?(format=="zip"?"split-password":"分卷 password"):null;
            var output=Path.Combine(root,$"split-{format}-{encrypted}.{format}");
            Console.WriteLine($"Testing split {format}, encrypted={encrypted}");
            await engine.CreateAsync(output,[folder],format,password:password,encryptNames:encrypted,volumeSize:cap);
            var parts=Directory.GetFiles(root,Path.GetFileName(output)+".*").OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            Check(!File.Exists(output)&&parts.Length==4&&parts.All(p=>new FileInfo(p).Length<=cap)&&parts.Take(parts.Length-1).All(p=>new FileInfo(p).Length==cap),$"{format} encrypted={encrypted} writes capped volumes directly");
            var first=output+".001";
            var entries=await engine.ListAsync(first,password);
            Check(entries.Any(e=>e.IsDirectory&&e.Name=="空目录")&&entries.Any(e=>e.Name=="随机 data.bin"),$"{format} split lists real entries");
            Check((await engine.ReadAsync(first,entries.Single(e=>!e.IsDirectory),password)).SequenceEqual(bytes),$"{format} split random-access preview roundtrip");
            await engine.TestAsync(first,password);checks.Add($"{format} split integrity verified");
            var extracted=Path.Combine(root,$"split-extracted-{format}-{encrypted}");
            await engine.ExtractAsync(first,entries,extracted,password:password);
            Check((await File.ReadAllBytesAsync(Path.Combine(extracted,Path.GetFileName(folder),Path.GetFileName(source)))).SequenceEqual(bytes),$"{format} split extraction SHA-equivalent roundtrip");
            if(format=="zip"&&!encrypted)
            {
                var joined=Path.Combine(root,"joined.zip");
                using(var joinedStream=File.Create(joined))foreach(var part in parts){using var input=File.OpenRead(part);await input.CopyToAsync(joinedStream);}
                using var zip=System.IO.Compression.ZipFile.OpenRead(joined);using var entry=zip.Entries.Single(e=>e.FullName.EndsWith("随机 data.bin",StringComparison.Ordinal)).Open();
                Check(SHA256.HashData(entry).SequenceEqual(SHA256.HashData(bytes)),"split ZIP concatenation is independently readable by .NET ZipArchive");
            }
            var refused=false;try{await engine.CreateAsync(output,[source],format,volumeSize:cap);}catch(IOException){refused=true;}
            Check(refused&&File.Exists(first),$"{format} existing split set is never overwritten");
        }
        var conflict=Path.Combine(root,"volume-conflict.7z");await File.WriteAllTextAsync(conflict+".003","keep");
        var conflictRefused=false;try{await engine.CreateAsync(conflict,[source],volumeSize:cap);}catch(IOException){conflictRefused=true;}
        Check(conflictRefused&&await File.ReadAllTextAsync(conflict+".003")=="keep"&&!File.Exists(conflict+".001"),"split preflight rejects orphan later volumes without writing");
        var unsupported=false;try{await engine.CreateAsync(Path.Combine(root,"split.zst"),[source],"zst",volumeSize:cap);}catch(ArgumentException){unsupported=true;}
        Check(unsupported,"split rejects unsupported stream formats");
        var race=Path.Combine(root,"split-race.zip");var injected=false;
        try
        {
            await engine.CreateAsync(race,[source],"zip",volumeSize:cap,progress:new InlineProgress(p=>{if(!injected&&p.Total>0&&p.Completed>=p.Total){File.WriteAllText(race+".002","keep concurrent file");injected=true;}}));
            throw new Exception("Concurrent volume collision was ignored");
        }
        catch(IOException){}
        Check(injected&&await File.ReadAllTextAsync(race+".002")=="keep concurrent file"&&Directory.GetFiles(root,"split-race*").Length==1,"split commit collision rolls back owned volumes and preserves concurrent file");
        var canceled=Path.Combine(root,"cancel-volumes.7z");using var cancellation=new CancellationTokenSource();
        try{await engine.CreateAsync(canceled,[source],level:0,volumeSize:cap,cancellation:cancellation.Token,progress:new InlineProgress(p=>{if(p.Completed>0)cancellation.Cancel();}));throw new Exception("Split cancellation ignored");}catch(OperationCanceledException){}
        Check(!Directory.EnumerateFileSystemEntries(root).Any(p=>Path.GetFileName(p).StartsWith("cancel-volumes",StringComparison.Ordinal)),"split cancellation cleans every temporary and output volume");
        var broken=Path.Combine(root,"split-7z-False.7z");File.Move(broken+".002",broken+".002.missing");
        var missing=false;try{await engine.TestAsync(broken+".001");}catch(IOException){missing=true;}
        Check(missing,"split missing middle volume fails integrity test");
        var poison=Path.Combine(root,"split-poison");Directory.CreateDirectory(poison);
        var protectedOutput=Path.Combine(root,"prevent-overwrite.tar");var poisonSource=Path.Combine(poison,"prevent-overwrite.tar.002");await File.WriteAllBytesAsync(poisonSource,bytes);
        await engine.CreateAsync(protectedOutput,[poisonSource],"tar",volumeSize:cap);
        var protectedPart=protectedOutput+".002";var hash=SHA256.HashData(await File.ReadAllBytesAsync(protectedPart));var protectedEntries=await engine.ListAsync(protectedOutput+".001");var protectedRefused=false;
        try{await engine.ExtractAsync(protectedOutput+".001",protectedEntries,root,ConflictPolicy.Replace);}catch(IOException){protectedRefused=true;}
        Check(protectedRefused&&SHA256.HashData(await File.ReadAllBytesAsync(protectedPart)).SequenceEqual(hash),"split extraction cannot replace any source volume");
        var small=Path.Combine(root,"single-volume.zip");await engine.CreateAsync(small,[Path.Combine(root,"empty.txt")],"zip",volumeSize:cap);
        Check(File.Exists(small+".001")&&!File.Exists(small+".002"),"split below cap still creates exactly one numbered volume");
    }
}
