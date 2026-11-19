using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using HarmonyLib;

namespace EvansGoethe.RedrawnStudio
{
    public sealed partial class Entry : IMod
    {
        internal const string Id = "evansgoethe.redrawn";
        internal static ModLog Log;
        internal static bool Active;
        private static ConfigEntry<int> scale;
        private static ConfigEntry<bool> trace, highResolution, nightEnabled, wantedEnabled;
        private static readonly HashSet<string> traced = new HashSet<string>();
        internal static bool NightEnabled => nightEnabled == null || nightEnabled.Value;
        internal static bool WantedEnabled => wantedEnabled == null || wantedEnabled.Value;
        private static readonly Dictionary<string,string> map = new Dictionary<string,string>(StringComparer.Ordinal);
        private static readonly Dictionary<string,Texture2D> textures = new Dictionary<string,Texture2D>();
        private static readonly Dictionary<string,Replacement> cache = new Dictionary<string,Replacement>();
        private static readonly HashSet<string> warned = new HashSet<string>();
        private static readonly Dictionary<PixelPerfectCamera,bool> cameras = new Dictionary<PixelPerfectCamera,bool>();
        private sealed class Replacement { public Sprite Original, Art; public string Asset, Key; public int Percent; public Vector2 Residual; }
        internal static int Percent => scale == null ? 88 : Mathf.Clamp(scale.Value, 50, 150);

