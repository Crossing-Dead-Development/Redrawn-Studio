using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;
namespace EvansGoethe.RedrawnStudio
{
    [HarmonyPatch(typeof(PaperUIManager),"OpenWantedPaper")]
    internal static class Wanted
    {
        private static readonly Dictionary<GameObject,GameObject> roots=new Dictionary<GameObject,GameObject>();
        private static readonly List<Sprite> created=new List<Sprite>();
        // Layout measured from 198: base paper 666 x 930, 3 columns, photos 150 x 166.
        static void Postfix(PaperUIManager __instance)
        {
            if(!Entry.Active||!Entry.WantedEnabled||__instance==null)return;
            try
            {
                var panel=__instance.wantedPanel;
                if(panel==null)return;
                if(roots.TryGetValue(panel,out var existing)&&existing!=null)return;
                var im=panel.GetComponent<Image>();
                if(im==null||im.overrideSprite==null)return;
                var original=im.overrideSprite;
                if(Mathf.Abs(original.rect.width-666)>0.1f||Mathf.Abs(original.rect.height-930)>0.1f){Entry.Warn("wanted-layout","Unknown paper geometry; kept original");return;}
                var host=new GameObject("Redrawn Studio Wanted Photos",typeof(RectTransform));
                roots[panel]=host;host.layer=panel.layer;
                var rt=(RectTransform)host.transform;rt.SetParent(panel.transform,false);rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=Vector2.zero;rt.offsetMax=Vector2.zero;rt.SetAsFirstSibling();
                foreach(int n in new[]{2,5})
                {
                    float col=(n-1)%3,row=(n-1)/3;
                    float x=62+194*col,y=148+224*row;
                    var tex=Entry.Texture("Wanted/wanted"+n+".png");
                    var face=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);created.Add(face);
                    var back=Add(host,x,y,150,166,"Photo backing "+n);back.color=new Color(.533f,.58f,.542f,1);
                    var photo=Add(host,x,y,150,166,"Photo "+n);photo.sprite=face;photo.preserveAspect=true;
                    // Preserve the native ID badge over the upper-right edge; no dynamic text is rewritten.
                    var rect=original.textureRect;float bx=156+194*col,by=140+224*row;
                    var badge=Sprite.Create(original.texture,new Rect(rect.x+bx,rect.y+930-by-38,84,38),new Vector2(.5f,.5f),original.pixelsPerUnit,0,SpriteMeshType.FullRect);created.Add(badge);
                    Add(host,bx,by,84,38,"Badge "+n).sprite=badge;
                }
                Entry.Log.Log("Wanted photo overlays: 2, 5");
            }catch(Exception e){Restore();Entry.Warn("wanted",e.Message);}
        }
        private static Image Add(GameObject host,float x,float y,float w,float h,string label)
        {
            var go=new GameObject(label,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.layer=host.layer;
            var rt=(RectTransform)go.transform;rt.SetParent(host.transform,false);
            rt.anchorMin=new Vector2(x/666f,1-(y+h)/930f);rt.anchorMax=new Vector2((x+w)/666f,1-y/930f);rt.offsetMin=Vector2.zero;rt.offsetMax=Vector2.zero;
            var im=go.GetComponent<Image>();im.raycastTarget=false;return im;
        }
        internal static void Restore(){foreach(var root in roots.Values)if(root!=null)UnityEngine.Object.Destroy(root);foreach(var s in created)if(s!=null)UnityEngine.Object.Destroy(s);roots.Clear();created.Clear();}
    }
}
