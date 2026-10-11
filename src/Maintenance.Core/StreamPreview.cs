using System.Text;
namespace Maintenance.Core;
public sealed record StreamPreview(long Size,byte[] Bytes,string Text,bool Binary,bool Truncated)
{
    public const int Limit=65536;
    public static StreamPreview Read(string path)
    {
        if(StreamCatalog.StreamName(path).Length==0)throw new IOException("Select a named data stream.");
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        long size=file.Length;byte[] bytes=new byte[(int)Math.Min(size,Limit)];int count=0;
        while(count<bytes.Length){int n=file.Read(bytes,count,bytes.Length-count);if(n==0)break;count+=n;}
        if(count!=bytes.Length)Array.Resize(ref bytes,count);
        string text;bool binary=false;
        try {
            if(bytes.Length>=2&&bytes[0]==0xff&&bytes[1]==0xfe)text=Encoding.Unicode.GetString(bytes,2,bytes.Length-2);
            else if(bytes.Length>=2&&bytes[0]==0xfe&&bytes[1]==0xff)text=Encoding.BigEndianUnicode.GetString(bytes,2,bytes.Length-2);
            else {int start=bytes.Length>=3&&bytes[0]==0xef&&bytes[1]==0xbb&&bytes[2]==0xbf?3:0;text=new UTF8Encoding(false,true).GetString(bytes,start,bytes.Length-start);}
            binary=text.Any(c=>char.IsControl(c)&&c is not ('\r' or '\n' or '\t'));
        }catch(DecoderFallbackException){text=Encoding.UTF8.GetString(bytes);binary=true;}
        return new(size,bytes,text,binary,size>count);
    }
    public string Hex()
    {
        var result=new StringBuilder();
        for(int offset=0;offset<Bytes.Length;offset+=16){var slice=Bytes.Skip(offset).Take(16).ToArray();result.Append(offset.ToString("X8")).Append("   ").Append(string.Join(" ",slice.Select(b=>b.ToString("X2"))).PadRight(47)).Append("   ").AppendLine(new string(slice.Select(b=>b>=32&&b<127?(char)b:'.').ToArray()));}
        return result.ToString();
    }
}
