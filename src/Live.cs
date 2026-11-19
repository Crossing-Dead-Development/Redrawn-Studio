using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using HarmonyLib;

namespace EvansGoethe.RedrawnStudio
{
    public sealed partial class Entry
    {
        private static ConfigEntry<bool> autoReload, reloadSwitch, separateGuests;
        private static string root, appliedStamp, pendingStamp;
        private static bool requested;
        private static float nextScan;
        private static readonly Dictionary<string, Layout> layouts = new Dictionary<string, Layout>(StringComparer.Ordinal);
        private sealed class Layout { public int Scale=100; public float X,Y; }
        private static Layout GetLayout(string key) => key!=null && key==studioDraftKey && studioDraft!=null ? studioDraft : SavedLayout(key);
        private static Layout SavedLayout(string key) => key!=null && layouts.TryGetValue(key,out var l) ? l : new Layout();
        private static void InitLive(ModConfig config)
        {
            discoverNative=config.Define("discover_native_sources",true,"Portraits / 立繪",()=>L("偵測已啟用立繪包","Discover active portrait packs"),requiresRestart:false);
            autoReload=config.Define("auto_reload",true,"Live reload / 即時刷新",()=>L("自動偵測立繪變更","Auto-detect portrait changes"),requiresRestart:false);
            reloadSwitch=config.Define("reload_now",false,"Live reload / 即時刷新",()=>L("立即刷新立繪（切換）","Reload portraits now (toggle)"),tooltip:()=>L("切換任一方向，在下一次櫃檯更新時掃描 PNG 及 CSV。","Toggle either way to rescan PNG and CSV files on the next counter update."),requiresRestart:false);
            separateGuests=config.Define("separate_guests",true,"Portraits / 立繪",()=>L("特殊客人獨立立繪","Separate special guests"),tooltip:()=>L("非改造人客人優先使用相符的 vanilla_client_ID 立繪。","Prefer a matching vanilla_client_ID portrait for non-augmented visitors."),requiresRestart:false);
        }
        private static void EnableLive(){reloadSwitch.OnChanged+=RequestReload;separateGuests.OnChanged+=GuestChanged;discoverNative.OnChanged+=GuestChanged;}
        private static void DisableLive(){reloadSwitch.OnChanged-=RequestReload;separateGuests.OnChanged-=GuestChanged;discoverNative.OnChanged-=GuestChanged;nativePacks.Clear();lookupRaw.Clear();root=null;appliedStamp=null;pendingStamp=null;requested=false;layouts.Clear();originalChoices.Clear();sources.Clear();sourceChoices.Clear();resolvedSources.Clear();ClearVisit();}
        private static void RequestReload(bool _) { requested=true; }
        private static void GuestChanged(bool _) { requested=true; }
        private static string OwnPath(string rel) => NativeAsset(rel)?NativePath(rel):Path.Combine(root,rel.Replace('/',Path.DirectorySeparatorChar));
        private static byte[] ReadLiveBytes(string rel) => root!=null && IsPortraitPath(rel) ? File.ReadAllBytes(OwnPath(rel)) : ModHelper.ReadModFileBytes(Id,rel);
        private static void LiveAssetsReady(LoadedMod mod)
        {
            root=mod.FolderPath;
            studioCatalog=null;
            try { Reload(false); appliedStamp=Stamp(); } catch(Exception e){Warn("initial-live",e.Message);requested=true;}
        }
        private static IEnumerable<string> PortraitFiles()
        {
            var dir=OwnPath("Portraits");
            return global::System.IO.Directory.Exists(dir)?global::System.IO.Directory.EnumerateFiles(dir,"*.png",SearchOption.TopDirectoryOnly).OrderBy(x=>x,StringComparer.Ordinal).ToArray():Array.Empty<string>();
        }
        private static string Stamp()
        {
            var pieces=new List<string>();
            foreach(var p in PortraitFiles().Concat(SourceFiles()).Concat(NativeFiles()).Concat(new[]{OwnPath("portrait-map.csv"),OwnPath("portrait-layout.csv"),OwnPath("portrait-original.csv"),OwnPath("portrait-source.csv")}))
            {
                var f=new FileInfo(p);pieces.Add(f.Exists?f.FullName+":"+f.Length+":"+f.LastWriteTimeUtc.Ticks:f.FullName+":missing");
            }
            pieces.Add("native:"+NativeEnabled+":"+string.Join(",",nativePacks.Keys.OrderBy(x=>x,StringComparer.Ordinal)));return string.Join("|",pieces);
        }
        internal static void Tick()
        {
            if(!Active)return;
            if(counter!=null && (counter.sprite!=visitArt || PlayerStore.Instance==null || PlayerStore.Instance.currentClientInstance!=visit))RestoreCounterOffset();
            if(root==null||Time.realtimeSinceStartup<nextScan)return;
            nextScan=Time.realtimeSinceStartup+1.5f;
            if(!requested && !autoReload.Value)return;
            try
            {
                var stamp=Stamp();
                if(!requested && stamp==appliedStamp){pendingStamp=null;return;}
                // Two equal observations reduce the chance of reading a file mid-save.
                if(stamp!=pendingStamp){pendingStamp=stamp;return;}
                Reload(true);appliedStamp=stamp;pendingStamp=null;requested=false;
            }catch(Exception e){Warn("live-reload",e.Message);pendingStamp=null;}
        }
        private static void Reload(bool refresh)
        {
            var nextMap=new Dictionary<string,string>(StringComparer.Ordinal);
            var nextLayouts=new Dictionary<string,Layout>(StringComparer.Ordinal);
            var files=PortraitFiles().ToArray();
            // Exact filenames are implicit IDs; explicit CSV rows override them.
            foreach(var f in files){var key=Path.GetFileNameWithoutExtension(f);if(Safe(key))nextMap[key]=key;}
            var mp=OwnPath("portrait-map.csv");
            if(File.Exists(mp))foreach(var raw in File.ReadAllLines(mp))
            {
                var line=raw.Trim().TrimStart('\uFEFF');if(line==""||line.StartsWith("#")||line=="key,portrait")continue;
                var a=line.Split(',').Select(x=>x.Trim()).ToArray();
                if(a.Length!=2||!Safe(a[0])||!Safe(a[1]))throw new InvalidDataException("Invalid portrait-map row: "+line);
                // v0.0.3 accidentally assigned Lou's wanted portrait to this unrelated event.
                // Ignore only that legacy alias; a correctly named PNG or another explicit alias is allowed.
                if(a[0]=="vanilla_client_louOperativeIntro" && a[1]=="wanted5")continue;
                nextMap[a[0]]=a[1];
            }
            var lp=OwnPath("portrait-layout.csv");
            if(File.Exists(lp))foreach(var raw in File.ReadAllLines(lp))
            {
                var line=raw.Trim().TrimStart('\uFEFF');if(line==""||line.StartsWith("#")||line=="key,percent,x,y")continue;
                var a=line.Split(',').Select(x=>x.Trim()).ToArray();
                if(a.Length!=4||!Safe(a[0])||!int.TryParse(a[1],out int pct)||pct<50||pct>150||!float.TryParse(a[2],NumberStyles.Float,CultureInfo.InvariantCulture,out float x)||!float.TryParse(a[3],NumberStyles.Float,CultureInfo.InvariantCulture,out float y)||float.IsNaN(x)||float.IsNaN(y)||Math.Abs(x)>100||Math.Abs(y)>100)throw new InvalidDataException("Invalid layout row: "+line);
                nextLayouts[a[0]]=new Layout{Scale=pct,X=x,Y=y};
            }
            var nextSources=ReadSources(nextMap);var nextChoices=ReadSourceChoices();
            nextMap=ResolveSources(nextSources,nextChoices,out var nextOwners);
            var nextOriginal=ReadOriginalChoices(nextMap);
            // Pre-decode only art currently cached, so incomplete or broken files cannot replace visible art.
            var replacementTextures=new Dictionary<string,Texture2D>(StringComparer.Ordinal);
            try
            {
                if(refresh)foreach(var r in cache.Values)
                {
                    if(!nextMap.TryGetValue(r.Key,out var asset))continue;
                    string rel=AssetPath(asset);
                    if(replacementTextures.ContainsKey(rel)||!File.Exists(OwnPath(rel)))continue;
                    var t=new Texture2D(2,2,TextureFormat.RGBA32,false);
                    try{if(!ImageConversion.LoadImage(t,File.ReadAllBytes(OwnPath(rel)),false))throw new InvalidDataException("Invalid PNG: "+rel);t.filterMode=FilterMode.Bilinear;t.wrapMode=TextureWrapMode.Clamp;t.name=Id+":"+rel;replacementTextures[rel]=t;}
                    catch{UnityEngine.Object.Destroy(t);throw;}
                }
            }catch{foreach(var t in replacementTextures.Values)UnityEngine.Object.Destroy(t);throw;}
            sources=nextSources;sourceChoices=nextChoices;resolvedSources=nextOwners;
            originalChoices.Clear();originalChoices.UnionWith(nextOriginal);
            studioRevision++;
            map.Clear();foreach(var a in nextMap)map[a.Key]=a.Value;
            layouts.Clear();foreach(var a in nextLayouts)layouts[a.Key]=a.Value;
            warned.Clear();
            if(refresh)
            {
                var old=cache.Values.ToArray();cache.Clear();
                var oldTextures=textures.Where(x=>IsPortraitPath(x.Key)).ToArray();
                foreach(var a in oldTextures)textures.Remove(a.Key);
                foreach(var a in replacementTextures)textures[a.Key]=a.Value;
                var swaps=new Dictionary<Sprite,Sprite>();
                foreach(var r in old)if(r.Art!=null&&r.Original!=null)swaps[r.Art]=Replace(r.Key,r.Original);
                Reassign(swaps);
                // Track the currently displayed image through replacement before selecting its event-specific art.
                if(visitArt!=null&&swaps.TryGetValue(visitArt,out var updated))visitArt=updated;
                RefreshCounter();
                foreach(var r in old)if(r.Art!=null)UnityEngine.Object.Destroy(r.Art);
                foreach(var a in oldTextures)if(a.Value!=null)UnityEngine.Object.Destroy(a.Value);
            }
            Log?.Log("Redrawn Studio: reloaded "+map.Count+" portrait keys; "+layouts.Count+" individual layouts.");
        }
        // Only capture the lookup made by the synchronous native arrival methods, not random background lookups.
        private static int visitDepth;
        private static readonly Dictionary<Sprite,string> arrivalOriginals=new Dictionary<Sprite,string>();
        private static bool visiting;
        private static Sprite nativeOriginal, visitArt, visitProvider;
        private sealed class Observed { public Sprite Raw, Provider; public string Key; }
        private static readonly Dictionary<Sprite,Observed> arrivalResults=new Dictionary<Sprite,Observed>();
        private static readonly Dictionary<string,Sprite> lookupRaw=new Dictionary<string,Sprite>(StringComparer.Ordinal);
        internal static void ObserveRaw(string key,Sprite raw){if(!Active||key==null)return;lookupRaw[key]=raw;}
        internal static Sprite ResolveLookup(string key,Sprite provider){
            if(!Active||key==null)return provider;
            var raw=lookupRaw.TryGetValue(key,out var candidate)&&candidate!=null?candidate:provider;lookupRaw.Remove(key);
            var result=IsOriginal(key)?raw:map.ContainsKey(key)?Replace(key,raw):provider;
            if(result==raw&&!IsOriginal(key))result=provider;
            if(visiting&&raw!=null){var seen=new Observed{Raw=raw,Provider=provider,Key=key};if(result!=null)arrivalResults[result]=seen;if(provider!=null)arrivalResults[provider]=seen;}
            return result;
        }
        private static string nativeKey;
        private static SpriteRenderer counter;
        private static StoreClientInstance visit;
        private static void ClearVisit(){RestoreCounterOffset();studioOriginal=false;visiting=false;visitDepth=0;arrivalOriginals.Clear();arrivalResults.Clear();nativeOriginal=null;visitProvider=null;nativeKey=null;visitArt=null;counter=null;visit=null;}
        internal static void BeginVisit(){if(!Active)return;if(visitDepth>0){visitDepth++;return;}ClearVisit();visitDepth=1;visiting=true;}
        private static void CaptureNative(string key,Sprite original){if(visiting&&original!=null&&!original.name.StartsWith(Id+":",StringComparison.Ordinal)){arrivalOriginals[original]=key;}}
        internal static void EndVisit(PlayerStore store)
        {
            if(!visiting)return;if(--visitDepth>0)return;visiting=false;
            if(!Active||store==null||StoreClientMono.Instance==null)return;
            visit=store.currentClientInstance;counter=StoreClientMono.Instance.image;
            if(counter==null||visit==null)return;
            visitArt=counter.sprite;
            if(visitArt!=null&&arrivalResults.TryGetValue(visitArt,out var observedResult)){nativeOriginal=observedResult.Raw;nativeKey=observedResult.Key;visitProvider=observedResult.Provider;}
            // Prefer the original corresponding to the actual displayed replacement, not a later unrelated lookup.
            if(nativeOriginal==null)foreach(var r in cache.Values)if(r.Art!=null&&r.Art==visitArt){nativeOriginal=r.Original;nativeKey=r.Key;break;}
            if(nativeOriginal==null&&visitArt!=null&&arrivalOriginals.TryGetValue(visitArt,out var observed)){nativeOriginal=visitArt;nativeKey=observed;}
            arrivalOriginals.Clear();arrivalResults.Clear();
            if(nativeOriginal==null)return;
            RefreshCounter();
        }
        internal static void CancelVisit(){visiting=false;visitDepth=0;arrivalOriginals.Clear();arrivalResults.Clear();}
        private static void RefreshCounter()
        {
            if(!Active||counter==null||visit==null||nativeOriginal==null||PlayerStore.Instance==null||PlayerStore.Instance.currentClientInstance!=visit)return;
            // Never overwrite an animation that has since replaced the previously displayed art.
            if(counter.sprite!=visitArt && counter.sprite!=nativeOriginal)return;
            var c=visit.GetClientBlueprint();string role=VisitRole();
            if(trace!=null&&trace.Value&&c!=null)Log.Log("Redrawn Studio visit: "+c.identifier+" / native="+nativeKey+" / role="+role);
            RestoreCounterOffset();
            var art=nativeOriginal;
            if(!studioOriginal&&!IsOriginal(EventKey())){
                if(FollowsProvider(EventKey()))art=visitProvider!=null?visitProvider:nativeOriginal;
                else {
                    if(role!=null&&map.ContainsKey(role))art=Replace(role,nativeOriginal);
                    if(art==nativeOriginal&&!IsOriginal(nativeKey)){art=Replace(nativeKey,nativeOriginal);if(art==nativeOriginal&&visitProvider!=null)art=visitProvider;}
                }
            }
            counter.sprite=art;visitArt=art;
            ApplyCounterOffset(art);

        }
    }
    [HarmonyPatch(typeof(StoreClientMono),"Update")]
    internal static class LiveTick { static void Postfix(){Entry.Tick();} }
    [HarmonyPatch(typeof(PlayerStore),"GetNextClient")]
    internal static class NewVisit
    {
        static void Prefix(){Entry.BeginVisit();}
        [HarmonyPriority(HarmonyLib.Priority.Last)]
        static void Postfix(PlayerStore __instance){Entry.EndVisit(__instance);}
        static Exception Finalizer(Exception __exception){if(__exception!=null)Entry.CancelVisit();return __exception;}
    }
    [HarmonyPatch(typeof(PlayerStore),"SetupWaitingClient")]
    internal static class WaitingVisit
    {
        static void Prefix(){Entry.BeginVisit();}
        [HarmonyPriority(HarmonyLib.Priority.Last)]
        static void Postfix(PlayerStore __instance){Entry.EndVisit(__instance);}
        static Exception Finalizer(Exception __exception){if(__exception!=null)Entry.CancelVisit();return __exception;}
    }
}
