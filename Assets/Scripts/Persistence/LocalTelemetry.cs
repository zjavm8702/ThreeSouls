using System;
using System.IO;
using Newtonsoft.Json;
using ThreeSouls.Data;

namespace ThreeSouls.Persistence
{
    /// <summary>Bounded local event journal. No account tokens, device identifiers or network upload.</summary>
    public static class LocalTelemetry
    {
        private static string PathName;
        private static string SessionId;
        private static int Gold,Enhancement,Growth;
        public static string ExportPath=>PathName;
        internal static void StartSession(bool returning,GameSnapshot snapshot)
        {
            PathName=Path.Combine(Path.GetDirectoryName(PersistentSession.Current.Store.PrimaryPath),"metrics.jsonl");
            SessionId=Guid.NewGuid().ToString("N");Gold=snapshot.Gold;Enhancement=snapshot.EnhancementStones;Growth=snapshot.GrowthStones;
            Record("session_start",new {returning,progress=snapshot.ClearedProgress});
        }
        public static void Record(string name,object data=null)
        {
            if(string.IsNullOrWhiteSpace(PathName))return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PathName));
                if(File.Exists(PathName) && new FileInfo(PathName).Length>2*1024*1024){if(File.Exists(PathName+".previous"))File.Delete(PathName+".previous");File.Move(PathName,PathName+".previous");}
                File.AppendAllText(PathName,JsonConvert.SerializeObject(new {utc=DateTimeOffset.UtcNow.ToUnixTimeSeconds(),session=SessionId,eventName=name,data})+"\n");
            }
            catch(IOException) { } catch(UnauthorizedAccessException) { }
        }
        internal static void Economy(UserData data)
        {
            var gold=data.Gold-Gold;var enhancement=data.EnhancementStones-Enhancement;var growth=data.GrowthStones-Growth;
            if(gold!=0 || enhancement!=0 || growth!=0)Record("economy_change",new {gold,enhancement,growth,balance=data.Gold});
            Gold=data.Gold;Enhancement=data.EnhancementStones;Growth=data.GrowthStones;
        }
        internal static void Rebase(UserData data){Gold=data.Gold;Enhancement=data.EnhancementStones;Growth=data.GrowthStones;}
    }
}
