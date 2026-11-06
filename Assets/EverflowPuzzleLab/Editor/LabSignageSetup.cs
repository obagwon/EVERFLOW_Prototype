using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowPuzzleLab.Editor
{
    public static class LabSignageSetup
    {
        public const string ConfigPath="Assets/EverflowPuzzleLab/Presentation/Signage.json";
        [Serializable] public sealed class Entry { public int index; public string key,title,goal,methods,controls; public float[] position,size; }
        [Serializable] public sealed class LabelEntry { public string source,text; }
        [Serializable] public sealed class Config { public Entry[] boards; public LabelEntry[] labels; public string statusPreview; }
        [MenuItem("Tools/EVERFLOW/v0.2 Puzzle Lab/Refresh Boxed Signage (current lab)")]
        public static void RefreshOpen()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var lab=UnityEngine.Object.FindFirstObjectByType<PuzzleLab>();
            if(lab==null || !lab.gameObject.scene.name.StartsWith("Everflow_V02_PuzzleLab")) throw new InvalidOperationException("Open the PuzzleLab scene first.");
            if(lab.gameObject.scene.isDirty) throw new InvalidOperationException("Save current scene edits before refreshing signage.");
            string path=lab.gameObject.scene.path;
            string backup="Backups/PuzzleLab-Signage-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup); File.Copy(path,Path.Combine(backup,Path.GetFileName(path)));
            Apply(lab); EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);
            if(!EditorSceneManager.SaveScene(lab.gameObject.scene)) throw new IOException("Scene save failed.");
        }
        static Transform Group(Transform parent,string name)
        {
            var found=parent.Find(name); if(found!=null) return found;
            var go=new GameObject(name); go.layer=2; go.transform.SetParent(parent,false); return go.transform;
        }
        static Material Material(string name,Color color)
        {
            string path="Assets/EverflowPuzzleLab/Presentation/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path); if(mat!=null) return mat;
            mat=new Material(Shader.Find("Universal Render Pipeline/Unlit")); mat.color=color;
            AssetDatabase.CreateAsset(mat,path); return mat;
        }
        static Renderer Box(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var existing=parent.Find(name); GameObject go;
            if(existing!=null) go=existing.gameObject;
            else { go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent,false); }
            foreach(var collider in go.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
            go.layer=2; go.transform.localPosition=position; go.transform.localScale=size;
            var renderer=go.GetComponent<Renderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false; return renderer;
        }
        static TextMesh Text(Transform parent,string name,string content,float x,float y,int size,Color color,TextMesh reuse=null)
        {
            var text=reuse;
            if(text==null) text=Group(parent,name).GetComponent<TextMesh>() ?? parent.Find(name).gameObject.AddComponent<TextMesh>();
            text.gameObject.name=name; text.gameObject.SetActive(true); text.transform.SetParent(parent,false);
            text.transform.localPosition=new Vector3(x,y,-.06f); text.transform.localRotation=Quaternion.identity; text.transform.localScale=Vector3.one;
            text.text=content; text.fontSize=size; text.characterSize=.1f; text.lineSpacing=.85f;
            text.anchor=TextAnchor.UpperLeft; text.alignment=TextAlignment.Left; text.color=color; text.richText=false;
            text.gameObject.layer=2; LabSignFont.Apply(text);
            var renderer=text.GetComponent<Renderer>(); renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
            return text;
        }
        public static void Apply(PuzzleLab lab)
        {
            var config=JsonUtility.FromJson<Config>(File.ReadAllText(ConfigPath));
            var background=Material("Sign dark",new Color(.035f,.065f,.09f));
            var header=Material("Sign header",new Color(.055f,.12f,.16f));
            var line=Material("Sign rule",new Color(.23f,.45f,.52f));
            foreach(var entry in config.boards.Where(e=>e.index<lab.areas.Length))
            {
                var area=lab.areas[entry.index];
                var oldTexts=area.GetComponentsInChildren<TextMesh>(true).Where(t=>t.GetComponentInParent<LabInfoBoard>()==null).ToArray();
                var presentation=Group(area.transform,"Presentation");
                var archive=Group(presentation,"Legacy Text (inactive)");
                var labels=Group(presentation,"Short Labels");
                var board=Group(presentation,"Board "+entry.key);
                board.localPosition=new Vector3(entry.position[0],entry.position[1],entry.position[2]); board.localRotation=Quaternion.identity; board.localScale=Vector3.one;
                float w=entry.size[0], h=entry.size[1], x=-w/2+.3f, top=h/2;
                bool underground=entry.index==4;
                var component=board.GetComponent<LabInfoBoard>() ?? board.gameObject.AddComponent<LabInfoBoard>(); component.area=area;
                Box(board,"Background",Vector3.zero,new Vector3(w,h,.04f),background);
                Box(board,"Title band",new Vector3(0,top-.38f,-.027f),new Vector3(w-.04f,.72f,.01f),header);
                Box(board,"Left accent",new Vector3(-w/2+.04f,0,-.027f),new Vector3(.05f,h-.06f,.01f),line);
                component.title=Text(board,"01 Title",entry.title,x,top-.22f,36,Color.white);
                float gy=underground?2.2f:1.7f, my=underground?1.55f:.82f, cy=underground?-.65f:-.80f, sy=underground?-1.86f:-1.80f;
                Color muted=new Color(.52f,.72f,.78f), white=new Color(.9f,.94f,.96f);
                Text(board,"02 Goal heading","목표",x,gy,16,muted); component.goal=Text(board,"03 Goal",entry.goal,x,gy-.25f,24,white);
                Text(board,"04 Methods heading","테스트 방법",x,my,16,muted); component.methods=Text(board,"05 Methods",entry.methods,x,my-.25f,22,white);
                Text(board,"06 Controls heading","조작 · 실패 복구",x,cy,16,muted); component.controls=Text(board,"07 Controls",entry.controls,x,cy-.23f,18,new Color(.74f,.82f,.87f));
                Text(board,"08 Status heading","현재 상태",x,sy,16,muted);
                component.status=Text(board,"09 Area status","진행 중 · 목표 미도달",x,sy-.22f,18,new Color(.65f,.88f,.78f),area.sign);
                if(area.canal!=null) component.circuitStatus=Text(board,"10 Circuit status",config.statusPreview,x,-2.34f,18,new Color(.65f,.88f,.78f),area.canal.readout);
                Box(board,"Status divider",new Vector3(0,sy+.09f,-.035f),new Vector3(w-.6f,.012f,.01f),line);
                component.detailRenderers=board.GetComponentsInChildren<Renderer>().Where(r=>r.gameObject.name!="01 Title"&&r.gameObject.name!="Title band").ToArray();
                foreach(var text in oldTexts)
                {
                    if(text==area.sign || area.canal!=null && text==area.canal.readout) continue;
                    var label=config.labels.FirstOrDefault(e=>text.gameObject.name=="Sign - "+e.source || text.gameObject.name=="Label - "+e.source);
                    var target=label==null?null:area.GetComponentsInChildren<LabTarget>(true).FirstOrDefault(t=>t.label==label.source);
                    if(target!=null)
                    {
                        text.transform.SetParent(labels,true); text.transform.localScale=Vector3.one; text.name="Label - "+label.source;
                        text.gameObject.SetActive(true); text.text=label.text; text.anchor=TextAnchor.MiddleCenter; text.alignment=TextAlignment.Center;
                        text.characterSize=.1f; text.fontSize=18; text.color=new Color(.67f,.83f,.86f); text.gameObject.layer=2; LabSignFont.Apply(text);
                        var shortLabel=text.GetComponent<LabShortLabel>() ?? text.gameObject.AddComponent<LabShortLabel>();
                        shortLabel.area=area; shortLabel.follow=target.transform; shortLabel.offset=Vector3.up*(target.acceptsFlow?1.15f:target.water?.55f:.85f);
                    }
                    else { text.transform.SetParent(archive,true); text.gameObject.SetActive(false); }
                }
                component.Refresh();
            }
        }
    }
}
