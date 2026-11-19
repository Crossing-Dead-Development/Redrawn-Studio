using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UI = UnityEngine.UI;
using ES = UnityEngine.EventSystems;

namespace EvansGoethe.RedrawnStudio
{
    public sealed partial class Entry
    {
        private static StudioPanel studio;
        private static Dictionary<string,string> studioCatalog;
        private static readonly Dictionary<string,string[]> catalogNames = new Dictionary<string,string[]>(StringComparer.Ordinal);
        private static int studioRevision;
        private static string studioDraftKey;
        private static Layout studioDraft;
        private static bool studioOriginal;
        private static Vector3 counterOffset;
        private static SpriteRenderer offsetRenderer;
        private static void EnableStudio()
        {
            if(studio!=null)return;
            var go=new GameObject("Redrawn Studio");
            UnityEngine.Object.DontDestroyOnLoad(go);
            studio=go.AddComponent<StudioPanel>();
        }
        private static void DisableStudio()
        {
            studioOriginal=false; studioDraftKey=null; studioDraft=null;
            RestoreCounterOffset();
            if(studio!=null){studio.ReleasePreviews();UnityEngine.Object.Destroy(studio.gameObject);}
            studio=null;studioCatalog=null;
        }
        private static bool CurrentVisit => counter!=null&&visit!=null&&nativeOriginal!=null&&PlayerStore.Instance!=null&&PlayerStore.Instance.currentClientInstance==visit;
        private static bool CanEditCounter => CurrentVisit&&(counter.sprite==visitArt||counter.sprite==nativeOriginal);
        private static string VisitRole()
        {
            if(visit==null)return null;
            var c=visit.GetClientBlueprint();
            if(separateGuests==null||!separateGuests.Value||c==null||c.isAug||!Safe(c.identifier??"")||!(string.IsNullOrEmpty(c.sourceModId)||c.sourceModId=="@vanilla"||c.sourceModId=="vanilla"))return null;
            return c.identifier.StartsWith("vanilla_client_",StringComparison.Ordinal)?c.identifier:"vanilla_client_"+c.identifier;
        }
        private static string EventKey()
        {
            if(!CurrentVisit)return null;
            var c=visit.GetClientBlueprint();
            if(c==null||!Safe(c.identifier??""))return null;
            return c.identifier.StartsWith("vanilla_client_",StringComparison.Ordinal)?c.identifier:"vanilla_client_"+c.identifier;
        }
        private static string ActualKey()
        {
            if(!CurrentVisit)return null;
            if(IsOriginal(EventKey())||FollowsProvider(EventKey()))return EventKey();
            if(visitArt!=null&&visitArt!=nativeOriginal)
                foreach(var r in cache.Values)if(r.Art==visitArt)return r.Key;
            var role=VisitRole();return role!=null&&HasArt(role)?role:nativeKey;
        }
        private static void RestoreCounterOffset()
        {
            if(offsetRenderer!=null)offsetRenderer.transform.localPosition-=counterOffset;
            offsetRenderer=null;counterOffset=Vector3.zero;
        }
        private static void ApplyCounterOffset(Sprite art)
        {
            if(counter==null||art==null)return;
            foreach(var r in cache.Values)if(r.Art==art)
            {
                if(r.Residual.sqrMagnitude<0.0000001f)return;
                var t=counter.transform;
                var delta=new Vector3(counter.flipX?-r.Residual.x:r.Residual.x,counter.flipY?-r.Residual.y:r.Residual.y,0);
                counterOffset=t.localRotation*Vector3.Scale(t.localScale,delta);
                t.localPosition+=counterOffset;offsetRenderer=counter;return;
            }
        }
        private static bool HasArt(string key) => root!=null&&key!=null&&map.TryGetValue(key,out var asset)&&File.Exists(OwnPath(AssetPath(asset)));
        private static Dictionary<string,string> Catalog()
        {
            if(studioCatalog!=null)return studioCatalog;
            studioCatalog=new Dictionary<string,string>(StringComparer.Ordinal);catalogNames.Clear();
            if(root!=null){var p=OwnPath("studio-catalog.tsv");if(File.Exists(p))foreach(var line in File.ReadAllLines(p))
            {var a=line.Split('\t');if(a.Length>=2&&Safe(a[0])){var en=a[1];var zh=a.Length>2?a[2]:en;catalogNames[a[0]]=new[]{en,zh};studioCatalog[a[0]]=en+" | "+zh;}}}
            return studioCatalog;
        }
        private static string CatalogName(string key){Catalog();return key!=null&&catalogNames.TryGetValue(key,out var a)?a[Chinese?1:0]:"";}
        private static void RefreshKey(string key)
        {
            if(key==null)return;
            var swaps=new Dictionary<Sprite,Sprite>();
            foreach(var r in cache.Values)
            {
                if(r.Key!=key||r.Art==null||r.Original==null)continue;
                try{var old=r.Art;var art=Create(r,Percent);r.Art=art;r.Percent=Percent;swaps[old]=art;}
                catch(Exception e){Warn(key+":studio",e.Message);}
            }
            Reassign(swaps);
            if(visitArt!=null&&swaps.TryGetValue(visitArt,out var updated))visitArt=updated;
            RefreshCounter();
            foreach(var s in swaps.Keys)UnityEngine.Object.Destroy(s);
        }
        private static void PreviewLayout(string key, Layout l)
        {
            var previous=studioDraftKey;
            studioDraftKey=key;studioDraft=l;
            if(previous!=null&&previous!=key)RefreshKey(previous);
            RefreshKey(key);
        }
        private static void CancelDraft()
        {
            var k=studioDraftKey;studioDraftKey=null;studioDraft=null;RefreshKey(k);
        }
        private static void SaveDraft()
        {
            if(root==null||studioDraftKey==null||studioDraft==null)return;
            // Merge against latest disk file so other manual edits are not discarded.
            var path=OwnPath("portrait-layout.csv");var k=studioDraftKey;var l=studioDraft;
            var lines=File.Exists(path)?File.ReadAllLines(path).ToList():new List<string>{"key,percent,x,y"};
            lines.RemoveAll(x=>!x.TrimStart().StartsWith("#")&&x.Split(',')[0].Trim().TrimStart('\uFEFF')==k);
            lines.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2:0.###},{3:0.###}",k,l.Scale,l.X,l.Y));
            var tmp=path+".studio.tmp";
            File.WriteAllLines(tmp,lines,new System.Text.UTF8Encoding(false));
            if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);
            layouts[k]=new Layout{Scale=l.Scale,X=l.X,Y=l.Y};studioDraftKey=null;studioDraft=null;
            RefreshKey(k);requested=true;
        }
        // Runtime-created uGUI canvas: no default IMGUI skin or external UI package.
        public sealed class StudioPanel : MonoBehaviour
        {
            private const float W=1400,H=900;
            private const int PageSize=12;
            private static readonly Color Bg=new Color32(15,21,25,255), Surface=new Color32(25,35,41,255), Tile=new Color32(33,46,53,255), Field=new Color32(12,20,24,255), Ink=new Color32(236,245,243,255), Muted=new Color32(172,193,195,255), Accent=new Color32(52,235,155,255), DarkInk=new Color32(12,35,29,255), Border=new Color32(58,79,83,255);
            private bool open,detail,onlyArt,onlyMissing,inputFailed,sourcePage;
            private bool builtChinese;
            private int sourceIndex;
            private int page,seenRevision=-1;
            private string query="",selected,notice="",pendingKey,visitSignature;
            private string[] keys=Array.Empty<string>();
            private Layout pending;
            private float lastEdit,previewFactor=1;
            private Font font;
            private Canvas canvas;
            private UI.CanvasScaler scaler;
            private RectTransform panel,body,gallery,editor;
            private GameObject ownedEventSystem;
            private UI.Text currentText,eventText,noticeText,filterText,pageText,modeText,finalText,originalPlaceholder,artPlaceholder,originalHint,artHint;
            private UI.InputField search,scaleInput,individualInput,xInput,yInput;
            private UI.Slider globalSlider,individualSlider,xSlider,ySlider;
            private UI.RawImage originalImage,redrawnImage;
            private RectTransform originalView,artView;
            private UI.Button onlyArtBtn,onlyMissingBtn,originalBtn,redrawnBtn,compareBtn;
            private bool syncing;
            private readonly Dictionary<string,Texture2D> previews=new Dictionary<string,Texture2D>(StringComparer.Ordinal);
            private readonly HashSet<string> bad=new HashSet<string>(StringComparer.Ordinal);
            internal void ReleasePreviews(){foreach(var t in previews.Values)if(t!=null)UnityEngine.Object.Destroy(t);previews.Clear();bad.Clear();}
            private void OnDestroy(){if(ownedEventSystem!=null)UnityEngine.Object.Destroy(ownedEventSystem);if(font!=null&&customFont)UnityEngine.Object.Destroy(font);}
            private bool customFont;
            private void Update()
            {
                if(!Active)return;
                try{if(!inputFailed&&Input.GetKeyDown(KeyCode.F9))Toggle();}catch(Exception ex){inputFailed=true;Warn("studio-input",ex.Message);}
                if(pending!=null&&Time.realtimeSinceStartup-lastEdit>=0.08f)SafeAction(FlushEdit);
                if(!open)return;
                if(builtChinese!=Chinese)SafeAction(RebuildLanguage);
                Resize();
                EnsureEventSystem();
                var signature=(EventKey()??"")+"|"+(ActualKey()??"")+"|"+(CurrentVisit?nativeOriginal.GetInstanceID().ToString():"");
                if(signature!=visitSignature){visitSignature=signature;UpdateHeader();if(detail)SyncEditor();}
                if(seenRevision!=studioRevision){seenRevision=studioRevision;ReleasePreviews();Rebuild();if(detail)SyncEditor();else if(sourcePage)RenderSources();else RenderGallery();SetNotice(L("立繪清單已刷新。","Portrait library refreshed."));}
            }
            private void EnsureEventSystem()
            {
                if(ES.EventSystem.current!=null){if(ownedEventSystem!=null&&ES.EventSystem.current.gameObject!=ownedEventSystem){UnityEngine.Object.Destroy(ownedEventSystem);ownedEventSystem=null;}return;}
                if(ownedEventSystem==null){ownedEventSystem=new GameObject("Redrawn Studio EventSystem",typeof(ES.EventSystem),typeof(ES.StandaloneInputModule));ownedEventSystem.transform.SetParent(transform,false);}
                else ownedEventSystem.SetActive(true);
            }
            private void FlushEdit(){if(pending==null)return;var l=pending;var k=pendingKey;pending=null;pendingKey=null;PreviewLayout(k,l);}
            private void Discard(){pending=null;pendingKey=null;CancelDraft();}
            private void Toggle()
            {
                if(open){SafeAction(Discard);studioOriginal=false;RefreshCounter();open=false;var es=ES.EventSystem.current;if(es!=null&&es.currentSelectedGameObject!=null&&canvas!=null&&es.currentSelectedGameObject.transform.IsChildOf(canvas.transform))es.SetSelectedGameObject(null);if(canvas!=null)canvas.gameObject.SetActive(false);if(ownedEventSystem!=null){UnityEngine.Object.Destroy(ownedEventSystem);ownedEventSystem=null;}ReleasePreviews();return;}
                SafeAction(()=>{if(canvas==null)BuildUI();else if(builtChinese!=Chinese)RebuildLanguage();EnsureEventSystem();open=true;canvas.gameObject.SetActive(true);Resize();seenRevision=studioRevision;detail=false;sourcePage=false;selected=EventKey()??ActualKey();Rebuild();RenderGallery();UpdateHeader();SetNotice(L("選擇角色進行編輯；儲存後才會寫入配置。F9 關閉。","Select a character to edit. Save to commit your changes. F9 closes Studio."));});
            }
            private void Resize(){if(scaler!=null)scaler.scaleFactor=Mathf.Max(0.25f,Mathf.Min(Screen.width/(W+32),Screen.height/(H+32)));}
            private void SafeAction(Action a){try{a();}catch(Exception ex){SetNotice(ex.Message);Log?.Warning("Studio: "+ex);}}
            private void SetNotice(string s){notice=s;if(noticeText!=null)noticeText.text=s;}
            private void Rebuild()
            {
                var catalog=Catalog();var all=new HashSet<string>(catalog.Keys,StringComparer.Ordinal);all.UnionWith(map.Keys);foreach(var source in sources.Values)all.UnionWith(source.Keys);
                if(CurrentVisit){if(nativeKey!=null)all.Add(nativeKey);var e=EventKey();if(e!=null)all.Add(e);}
                var q=query.Trim();keys=all.Where(k=>(!onlyArt||AvailableSources(k).Length>0)&&(!onlyMissing||AvailableSources(k).Length==0)&&(q.Length==0||k.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0||(catalog.TryGetValue(k,out var n)&&n.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0))).OrderBy(k=>k,StringComparer.OrdinalIgnoreCase).ToArray();
                page=Mathf.Clamp(page,0,Mathf.Max(0,(keys.Length-1)/PageSize));
            }
            private void Select(string k){if(k==null||!Safe(k)){SetNotice(L("目前沒有可追蹤的客人。","No current visitor to track."));return;}SafeAction(()=>{if(k!=selected){Discard();studioOriginal=false;RefreshCounter();selected=k;}detail=true;sourcePage=false;ReleasePreviews();RenderEditor();SetNotice(L("選擇：","Selected: ")+k);});}
            private Layout DisplayLayout()=>pending!=null&&pendingKey==selected?pending:GetLayout(selected);
            private void Edit(int p,float x,float y){if(selected==null)return;pendingKey=selected;pending=new Layout{Scale=Mathf.Clamp(p,50,150),X=Mathf.Clamp(x,-100,100),Y=Mathf.Clamp(y,-100,100)};lastEdit=Time.realtimeSinceStartup;SyncEditor();}
            private Texture2D Preview(string k)
            {
                if(!HasArt(k))return null;var asset=map[k];if(previews.TryGetValue(asset,out var existing))return existing;if(bad.Contains(asset))return null;
                Texture2D t=null;try{t=new Texture2D(2,2,TextureFormat.RGBA32,false);if(!ImageConversion.LoadImage(t,File.ReadAllBytes(OwnPath(AssetPath(asset))),true))throw new IOException("Invalid PNG");t.filterMode=FilterMode.Bilinear;t.wrapMode=TextureWrapMode.Clamp;previews[asset]=t;return t;}catch{if(t!=null)UnityEngine.Object.Destroy(t);bad.Add(asset);return null;}
            }
            private static RectTransform Node(Transform parent,string name,float x,float y,float w,float h)
            {
                var go=new GameObject(name,typeof(RectTransform));var r=(RectTransform)go.transform;r.SetParent(parent,false);Place(r,x,y,w,h);return r;
            }
            private static void Place(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
            private static UI.Image Solid(RectTransform r,Color color,bool hit=false){var img=r.gameObject.AddComponent<UI.Image>();img.color=color;img.raycastTarget=hit;return img;}
            private static RectTransform Block(Transform t,string name,float x,float y,float w,float h,Color c,bool hit=false){var r=Node(t,name,x,y,w,h);Solid(r,c,hit);return r;}
            private UI.Text Text(Transform parent,string value,float x,float y,float w,float h,int size=20,Color? color=null,TextAnchor align=TextAnchor.MiddleLeft,bool bold=false)
            {
                var r=Node(parent,"Text",x,y,w,h);var t=r.gameObject.AddComponent<UI.Text>();t.font=font;t.fontSize=size;t.color=color??Ink;t.alignment=align;t.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.raycastTarget=false;t.resizeTextForBestFit=true;t.resizeTextMinSize=Mathf.Max(12,size-5);t.resizeTextMaxSize=size;t.text=value;return t;
            }
            private static void PaintButton(UI.Button b,Color bg){var colors=b.colors;colors.normalColor=bg;colors.highlightedColor=Color.Lerp(bg,Color.white,.13f);colors.pressedColor=Color.Lerp(bg,Color.black,.17f);colors.selectedColor=Color.Lerp(bg,Color.white,.08f);colors.disabledColor=new Color32(45,54,57,255);colors.colorMultiplier=1;colors.fadeDuration=.08f;b.colors=colors;}
            private UI.Button Button(Transform p,string label,float x,float y,float w,float h,Action click,bool primary=false,int size=20)
            {
                var r=Node(p,label,x,y,w,h);var img=Solid(r,Color.white,true);var b=r.gameObject.AddComponent<UI.Button>();b.targetGraphic=img;PaintButton(b,primary?Accent:Tile);var nav=b.navigation;nav.mode=UI.Navigation.Mode.None;b.navigation=nav;
                Text(r,label,5,0,w-10,h,size,primary?DarkInk:Ink,TextAnchor.MiddleCenter,true);b.onClick.AddListener(()=>SafeAction(click));return b;
            }
            private UI.InputField InputBox(Transform p,float x,float y,float w,float h,string placeholder,Action<string> changed,Action<string> ended=null)
            {
                var r=Node(p,"Input",x,y,w,h);var img=Solid(r,Field,true);var input=r.gameObject.AddComponent<UI.InputField>();input.targetGraphic=img;
                var t=Text(r,"",10,0,w-20,h,20,Ink);t.horizontalOverflow=HorizontalWrapMode.Overflow;var ph=Text(r,placeholder,10,0,w-20,h,20,Muted);input.textComponent=t;input.placeholder=ph;input.lineType=UI.InputField.LineType.SingleLine;input.characterLimit=120;input.caretColor=Accent;input.selectionColor=new Color32(47,108,90,255);
                if(changed!=null)input.onValueChanged.AddListener(v=>{if(!syncing)SafeAction(()=>changed(v));});if(ended!=null)input.onEndEdit.AddListener(v=>{if(!syncing)SafeAction(()=>ended(v));});return input;
            }
            private UI.Slider Slider(Transform p,float x,float y,float w,float h,float min,float max,bool whole,Action<float> changed)
            {
                var r=Node(p,"Slider",x,y,w,h);Solid(r,new Color32(0,0,0,0),true);Block(r,"Track",10,h/2-4,w-20,8,Border);var area=Node(r,"Handle area",10,0,w-20,h);var handle=Block(area,"Handle",0,0,20,h-4,Accent,true);handle.pivot=new Vector2(.5f,.5f);handle.anchorMin=handle.anchorMax=new Vector2(0,.5f);handle.anchoredPosition=Vector2.zero;
                var s=r.gameObject.AddComponent<UI.Slider>();s.handleRect=handle;s.targetGraphic=handle.GetComponent<UI.Image>();s.direction=UI.Slider.Direction.LeftToRight;s.minValue=min;s.maxValue=max;s.wholeNumbers=whole;var n=s.navigation;n.mode=UI.Navigation.Mode.None;s.navigation=n;s.onValueChanged.AddListener(v=>{if(!syncing)SafeAction(()=>changed(v));});return s;
            }
            private void RebuildLanguage()
            {
                bool wasOpen=open;
                var es=ES.EventSystem.current;
                if(es!=null&&es.currentSelectedGameObject!=null&&canvas!=null&&es.currentSelectedGameObject.transform.IsChildOf(canvas.transform))es.SetSelectedGameObject(null);
                if(canvas!=null){canvas.gameObject.SetActive(false);UnityEngine.Object.Destroy(canvas.gameObject);}
                ReleasePreviews();BuildUI();canvas.gameObject.SetActive(wasOpen);
                if(wasOpen){Rebuild();UpdateHeader();if(detail)RenderEditor();else if(sourcePage)RenderSources();else RenderGallery();Resize();SetNotice(L("已切換為繁體中文。","Language switched to English."));}
            }
            private void BuildUI()
            {
                builtChinese=Chinese;
                if(font==null)try{font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft JhengHei","Noto Sans CJK TC","Microsoft YaHei","Arial"},24);customFont=font!=null;}catch{}
                if(font==null){try{font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");}catch{font=Resources.GetBuiltinResource<Font>("Arial.ttf");}}
                var cg=new GameObject("Redrawn Studio Canvas",typeof(RectTransform),typeof(Canvas),typeof(UI.CanvasScaler),typeof(UI.GraphicRaycaster));cg.transform.SetParent(transform,false);canvas=cg.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30000;scaler=cg.GetComponent<UI.CanvasScaler>();scaler.uiScaleMode=UI.CanvasScaler.ScaleMode.ConstantPixelSize;
                var shade=Node(cg.transform,"Modal backdrop",0,0,0,0);shade.anchorMin=Vector2.zero;shade.anchorMax=Vector2.one;shade.offsetMin=shade.offsetMax=Vector2.zero;Solid(shade,new Color32(5,9,11,220),true);
                panel=Node(shade,"Studio",0,0,W,H);panel.anchorMin=panel.anchorMax=new Vector2(.5f,.5f);panel.pivot=new Vector2(.5f,.5f);panel.anchoredPosition=Vector2.zero;Solid(panel,Bg,true);
                Block(panel,"Accent",0,0,W,5,Accent);Text(panel,"Redrawn Studio",24,12,530,51,34,Ink,TextAnchor.MiddleLeft,true);Text(panel,L("立繪管理  /  v1.0.0","Portrait management  /  v1.0.0"),26,62,390,28,18,Muted);Button(panel,L("English","繁體中文"),416,53,146,39,ToggleLanguage,false,18);Button(panel,L("關閉  F9","Close  F9"),1235,21,140,50,Toggle);
                currentText=Text(panel,"",580,14,640,36,22,Ink,TextAnchor.MiddleLeft,true);eventText=Text(panel,"",580,51,640,60,17,Muted);
                Button(panel,L("角色圖庫","Library"),24,110,160,48,()=>{Discard();studioOriginal=false;RefreshCounter();detail=false;sourcePage=false;Rebuild();RenderGallery();});
                Button(panel,L("目前事件","Current event"),194,110,160,48,()=>Select(EventKey()??ActualKey()),true);
                Button(panel,L("原始 Sprite","Native sprite"),364,110,160,48,()=>Select(CurrentVisit?nativeKey:null));
                Button(panel,L("刷新 PNG / CSV","Reload PNG / CSV"),534,110,195,48,()=>{requested=true;pendingStamp=null;SetNotice(L("等待檔案穩定後重新載入。","Reloading once files are stable."));});
                Button(panel,L("複製資料夾路徑","Copy folder path"),739,110,220,48,()=>{if(root!=null){GUIUtility.systemCopyBuffer=OwnPath("Portraits");SetNotice(L("已複製立繪資料夾路徑。","Portrait folder path copied."));}});
                Button(panel,L("目前客人使用原版","Use original for current visitor"),969,110,407,48,()=>{var k=EventKey()??ActualKey();if(k==null){SetNotice(L("目前沒有可追蹤的客人。","No current visitor to track."));return;}Select(k);SetOriginalChoice(k,true);SyncEditor();SetNotice(L("已儲存原版選擇：","Saved original choice: ")+k);});
                Block(panel,"Rule",24,172,1352,2,Border);body=Node(panel,"Content",24,186,1352,654);noticeText=Text(panel,"",24,850,1352,36,18,Muted);canvas.gameObject.SetActive(false);
            }
            private void UpdateHeader(){if(currentText==null)return;currentText.text=L("目前客人：","Current visitor: ")+(CurrentVisit?(visit.GetClientBlueprint()?.identifier??"?"):L("未追蹤","Not tracked"));eventText.text=L("立繪 ID：","Sprite: ")+(CurrentVisit?nativeKey:"-")+L("\n事件 ID：","\nEvent: ")+(EventKey()??"-");}
            private string Mode(string k)=>IsOriginal(k)?L("使用原版","Use original"):FollowsProvider(k)?L("跟隨來源模組","Follow provider"):HasArt(k)?SourceLabel(SourceName(k)):AvailableSources(k).Length>0?L("有其他來源（未選）","Other sources available"):L("無重繪素材","No selected artwork");
            private static void Clear(Transform t){for(int i=t.childCount-1;i>=0;i--){var child=t.GetChild(i).gameObject;child.SetActive(false);UnityEngine.Object.Destroy(child);}}
            private void RenderGallery()
            {
                if(body==null)return;Clear(body);ReleasePreviews();gallery=Node(body,"Gallery",0,0,1352,654);editor=null;
                search=InputBox(gallery,0,0,560,48,L("搜尋角色名稱或 ID","Search name or ID"),v=>{query=v;page=0;Rebuild();RenderTiles();});search.SetTextWithoutNotify(query);
                onlyArtBtn=Button(gallery,L("有素材","Has art"),575,0,145,48,()=>{onlyArt=!onlyArt;onlyMissing=false;page=0;Rebuild();RenderTiles();});onlyMissingBtn=Button(gallery,L("缺少素材","Missing art"),730,0,145,48,()=>{onlyMissing=!onlyMissing;onlyArt=false;page=0;Rebuild();RenderTiles();});
                Button(gallery,L("來源管理","Sources"),890,0,180,48,()=>{sourcePage=true;detail=false;RenderSources();});filterText=Text(gallery,"",1080,0,270,48,20,Muted,TextAnchor.MiddleRight);tiles=Node(gallery,"Portrait tiles",0,60,1352,535);pageText=Text(gallery,"",400,605,550,45,20,Ink,TextAnchor.MiddleCenter);
                Button(gallery,L("上一頁","Previous page"),0,605,180,45,()=>{if(page>0){page--;RenderTiles();}});Button(gallery,L("下一頁","Next page"),1172,605,180,45,()=>{if((page+1)*PageSize<keys.Length){page++;RenderTiles();}});RenderTiles();
            }
            private string armedSource;
            private void RenderSources()
            {
                Clear(body);ReleasePreviews();gallery=null;editor=null;
                var list=sources.Keys.OrderBy(x=>x==SelfSource?0:1).ThenBy(x=>x,StringComparer.Ordinal).ToArray();
                Text(body,L("立繪來源管理","Portrait sources"),0,0,1352,50,32,Ink,TextAnchor.MiddleLeft,true);
                Text(body,L("自動偵測已啟用模組及本機 Sources。外部素材須手動選擇。","Active mods and local Sources are detected automatically. Select external artwork explicitly."),0,55,1352,45,22,Muted);
                if(list.Length==0){Text(body,L("尚未載入來源，請刷新。","No sources loaded. Please refresh."),0,115,1352,50,22,Muted);return;}
                sourceIndex=Mathf.Clamp(sourceIndex,0,list.Length-1);var id=list[sourceIndex];var count=sources[id].Count;
                Button(body,L("上一來源","Previous source"),0,125,220,60,()=>{sourceIndex=(sourceIndex+list.Length-1)%list.Length;armedSource=null;RenderSources();});
                Button(body,L("下一來源","Next source"),1132,125,220,60,()=>{sourceIndex=(sourceIndex+1)%list.Length;armedSource=null;RenderSources();});
                Text(body,SourceLabel(id)+"  ·  "+(sourceIndex+1)+" / "+list.Length,230,125,892,60,30,Accent,TextAnchor.MiddleCenter,true);
                Text(body,L("提供 ","Provides ")+count+L(" 個映射入口。批量套用會覆蓋這些角色先前的來源選擇，但保留永久原版設定。"," mapped entries. Applying this pack replaces their prior source choices but keeps saved original settings."),20,205,1312,95,24,Ink);
                Button(body,armedSource==id?L("確認套用此來源","Confirm apply source"):L("批量套用此來源","Apply entire source"),20,325,640,70,()=>{if(armedSource!=id){armedSource=id;RenderSources();return;}var n=ApplySourcePack(id);armedSource=null;RenderSources();SetNotice(L("已套用 ","Applied to ")+n+L(" 個角色來源。"," character sources."));},true,24);
                Button(body,armedSource=="#reset"?L("確認恢復預設","Confirm reset sources"):L("清除全部外部來源選擇","Clear all external source choices"),692,325,640,70,()=>{if(armedSource!="#reset"){armedSource="#reset";RenderSources();return;}ResetSourceChoices();armedSource=null;RenderSources();SetNotice(L("已恢復 Redrawn Studio / 原版預設來源。","Restored Redrawn Studio / original defaults."));},false,24);
                Button(body,L("複製此來源資料夾路徑","Copy source folder path"),20,425,640,65,()=>{if(root!=null){GUIUtility.systemCopyBuffer=nativePacks.TryGetValue(id,out var pack)?pack.Folder:id==SelfSource?OwnPath("Portraits"):OwnPath("Sources/"+id);SetNotice(L("已複製路徑。","Path copied."));}});
                Button(body,L("返回角色圖庫","Return to library"),692,425,640,65,()=>{armedSource=null;sourcePage=false;Rebuild();RenderGallery();});
                Text(body,L("未接管的角色保留來源模組畫面；動畫、隨機池或其他晚期覆蓋可能仍與 Redrawn Studio 衝突。","Unmanaged characters follow their provider. Animations, random pools and later overrides may still conflict with Redrawn Studio."),20,520,1312,95,21,Muted);
            }
            private RectTransform tiles;
            private void RenderTiles()
            {
                if(tiles==null)return;Clear(tiles);ReleasePreviews();PaintButton(onlyArtBtn,onlyArt?new Color32(40,105,81,255):Tile);PaintButton(onlyMissingBtn,onlyMissing?new Color32(40,105,81,255):Tile);filterText.text=keys.Length+L(" 個角色入口"," character entries");pageText.text=(page+1)+" / "+Mathf.Max(1,(keys.Length+PageSize-1)/PageSize);
                for(int i=0;i<PageSize&&page*PageSize+i<keys.Length;i++){
                    var k=keys[page*PageSize+i];float x=(i%4)*340,y=(i/4)*180;var card=Block(tiles,k,x,y,332,172,Surface,true);var b=card.gameObject.AddComponent<UI.Button>();b.targetGraphic=card.GetComponent<UI.Image>();card.GetComponent<UI.Image>().color=Color.white;PaintButton(b,Surface);b.onClick.AddListener(()=>Select(k));
                    Block(card,"Preview",4,4,324,100,Field);var t=Preview(k);if(t!=null){float f=Mathf.Min(324f/t.width,100f/t.height);Raw(card,t, (332-t.width*f)/2,104-t.height*f,t.width*f,t.height*f);}else Text(card,L("無重繪素材","No selected artwork"),4,4,324,100,18,Muted,TextAnchor.MiddleCenter);
                    Text(card,k,8,106,316,25,17,Ink,TextAnchor.MiddleLeft,true);var name=CatalogName(k);Text(card,name,8,131,316,20,15,Muted);Text(card,Mode(k),8,151,316,20,15,IsOriginal(k)?Muted:HasArt(k)?Accent:Muted);
                }
            }
            private static UI.RawImage Raw(Transform p,UnityEngine.Texture t,float x,float y,float w,float h){var r=Node(p,"Portrait",x,y,w,h);var a=r.gameObject.AddComponent<UI.RawImage>();a.texture=t;a.raycastTarget=false;return a;}
            private bool CurrentSelection()=>CurrentVisit&&(selected==EventKey()||selected==nativeKey||selected==ActualKey());
            private void RenderEditor()
            {
                Clear(body);editor=Node(body,"Editor",0,0,1352,654);gallery=null;var name=CatalogName(selected);Text(editor,selected,0,0,815,37,26,Ink,TextAnchor.MiddleLeft,true);Text(editor,name,0,40,815,29,19,Muted);
                Button(editor,L("切換立繪來源","Switch art source"),832,10,254,55,()=>{Discard();var choices=AvailableSources(selected);if(choices.Length==0){SetNotice(L("沒有可用素材來源。","No available artwork source."));return;}var index=Array.IndexOf(choices,SourceName(selected));var choice=choices[(index+1)%choices.Length];ChooseSource(selected,choice);ReleasePreviews();SyncEditor();SetNotice(L("已選擇來源：","Selected source: ")+SourceLabel(choice));},true);
                Button(editor,L("跟隨來源模組","Follow provider"),1098,10,254,55,()=>{Discard();FollowProvider(selected);ReleasePreviews();SyncEditor();SetNotice(L("已停止接管此角色；跟隨來源模組或遊戲原圖。","Character released to its provider or the original game artwork."));});
                Text(editor,L("原版立繪","Original portrait"),0,78,400,30,21,Ink,TextAnchor.MiddleLeft,true);Text(editor,L("選定素材預覽","Selected artwork"),414,78,400,30,21,Accent,TextAnchor.MiddleLeft,true);
                originalView=Block(editor,"Original",0,116,400,370,Field);artView=Block(editor,"Redrawn",414,116,400,370,Field,true);originalView.gameObject.AddComponent<UI.RectMask2D>();artView.gameObject.AddComponent<UI.RectMask2D>();originalImage=Raw(originalView,null,0,0,400,370);redrawnImage=Raw(artView,null,0,0,400,370);originalPlaceholder=Text(originalView,L("尚未取得原版立繪\n請等待角色實際來訪","Original portrait unavailable\nWait for the character to visit"),10,30,380,310,22,Muted,TextAnchor.MiddleCenter);artPlaceholder=Text(artView,L("尚無重繪素材","No selected artwork"),10,30,380,310,22,Muted,TextAnchor.MiddleCenter);
                var drag=artView.gameObject.AddComponent<PreviewDrag>();drag.Owner=this;
                originalHint=Text(editor,L("原版僅預覽已追蹤的目前客人","Original preview is available for the tracked visitor only"),0,491,400,58,17,Muted);artHint=Text(editor,L("拖曳移動 / 滾輪縮放 / 底部對齊","Drag to move / wheel to scale / bottom aligned"),414,491,400,58,17,Muted);Button(editor,L("複製角色 ID","Copy character ID"),0,560,400,48,()=>{GUIUtility.systemCopyBuffer=selected;SetNotice(L("已複製：","Copied: ")+selected);});compareBtn=Button(editor,L("暫時比較原版","Compare original"),414,560,400,48,()=>{if(CurrentSelection()){studioOriginal=!studioOriginal;RefreshCounter();SyncEditor();}else SetNotice(L("請先選擇目前客人。","Select the current visitor first."));});
                var right=Block(editor,"Settings",832,78,520,576,Surface);modeText=Text(right,"",16,12,488,36,23,Ink,TextAnchor.MiddleLeft,true);
                originalBtn=Button(right,L("使用原版","Use original"),16,55,238,50,()=>{Discard();SetOriginalChoice(selected,true);SyncEditor();SetNotice(L("已儲存原版選擇：","Saved original choice: ")+selected);});redrawnBtn=Button(right,L("使用選定素材","Use selected art"),266,55,238,50,()=>{SetOriginalChoice(selected,false);SyncEditor();SetNotice(L("已恢復選定素材：","Restored selected art: ")+selected);},true);
                globalSlider=AddControl(right,L("全局倍率 (%)","Global scale (%)"),124,50,150,true,v=>{if(scale!=null)scale.Set(Mathf.RoundToInt(v));SyncEditor();},out scaleInput);
                individualSlider=AddControl(right,L("個別倍率 (%)","Individual scale (%)"),204,50,150,true,v=>{var l=DisplayLayout();Edit(Mathf.RoundToInt(v),l.X,l.Y);},out individualInput);
                xSlider=AddControl(right,L("X 向右 (+)","X right (+)"),284,-100,100,false,v=>{var l=DisplayLayout();Edit(l.Scale,v,l.Y);},out xInput);
                ySlider=AddControl(right,L("Y 向上 (+)","Y up (+)"),364,-100,100,false,v=>{var l=DisplayLayout();Edit(l.Scale,l.X,v);},out yInput);
                finalText=Text(right,"",16,448,488,32,21,Accent,TextAnchor.MiddleLeft,true);
                Button(right,L("儲存位置與大小","Save layout"),16,490,238,42,()=>{FlushEdit();if(studioDraftKey!=null){SaveDraft();SetNotice(L("已儲存個別配置。","Individual layout saved."));}else SetNotice(L("沒有未儲存的調整。","No unsaved changes."));SyncEditor();},true,18);
                Button(right,L("捨棄調整","Discard changes"),266,490,238,42,()=>{Discard();SyncEditor();SetNotice(L("已還原配置。","Layout restored."));},false,18);
                Button(right,L("重設個別倍率與位置","Reset individual scale and position"),16,538,488,32,()=>Edit(100,0,0),false,17);SyncEditor();
            }
            private UI.Slider AddControl(Transform p,string label,float y,float min,float max,bool whole,Action<float> change,out UI.InputField field)
            {
                Text(p,label,16,y,360,28,20,Ink);var s=Slider(p,16,y+34,372,34,min,max,whole,change);
                field=InputBox(p,400,y+26,104,43,"",null,v=>{if(float.TryParse(v,NumberStyles.Float,CultureInfo.InvariantCulture,out var f)&&!float.IsNaN(f)&&!float.IsInfinity(f))change(Mathf.Clamp(f,min,max));else SyncEditor();});return s;
            }
            private void SyncEditor()
            {
                if(!detail||editor==null||selected==null)return;var l=DisplayLayout();syncing=true;
                try{globalSlider.SetValueWithoutNotify(Percent);individualSlider.SetValueWithoutNotify(l.Scale);xSlider.SetValueWithoutNotify(l.X);ySlider.SetValueWithoutNotify(l.Y);if(!scaleInput.isFocused)scaleInput.SetTextWithoutNotify(Percent.ToString());if(!individualInput.isFocused)individualInput.SetTextWithoutNotify(l.Scale.ToString());if(!xInput.isFocused)xInput.SetTextWithoutNotify(l.X.ToString("0.##",CultureInfo.InvariantCulture));if(!yInput.isFocused)yInput.SetTextWithoutNotify(l.Y.ToString("0.##",CultureInfo.InvariantCulture));}finally{syncing=false;}
                modeText.text=L("目前選擇：","Current: ")+Mode(selected);finalText.text=L("最終倍率 ","Final scale ")+(Percent*l.Scale/100f).ToString("0.0")+"%"+(studioDraftKey!=null||pending!=null?L("  · 未儲存","  · Unsaved"):"");
                PaintButton(originalBtn,IsOriginal(selected)?new Color32(40,105,81,255):Tile);PaintButton(redrawnBtn,IsOriginal(selected)?Tile:Accent);compareBtn.interactable=CurrentSelection();
                var show=CurrentSelection()&&nativeOriginal!=null;originalImage.gameObject.SetActive(show);originalPlaceholder.gameObject.SetActive(!show);float nw=288,nh=211;
                if(show){var t=nativeOriginal.texture;var rect=nativeOriginal.textureRect;nw=nativeOriginal.rect.width;nh=nativeOriginal.rect.height;float f=Mathf.Min(400/rect.width,370/rect.height);originalImage.texture=t;originalImage.uvRect=new Rect(rect.x/t.width,rect.y/t.height,rect.width/t.width,rect.height/t.height);Place(originalImage.rectTransform,(400-rect.width*f)/2,370-rect.height*f,rect.width*f,rect.height*f);}
                var art=Preview(selected);redrawnImage.gameObject.SetActive(art!=null);artPlaceholder.gameObject.SetActive(art==null);previewFactor=Mathf.Min(400/nw,370/nh);if(art!=null){float fit=Mathf.Max(art.width/nw,art.height/nh),s=Percent/100f*l.Scale/100f;float w=art.width/fit*s*previewFactor,h=art.height/fit*s*previewFactor;redrawnImage.texture=art;redrawnImage.uvRect=new Rect(0,0,1,1);Place(redrawnImage.rectTransform,200-w/2+l.X*previewFactor,370-h-l.Y*previewFactor,w,h);}originalHint.text=show?L("原版參考 · 目前客人","Original reference · current visitor"):L("原版僅預覽已追蹤的目前客人","Original preview is available for the tracked visitor only");artHint.text=studioOriginal?L("櫃檯暫時顯示原版；再按比較鍵恢復","Counter shows original temporarily; compare again to restore"):L("拖曳移動 / 滾輪縮放 / 底部對齊","Drag to move / wheel to scale / bottom aligned");
            }
            public void DragPreview(Vector2 delta){if(selected==null||!HasArt(selected))return;var l=DisplayLayout();float f=Mathf.Max(.001f,scaler.scaleFactor*previewFactor);Edit(l.Scale,l.X+delta.x/f,l.Y+delta.y/f);}
            public void ScrollPreview(float delta){if(selected==null||!HasArt(selected))return;var l=DisplayLayout();Edit(l.Scale+Mathf.RoundToInt(delta*3),l.X,l.Y);}
        }
        public sealed class PreviewDrag : MonoBehaviour, ES.IDragHandler, ES.IScrollHandler
        {
            internal StudioPanel Owner;
            public void OnDrag(ES.PointerEventData e){if(e.button==ES.PointerEventData.InputButton.Left)Owner?.DragPreview(e.delta);e.Use();}
            public void OnScroll(ES.PointerEventData e){Owner?.ScrollPreview(e.scrollDelta.y);e.Use();}
        }
    }
}
