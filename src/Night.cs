using System;
using System.Collections.Generic;
using UnityEngine;
namespace EvansGoethe.RedrawnStudio
{
    internal static class Night
    {
        private sealed class Change { public Sprite Original, Art; }
        private static readonly Dictionary<SpriteRenderer,Change> changes=new Dictionary<SpriteRenderer,Change>();
        internal static void Arrived(string location) => Refresh();
        internal static void Refresh()
        {
            if(!Entry.Active || !Entry.NightEnabled) return;
            foreach(var r in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
            {
                if(r==null||r.sprite==null||!r.gameObject.scene.IsValid())continue;
                if(r.sprite.name!="drjackson"&&r.sprite.name!="drjackson2") continue;
                try
                {
                    var original=r.sprite; var t=Entry.Texture("Night/"+original.name+".png");
                    float fx=t.width/original.rect.width,fy=t.height/original.rect.height;
                    if(Mathf.Abs(fx/fy-1f)>.03f){Entry.Warn(original.name,"Night aspect mismatch; retained original");continue;}
                    var pivot=new Vector2(original.pivot.x/original.rect.width,original.pivot.y/original.rect.height);
                    var art=Sprite.Create(t,new Rect(0,0,t.width,t.height),pivot,original.pixelsPerUnit*fx,0,SpriteMeshType.FullRect);
                    art.name=Entry.Id+":night:"+original.name;
                    if(changes.TryGetValue(r,out var prior)&&prior.Art!=null)UnityEngine.Object.Destroy(prior.Art);
                    changes[r]=new Change{Original=original,Art=art};r.sprite=art;
                }catch(Exception e){Entry.Warn("night:"+r.sprite.name,e.Message);}
            }
        }
        internal static void Restore()
        {
            foreach(var p in changes){if(p.Key!=null&&p.Key.sprite==p.Value.Art)p.Key.sprite=p.Value.Original;if(p.Value.Art!=null)UnityEngine.Object.Destroy(p.Value.Art);}
            changes.Clear();
        }
    }
}
