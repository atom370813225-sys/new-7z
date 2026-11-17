namespace New7z.Core;

public sealed class TransferTracker
{
    private ulong total,startBytes,lastBytes;
    private TimeSpan start;
    private bool initialized;
    public double BytesPerSecond {get;private set;}
    public TimeSpan? Remaining {get;private set;}
    public void Update(EngineProgress progress,TimeSpan elapsed)
    {
        if(!progress.IsByteProgress){initialized=false;BytesPerSecond=0;Remaining=null;return;}
        if(!initialized||total!=progress.Total||progress.Completed<lastBytes)
        {initialized=true;total=progress.Total;startBytes=progress.Completed;start=elapsed;BytesPerSecond=0;Remaining=null;}
        lastBytes=progress.Completed;
        var seconds=(elapsed-start).TotalSeconds;
        if(seconds<0.2)return;
        BytesPerSecond=(progress.Completed-startBytes)/seconds;
        Remaining=total>0&&BytesPerSecond>0?TimeSpan.FromSeconds(Math.Min(TimeSpan.MaxValue.TotalSeconds-1,(total-Math.Min(total,progress.Completed))/BytesPerSecond)):null;
    }
}
