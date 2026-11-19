using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace EvansGoethe.RedrawnStudio
{
    public sealed partial class Entry
    {
        // Local sources and read-only native active source candidates. Never execute provider DLLs.
        // Retained serialized values for backward compatibility; UI shows DisplayName.
        private const string SelfSource="Redrawn", ProviderSource="ProviderDefault";
        private static Dictionary<string,Dictionary<string,string>> sources=new Dictionary<string,Dictionary<string,string>>(StringComparer.Ordinal);
        private static Dictionary<string,string> sourceChoices=new Dictionary<string,string>(StringComparer.Ordinal);
        private static Dictionary<string,string> resolvedSources=new Dictionary<string,string>(StringComparer.Ordinal);
        private static string AssetPath(string asset)=>(asset.StartsWith("Sources/",StringComparison.Ordinal)||NativeAsset(asset))?asset:"Portraits/"+asset+".png";
        private static bool IsPortraitPath(string path)=>path.StartsWith("Portraits/",StringComparison.Ordinal)||path.StartsWith("Sources/",StringComparison.Ordinal)||NativeAsset(path);
        private static IEnumerable<string> SourceFiles()
        {
            var dir=OwnPath("Sources");
            if(!global::System.IO.Directory.Exists(dir))yield break;
            foreach(var p in global::System.IO.Directory.EnumerateDirectories(dir).OrderBy(x=>x,StringComparer.Ordinal))
            {
                var id=Path.GetFileName(p);if(!Safe(id)||(id==SelfSource||id==ProviderSource)||id.StartsWith(NativePrefix,StringComparison.Ordinal))continue;
                var m=Path.Combine(p,"portrait-map.csv");yield return m;
                var imageDir=Path.Combine(p,"Portraits");
                if(global::System.IO.Directory.Exists(imageDir))foreach(var f in global::System.IO.Directory.EnumerateFiles(imageDir,"*.png").OrderBy(x=>x,StringComparer.Ordinal))yield return f;
            }
        }
        private static Dictionary<string,string> ReadSourceChoices()
        {
            var result=new Dictionary<string,string>(StringComparer.Ordinal);var p=OwnPath("portrait-source.csv");if(!File.Exists(p))return result;
            foreach(var raw in File.ReadAllLines(p)) {var l=raw.Trim().TrimStart('\uFEFF');if(l==""||l.StartsWith("#")||l=="key,source")continue;var a=l.Split(',').Select(x=>x.Trim()).ToArray();if(a.Length!=2||!Safe(a[0])||!Safe(a[1]))throw new InvalidDataException("Invalid source choice: "+l);result[a[0]]=a[1];}
            return result;
        }
        private static Dictionary<string,Dictionary<string,string>> ReadSources(Dictionary<string,string> own)
        {
            var result=new Dictionary<string,Dictionary<string,string>>(StringComparer.Ordinal){{SelfSource,new Dictionary<string,string>(own,StringComparer.Ordinal)}};
            var dir=OwnPath("Sources");if(!global::System.IO.Directory.Exists(dir)){ReadNativeSources(result);return result;}
            foreach(var p in global::System.IO.Directory.EnumerateDirectories(dir).OrderBy(x=>x,StringComparer.Ordinal))
            {
                var id=Path.GetFileName(p);if(!Safe(id)||(id==SelfSource||id==ProviderSource)||id.StartsWith(NativePrefix,StringComparison.Ordinal))continue;
                var d=new Dictionary<string,string>(StringComparer.Ordinal);var imageDir=Path.Combine(p,"Portraits");var m=Path.Combine(p,"portrait-map.csv");
                if(!File.Exists(m)&&global::System.IO.Directory.Exists(imageDir))foreach(var f in global::System.IO.Directory.EnumerateFiles(imageDir,"*.png").OrderBy(x=>x,StringComparer.Ordinal)){var key=Path.GetFileNameWithoutExtension(f);if(Safe(key))d[key]="Sources/"+id+"/Portraits/"+key+".png";}
                if(File.Exists(m))foreach(var raw in File.ReadAllLines(m)) {var l=raw.Trim().TrimStart('\uFEFF');if(l==""||l.StartsWith("#")||l=="key,portrait")continue;var a=l.Split(',').Select(x=>x.Trim()).ToArray();if(a.Length!=2||!Safe(a[0])||!Safe(a[1]))throw new InvalidDataException("Invalid source map "+id+": "+l);var rel="Sources/"+id+"/Portraits/"+a[1]+".png";if(File.Exists(OwnPath(rel)))d[a[0]]=rel;}
                if(d.Count>0)result[id]=d;
            }
            ReadNativeSources(result);return result;
        }
        private static Dictionary<string,string> ResolveSources(Dictionary<string,Dictionary<string,string>> packs, Dictionary<string,string> choices,out Dictionary<string,string> owners)
        {
            var result=new Dictionary<string,string>(StringComparer.Ordinal);owners=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var a in packs[SelfSource])if(File.Exists(OwnPath(AssetPath(a.Value)))){result[a.Key]=a.Value;owners[a.Key]=SelfSource;}
            foreach(var c in choices){if(c.Value==ProviderSource){result.Remove(c.Key);owners.Remove(c.Key);continue;}if(packs.TryGetValue(c.Value,out var pack)&&pack.TryGetValue(c.Key,out var asset)&&File.Exists(OwnPath(AssetPath(asset)))){result[c.Key]=asset;owners[c.Key]=c.Value;}}
            return result;
        }
        private static bool FollowsProvider(string key)=>key!=null&&sourceChoices.TryGetValue(key,out var value)&&value==ProviderSource;
        private static void FollowProvider(string key){if(!Safe(key??""))throw new InvalidOperationException(L("角色 ID 無效。","Invalid character ID."));var c=ReadSourceChoices();c[key]=ProviderSource;SaveSourceChoices(c);if(IsOriginal(key))SetOriginalChoice(key,false);}
        private static string SourceName(string key)=>key!=null&&resolvedSources.TryGetValue(key,out var source)?source:"-";
        private static string[] AvailableSources(string key)=>sources.Where(p=>p.Value.TryGetValue(key,out var asset)&&File.Exists(OwnPath(AssetPath(asset)))).Select(p=>p.Key).OrderBy(x=>x==SelfSource?0:1).ThenBy(x=>x,StringComparer.Ordinal).ToArray();
        private static void SaveSourceChoices(Dictionary<string,string> choices)
        {
            var path=OwnPath("portrait-source.csv");var tmp=path+".studio.tmp";
            File.WriteAllLines(tmp,new[]{"key,source"}.Concat(choices.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Key+","+x.Value)),new System.Text.UTF8Encoding(false));
            if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);
            requested=true;pendingStamp=null;
            Reload(true);appliedStamp=Stamp();requested=false;
        }
        private static void ChooseSource(string key,string source)
        {
            if(!AvailableSources(key).Contains(source))throw new InvalidOperationException(L("來源沒有有效素材。","Source has no valid artwork."));
            var c=ReadSourceChoices();c[key]=source;SaveSourceChoices(c);
            if(IsOriginal(key))SetOriginalChoice(key,false);
        }
        private static int ApplySourcePack(string source)
        {
            if(!sources.TryGetValue(source,out var pack))throw new InvalidOperationException(L("來源已不存在。","Source no longer exists."));
            var c=ReadSourceChoices();int n=0;
            foreach(var p in pack)if(!IsOriginal(p.Key)&&File.Exists(OwnPath(AssetPath(p.Value)))){c[p.Key]=source;n++;}
            SaveSourceChoices(c);return n;
        }
        private static void ResetSourceChoices(){SaveSourceChoices(new Dictionary<string,string>(StringComparer.Ordinal));}
    }
}