        public void Init(ModManifest manifest)
        {
            Log = new ModLog(manifest);
            // Native config, no external configuration mod.
            var config = ModHelper.GetConfig(manifest.ID);
            InitLanguage(config);
            scale = config.Define("portrait_percent", 88, "Portraits / 立繪", () => L("立繪大小 (%)","Portrait size (%)"), min:50, max:150, tooltip:() => L("底部置中；通緝照片與夜間場景不受影響。","Bottom-centred. Wanted photos and night art are unaffected."), requiresRestart:false);
            trace = config.Define("trace_native_keys", false, "Diagnostics / 診斷", () => L("首次記錄原生立繪 ID","Log native sprite keys once"));
            highResolution = config.Define("high_resolution", true, "Portraits / 立繪", () => L("停用低解析度攝影機放大","Disable low-resolution camera upscale"), requiresRestart:true);
            nightEnabled = config.Define("replace_night", true, "Experimental / 實驗功能", () => L("替換傑克森夜間場景","Replace Jackson night scenes"), requiresRestart:true);
            InitLive(config);
            wantedEnabled = config.Define("replace_wanted", true, "Experimental / 實驗功能", () => L("替換通緝照片","Replace wanted photos"), requiresRestart:true);
        }
        public void OnEnable()
        {
            Active = true;
            ModHook.OnModAssetsLoaded += AssetsReady;
            ModHook.OnGameLoadedLate += ApplyCamera;
            ModHook.OnGameLoadedLate += Night.Refresh;
            ModHook.OnAfterhourArriveLate += Night.Arrived;
            scale.OnChanged += Refresh;
            EnableLive();
            try{EnableStudio();}catch(Exception e){Warn("studio-init",e.Message);}
        }
        public void OnDisable()
        {
            DisableStudio();
            if(CurrentVisit&&CanEditCounter&&visitProvider!=null)counter.sprite=visitProvider;
            Active = false;
            DisableLive();
            ModHook.OnModAssetsLoaded -= AssetsReady;
            ModHook.OnGameLoadedLate -= ApplyCamera;
            ModHook.OnGameLoadedLate -= Night.Refresh;
            ModHook.OnAfterhourArriveLate -= Night.Arrived;
            if (scale != null) scale.OnChanged -= Refresh;
            Night.Restore(); Wanted.Restore();
            var undo = new Dictionary<Sprite,Sprite>();
            foreach (var r in cache.Values) if(r.Art != null && r.Original != null) undo[r.Art] = r.Original;
            Reassign(undo);
            foreach(var r in cache.Values) if(r.Art != null) UnityEngine.Object.Destroy(r.Art);
            foreach(var t in textures.Values) if(t != null) UnityEngine.Object.Destroy(t);
            foreach(var p in cameras) if(p.Key != null) p.Key.upscaleRT=p.Value;
            cameras.Clear(); cache.Clear(); textures.Clear(); map.Clear(); warned.Clear(); traced.Clear();
        }
        private static void AssetsReady(LoadedMod mod)
        {
            if(mod.Manifest.ID != Id){ObserveMod(mod);return;}
            LiveAssetsReady(mod);
        }
        private static bool Safe(string s)
        {
            if(s.Length==0) return false;
            foreach(char c in s) if(!((c>='a'&&c<='z')||(c>='A'&&c<='Z')||(c>='0'&&c<='9')||c=='_')) return false;
            return true;
        }
        internal static void Warn(string key,string message){ if(warned.Add(key)) Log?.Warning(key+": "+message); }
        internal static Texture2D Texture(string path)
        {
            if(textures.TryGetValue(path,out var old)&&old!=null) return old;
            byte[] bytes=ReadLiveBytes(path);
            if(bytes==null) throw new InvalidOperationException("Missing "+path);
            var t=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!ImageConversion.LoadImage(t,bytes,false)){ UnityEngine.Object.Destroy(t); throw new InvalidOperationException("Invalid PNG "+path); }
            t.filterMode=FilterMode.Bilinear; t.wrapMode=TextureWrapMode.Clamp;
            t.name=Id+":"+path; textures[path]=t; return t;
        }
        internal static void ApplyCamera()
        {
            if(highResolution!=null&&!highResolution.Value)return;
            var camera=Camera.main;
            if(camera==null) return;
            var pp=camera.GetComponent<PixelPerfectCamera>();
            if(pp!=null && pp.enabled && pp.upscaleRT)
            { if(!cameras.ContainsKey(pp)) cameras.Add(pp,pp.upscaleRT); pp.upscaleRT=false; }
        }
        // Preserve the original sprite's coordinate system. Art shares native 288:211 aspect.
        // Both horizontal centre and bottom edge are invariant when the scale changes.
        private static Sprite Create(Replacement r,int percent)
        {
            var t=Texture(AssetPath(r.Asset));
            var original=r.Original; float w=original.rect.width,h=original.rect.height;
            if(w<=0||h<=0||original.pixelsPerUnit<=0) throw new InvalidOperationException("Invalid original sprite geometry");
            var layout = GetLayout(r.Key);
            float s=percent/100f*layout.Scale/100f;
            float fit=Mathf.Max(t.width/w,t.height/h); // texture px per original px at 100%
            float worldW=t.width/fit*s, worldH=t.height/fit*s; // in original-pixel units
            var pivot=new Vector2(0.5f+(original.pivot.x-w/2f-layout.X)/worldW, (original.pivot.y-layout.Y)/worldH);
            // Keep Unity's pivot in bounds; apply any remaining offset to the live counter renderer.
            if(float.IsNaN(pivot.x)||float.IsNaN(pivot.y)) throw new InvalidOperationException("Invalid pivot");
            var clamped=new Vector2(Mathf.Clamp01(pivot.x),Mathf.Clamp01(pivot.y));
            float ppu=original.pixelsPerUnit*fit/s;
            r.Residual=new Vector2((clamped.x-pivot.x)*worldW/original.pixelsPerUnit,(clamped.y-pivot.y)*worldH/original.pixelsPerUnit);
            pivot=clamped;
            var art=Sprite.Create(t,new Rect(0,0,t.width,t.height),pivot,ppu,0,SpriteMeshType.FullRect);
            if(art==null) throw new InvalidOperationException("Sprite creation failed");
            art.name=Id+":"+r.Asset;
            return art;
        }
        internal static Sprite Replace(string key,Sprite original)
        {
            if(!Active||key==null) return original;
            CaptureNative(key,original);
            if(trace!=null&&trace.Value&&traced.Add(key)) Log.Log("Native portrait lookup: "+key);
            if(original==null||IsOriginal(key)||!map.TryGetValue(key,out var asset)) return original;
            // Do not recursively resize a sprite already returned by this mod.
            if(original.name.StartsWith(Id+":",StringComparison.Ordinal)) return original;
            try
            {
                ApplyCamera();
                string k=key+":"+original.GetInstanceID();
                if(cache.TryGetValue(k,out var r)&&r.Art!=null) return r.Art;
                r=new Replacement{Original=original,Asset=asset,Key=key,Percent=Percent};
                r.Art=Create(r,r.Percent); cache[k]=r;
                Log.Log("Portrait hit: "+key+" -> "+asset+" ("+r.Percent+"%)");
                return r.Art;
            } catch(Exception e){Warn(key,e.Message);return original;}
        }
        private static void Refresh(int percent)
        {
            if(!Active) return;
            var swaps=new Dictionary<Sprite,Sprite>();
            foreach(var r in cache.Values)
            {
                if(r.Original==null||r.Art==null||r.Percent==Percent) continue;
                try{var art=Create(r,Percent);swaps[r.Art]=art;r.Art=art;r.Percent=Percent;}
                catch(Exception e){Warn(r.Asset+":scale",e.Message);}
            }
            Reassign(swaps);
            if(visitArt!=null&&swaps.TryGetValue(visitArt,out var updated))visitArt=updated;
            // Prior sprites are released after all visible renderers have been updated.
            foreach(var s in swaps.Keys) UnityEngine.Object.Destroy(s);
            RefreshCounter();
        }
        private static void Reassign(Dictionary<Sprite,Sprite> swaps)
        {
            if(swaps.Count==0)return;
            foreach(var r in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
                if(r!=null&&r.sprite!=null&&swaps.TryGetValue(r.sprite,out var sprite)) r.sprite=sprite;
        }
    }
    [HarmonyPatch(typeof(SpriteDict), "ShowClientSprite")]
    internal static class NativeOriginalLookup
    {
        [HarmonyPriority(HarmonyLib.Priority.First)]
        static void Postfix(string key,Sprite __result){Entry.ObserveRaw(key,__result);}
    }
    // The exact native lookup name and argument signature were observed in 198's DLL metadata.
    [HarmonyPatch(typeof(SpriteDict), "ShowClientSprite")]
    internal static class PortraitLookup
    {
        [HarmonyPriority(HarmonyLib.Priority.Last)]
        static void Postfix(string key,ref Sprite __result)
        { __result=Entry.ResolveLookup(key,__result); }
    }
}
