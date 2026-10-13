using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using ThreeSouls.Persistence;
using UnityEditor;

namespace ThreeSouls.EditorTools
{
    public static class LocalMetricsReport
    {
        [MenuItem("Tools/Three Souls/Diagnostics/Export Local Metrics Summary")]
        public static void Export()
        {
            var path=LocalTelemetry.ExportPath;
            if(string.IsNullOrEmpty(path))path=Path.Combine(UnityEngine.Application.persistentDataPath,"ThreeSouls","metrics.jsonl");
            var target=EditorUtility.SaveFilePanel("Export local metrics", "Docs", "LocalMetrics", "json");if(string.IsNullOrEmpty(target))return;
            File.WriteAllText(target,Summarize(path));
        }
        public static string Summarize(string path)
        {
            var events=new[]{path+".previous",path}.Where(File.Exists).SelectMany(File.ReadLines).Select(Parse).Where(x=>x!=null).ToArray();
            var battles=events.Where(x=>(string)x["eventName"]=="dungeon_win" || (string)x["eventName"]=="dungeon_loss").ToArray();
            return Newtonsoft.Json.JsonConvert.SerializeObject(new {generatedUtc=DateTime.UtcNow,scope="Local device journal; no population retention estimate",events=events.Length,
                counts=events.GroupBy(x=>(string)x["eventName"]).ToDictionary(g=>g.Key,g=>g.Count()),
                battles=battles.GroupBy(x=>$"{x["data"]?["RegionNumber"]}-{x["data"]?["StageNumber"]}").Select(g=>new {stage=g.Key,runs=g.Count(),wins=g.Count(x=>(string)x["eventName"]=="dungeon_win"),averageSeconds=g.Average(x=>(double?)x["data"]?["ElapsedSeconds"] ?? 0)}),
                economy=events.Where(x=>(string)x["eventName"]=="economy_change").Select(x=>x["data"]).ToArray()},Newtonsoft.Json.Formatting.Indented);
        }
        private static JObject Parse(string line){try{return JObject.Parse(line);}catch(Newtonsoft.Json.JsonException){return null;}}
    }
}
