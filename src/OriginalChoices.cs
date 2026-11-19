using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace EvansGoethe.RedrawnStudio
{
    public sealed partial class Entry
    {
        private const string LouEvent="vanilla_client_louOperativeIntro";
        private static readonly HashSet<string> originalChoices=new HashSet<string>(StringComparer.Ordinal);
        private static bool IsOriginal(string key)=>key!=null&&originalChoices.Contains(key);
        private static HashSet<string> ReadOriginalChoices(Dictionary<string,string> nextMap)
        {
            var result=new HashSet<string>(StringComparer.Ordinal);
            var path=OwnPath("portrait-original.csv");
            if(!nextMap.TryGetValue(LouEvent,out var asset)||!File.Exists(OwnPath(AssetPath(asset))))result.Add(LouEvent);
            if(!File.Exists(path))return result;
            foreach(var raw in File.ReadAllLines(path))
            {
                var line=raw.Trim().TrimStart('\uFEFF');if(line==""||line.StartsWith("#")||line=="key,mode")continue;
                var a=line.Split(',');if(a.Length!=2||!Safe(a[0].Trim()))throw new InvalidDataException("Invalid original choice: "+line);
                var k=a[0].Trim();var m=a[1].Trim();
                if(m=="original")result.Add(k);else if(m=="redrawn")result.Remove(k);else throw new InvalidDataException("Invalid portrait mode: "+line);
            }
            return result;
        }
        private static void SetOriginalChoice(string key,bool original)
        {
            if(root==null||!Safe(key??""))throw new InvalidOperationException(L("模組資料夾尚未載入或角色 ID 無效。","Mod folder not loaded or invalid character ID."));
            var path=OwnPath("portrait-original.csv");
            var lines=File.Exists(path)?File.ReadAllLines(path).ToList():new List<string>{"key,mode"};
            lines.RemoveAll(x=>!x.TrimStart().StartsWith("#")&&x.Split(',')[0].Trim().TrimStart('\uFEFF')==key);
            lines.Add(key+","+(original?"original":"redrawn"));
            var tmp=path+".studio.tmp";
            File.WriteAllLines(tmp,lines,new System.Text.UTF8Encoding(false));
            if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);
            if(original)originalChoices.Add(key);else originalChoices.Remove(key);
            studioOriginal=false;RefreshCounter();studioRevision++; 
        }
    }
}
