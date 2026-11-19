using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace EvansGoethe.RedrawnStudio
{
    public sealed partial class Entry
    {
        // Only folders of mods whose native assets-loaded event has fired are eligible.
        // No Workshop crawling, foreign DLL loading, or reflection into the loader.
        private const string NativePrefix="Native_";
        private sealed class NativePack { public string Id, Folder; }
        private static readonly Dictionary<string,NativePack> nativePacks=new Dictionary<string,NativePack>(StringComparer.Ordinal);
        private static ConfigEntry<bool> discoverNative;
        private static bool NativeEnabled=>discoverNative==null||discoverNative.Value;
        private static string NativeId(string id)=>NativePrefix+string.Concat(global::System.Text.Encoding.UTF8.GetBytes(id).Select(b=>b.ToString("x2")));
        private static void ObserveMod(LoadedMod mod)
        {
            string id=mod.Manifest.ID;
            if(string.IsNullOrEmpty(id)||id==Id||id.Equals("PortraitStudio",StringComparison.OrdinalIgnoreCase))return;
            try{
                var folder=Path.GetFullPath(mod.FolderPath);
                nativePacks[NativeId(id)]=new NativePack{Id=id,Folder=folder};
                requested=true;pendingStamp=null;
            }catch(Exception e){Warn("native-source:"+id,e.Message);}
        }
        private static string SourceLabel(string id)=>id==SelfSource?DisplayName:id!=null&&nativePacks.TryGetValue(id,out var n)?n.Id+L(" [已啟用]"," [Active]"):id??"-";
        private static bool NativeAsset(string rel)=>rel.StartsWith("Native/",StringComparison.Ordinal);
        private static string NativePath(string rel)
        {
            var a=rel.Split(new[]{'/'},3);
            if(a.Length!=3||!nativePacks.TryGetValue(a[1],out var p))throw new IOException("Native source unavailable: "+rel);
            var full=Path.GetFullPath(Path.Combine(p.Folder,a[2].Replace('/',Path.DirectorySeparatorChar)));
            var prefix=p.Folder.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||!Path.GetExtension(full).Equals(".png",StringComparison.OrdinalIgnoreCase))throw new IOException("Invalid source path: "+rel);
            return full;
        }
        private static bool PortraitKey(string key)=>Safe(key??"")&&!key.StartsWith("night_",StringComparison.OrdinalIgnoreCase)&&!key.StartsWith("drjackson",StringComparison.OrdinalIgnoreCase)&&!key.StartsWith("commissary",StringComparison.OrdinalIgnoreCase)&&!key.StartsWith("dumping_ground",StringComparison.OrdinalIgnoreCase);
        private static string[] Pngs(string dir)=>global::System.IO.Directory.Exists(dir)?global::System.IO.Directory.EnumerateFiles(dir,"*.png",SearchOption.TopDirectoryOnly).OrderBy(x=>x,StringComparer.Ordinal).ToArray():Array.Empty<string>();
        private static string NativeRel(string id,string relative)=>"Native/"+id+"/"+relative.Replace('\\','/');
        private static IEnumerable<string> NativeFiles()
        {
            if(!NativeEnabled)yield break;
            foreach(var item in nativePacks.OrderBy(x=>x.Key,StringComparer.Ordinal))
            {
                var found=new List<string>();
                try{
                    var folder=item.Value.Folder;found.Add(Path.Combine(folder,"portrait-map.csv"));
                    var portraits=Path.Combine(folder,"Portraits");found.AddRange(Pngs(portraits));
                    if(global::System.IO.Directory.Exists(portraits))foreach(var d in global::System.IO.Directory.EnumerateDirectories(portraits).OrderBy(x=>x,StringComparer.Ordinal))found.AddRange(Pngs(d));
                    // Textures may also hold items, backgrounds, etc. Only recognized character IDs are eligible.
                    var known=Catalog();found.AddRange(Pngs(Path.Combine(folder,"Textures")).Where(x=>known.ContainsKey(Path.GetFileNameWithoutExtension(x))));
                }catch(Exception e){Warn("native-scan:"+item.Value.Id,e.Message);}
                foreach(var f in found)yield return f;
            }
        }
        private static void ReadNativeSources(Dictionary<string,Dictionary<string,string>> result)
        {
            if(!NativeEnabled)return;
            foreach(var item in nativePacks.OrderBy(x=>x.Key,StringComparer.Ordinal))
            {
                string id=item.Key,folder=item.Value.Folder;
                try{
                    var d=new Dictionary<string,string>(StringComparer.Ordinal);
                    string m=Path.Combine(folder,"portrait-map.csv"),portraits=Path.Combine(folder,"Portraits");
                    var known=Catalog();
                    // Explicit mappings override implicit names. If a map exists, never expose numeric asset filenames as NPC IDs.
                    foreach(var f in Pngs(Path.Combine(folder,"Textures"))){var k=Path.GetFileNameWithoutExtension(f);if(PortraitKey(k)&&known.ContainsKey(k))d[k]=NativeRel(id,"Textures/"+Path.GetFileName(f));}
                    if(!File.Exists(m))foreach(var f in Pngs(portraits)){var k=Path.GetFileNameWithoutExtension(f);if(PortraitKey(k)&&known.ContainsKey(k))d[k]=NativeRel(id,"Portraits/"+Path.GetFileName(f));}
                    if(global::System.IO.Directory.Exists(portraits))foreach(var sub in global::System.IO.Directory.EnumerateDirectories(portraits).OrderBy(x=>x,StringComparer.Ordinal)){
                        var k=Path.GetFileName(sub);var png=Pngs(sub);
                        // Ambiguous multi-image folders need an explicit supported map, not an arbitrary choice.
                        if(PortraitKey(k)&&png.Length==1)d[k]=NativeRel(id,"Portraits/"+k+"/"+Path.GetFileName(png[0]));
                    }
                    if(File.Exists(m))foreach(var raw in File.ReadAllLines(m)){
                        var l=raw.Trim().TrimStart('\uFEFF');if(l==""||l.StartsWith("#")||l=="key,portrait")continue;
                        var a=l.Split(',').Select(x=>x.Trim()).ToArray();if(a.Length!=2||!PortraitKey(a[0])||!Safe(a[1]))continue;
                        string rel=NativeRel(id,"Portraits/"+a[1]+".png");if(File.Exists(NativePath(rel)))d[a[0]]=rel;
                    }
                    if(d.Count>0)result[id]=d;
                }catch(Exception e){Warn("native-map:"+item.Value.Id,e.Message);}
            }
        }
    }
}
