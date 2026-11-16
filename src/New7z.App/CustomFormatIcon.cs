using System.Security.Cryptography;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace New7z.App;

// Explorer needs an ICO, while app previews continue to use the original PNG.
internal static class CustomFormatIcon
{
    private static readonly int[] Sizes=[16,24,32,48,64,128,256];

    internal static async Task<string> CreateAsync(string pngPath)
    {
        if(new FileInfo(pngPath).Length>10*1024*1024)throw new IOException("The image must not exceed 10 MiB.");
        var bytes=await File.ReadAllBytesAsync(pngPath);
        if(bytes.Length>10*1024*1024)throw new IOException("The image must not exceed 10 MiB.");
        var folder=Path.Combine(UserSettings.Folder,"ExplorerIcons");
        // A content-specific path also prevents Explorer from reusing an old cached icon.
        var target=Path.Combine(folder,"v1-"+Convert.ToHexString(SHA256.HashData(bytes))+".ico");
        if(File.Exists(target))return target;
        using var input=new MemoryStream(bytes);
        using var source=input.AsRandomAccessStream();
        var decoder=await BitmapDecoder.CreateAsync(source);
        if(decoder.DecoderInformation.CodecId!=BitmapDecoder.PngDecoderId||decoder.PixelWidth==0||decoder.PixelHeight==0||decoder.PixelWidth>4096||decoder.PixelHeight>4096)
            throw new IOException("Use a PNG image no larger than 4096 × 4096.");
        var frames=new List<byte[]>();
        foreach(var size in Sizes)
        {
            var ratio=Math.Min((double)size/decoder.PixelWidth,(double)size/decoder.PixelHeight);
            var width=(uint)Math.Max(1,Math.Round(decoder.PixelWidth*ratio));
            var height=(uint)Math.Max(1,Math.Round(decoder.PixelHeight*ratio));
            var transform=new BitmapTransform{ScaledWidth=width,ScaledHeight=height,InterpolationMode=BitmapInterpolationMode.Fant};
            var data=await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,transform,ExifOrientationMode.IgnoreExifOrientation,ColorManagementMode.DoNotColorManage);
            var pixels=data.DetachPixelData();
            var square=new byte[size*size*4];
            var left=(size-(int)width)/2;var top=(size-(int)height)/2;
            for(var y=0;y<height;y++)System.Buffer.BlockCopy(pixels,y*(int)width*4,square,((y+top)*size+left)*4,(int)width*4);
            using var output=new InMemoryRandomAccessStream();
            var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,output);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)size,(uint)size,96,96,square);
            await encoder.FlushAsync();
            output.Seek(0);
            using var reader=new DataReader(output.GetInputStreamAt(0));
            var frame=new byte[checked((int)output.Size)];
            await reader.LoadAsync((uint)frame.Length);reader.ReadBytes(frame);frames.Add(frame);
        }
        Directory.CreateDirectory(folder);
        var temporary=target+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var writer=new BinaryWriter(File.Create(temporary)))
            {
                writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)Sizes.Length);
                var offset=6+16*Sizes.Length;
                for(var i=0;i<Sizes.Length;i++)
                {
                    writer.Write((byte)(Sizes[i]==256?0:Sizes[i]));writer.Write((byte)(Sizes[i]==256?0:Sizes[i]));
                    writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);
                    writer.Write(frames[i].Length);writer.Write(offset);offset+=frames[i].Length;
                }
                foreach(var frame in frames)writer.Write(frame);
            }
            try{File.Move(temporary,target);}
            catch(IOException) when(File.Exists(target)){/* Another window generated the same content. */}
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
        return target;
    }
}
