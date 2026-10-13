using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace ThreeSouls.Persistence
{
    public enum SaveLoadStatus { Fresh, Loaded, Recovered, Damaged, Unsupported }
    public sealed class SaveLoadResult { public SaveLoadStatus Status; public GameSnapshot Snapshot; public string Detail; }
    public sealed class GameSaveStore
    {
        private sealed class Envelope { public int Version; public string Payload, Checksum; }
        private const int MaximumBytes=2*1024*1024;
        private readonly string DirectoryPath;
        public string PrimaryPath=>Path.Combine(DirectoryPath,"save.json");
        public string BackupPath=>Path.Combine(DirectoryPath,"save.backup.json");
        private string TemporaryPath=>Path.Combine(DirectoryPath,"save.tmp.json");
        public GameSaveStore(string directory) { DirectoryPath=Path.GetFullPath(directory); }
        private static string Digest(string text)
        { using var hash=SHA256.Create();return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-",""); }
        public static string Encode(GameSnapshot snapshot)
        { var payload=JsonConvert.SerializeObject(snapshot);return JsonConvert.SerializeObject(new Envelope {Version=GameSnapshot.CurrentVersion,Payload=payload,Checksum=Digest(payload)}); }
        public static GameSnapshot Decode(string json,bool validate=true)
        {
            if(json==null || Encoding.UTF8.GetByteCount(json)>MaximumBytes)throw new InvalidDataException("저장 파일 크기");
            var envelope=JsonConvert.DeserializeObject<Envelope>(json,new JsonSerializerSettings {MaxDepth=48});
            if(envelope==null || envelope.Version>GameSnapshot.CurrentVersion)throw new NotSupportedException("새 버전에서 작성한 저장 기록입니다.");
            if(envelope.Version!=GameSnapshot.CurrentVersion || envelope.Payload==null || envelope.Checksum!=Digest(envelope.Payload))throw new InvalidDataException("저장 파일 검증 실패");
            var snapshot=JsonConvert.DeserializeObject<GameSnapshot>(envelope.Payload,new JsonSerializerSettings {MaxDepth=48});
            if(snapshot==null)throw new InvalidDataException("빈 저장 기록");
            if(snapshot.Version>GameSnapshot.CurrentVersion)throw new NotSupportedException("새 저장 버전");
            if(validate)snapshot.Validate();return snapshot;
        }
        public SaveLoadResult Load()
        {
            var paths=new[]{PrimaryPath,BackupPath,TemporaryPath};
            if(paths.All(p=>!File.Exists(p)))return new SaveLoadResult {Status=SaveLoadStatus.Fresh,Detail="새 게임"};
            GameSnapshot best=null;string winner=null;
            foreach(var path in paths)
            {
                if(!File.Exists(path))continue;
                try
                {
                    if(new FileInfo(path).Length>MaximumBytes)throw new InvalidDataException("저장 파일 크기");
                    var snapshot=Decode(File.ReadAllText(path,Encoding.UTF8));
                    if(path==PrimaryPath)return new SaveLoadResult {Snapshot=snapshot,Status=SaveLoadStatus.Loaded,Detail="저장 기록 불러옴"};
                    if(best==null || snapshot.Revision>best.Revision){best=snapshot;winner=path;}
                }
                catch(NotSupportedException) when(path==PrimaryPath)
                { return new SaveLoadResult {Status=SaveLoadStatus.Unsupported,Detail="저장 기록이 현재 게임보다 새 버전입니다. 업데이트해 주세요."}; }
                catch(Exception error) when(error is IOException || error is UnauthorizedAccessException || error is JsonException || error is InvalidOperationException || error is ArgumentException || error is NotSupportedException)
                { /* Preserve every candidate; failed validation cannot alter player state. */ }
            }
            if(best==null)return new SaveLoadResult {Status=SaveLoadStatus.Damaged,Detail="저장 기록을 읽지 못했습니다. 원본 파일은 보존했습니다."};
            return new SaveLoadResult {Snapshot=best,Status=winner==PrimaryPath?SaveLoadStatus.Loaded:SaveLoadStatus.Recovered,
                Detail=winner==PrimaryPath?"저장 기록 불러옴":"정상 백업에서 복구했습니다."};
        }
        public bool Save(GameSnapshot snapshot,out string error)
        {
            error=null;
            try
            {
                snapshot.Validate();Directory.CreateDirectory(DirectoryPath);
                var bytes=Encoding.UTF8.GetBytes(Encode(snapshot));
                if(bytes.Length>MaximumBytes)throw new InvalidDataException("저장 파일 크기");
                using(var stream=new FileStream(TemporaryPath,FileMode.Create,FileAccess.Write,FileShare.None)) {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                // Keep a valid previous record. Corrupt files are archived rather than promoted to backup.
                if(File.Exists(PrimaryPath))
                {
                    var valid=false;try{Decode(File.ReadAllText(PrimaryPath,Encoding.UTF8));valid=true;}catch(Exception e)when(e is IOException||e is JsonException||e is InvalidOperationException||e is ArgumentException||e is NotSupportedException){}
                    if(valid)
                    {
                        try {File.Replace(TemporaryPath,PrimaryPath,BackupPath);return true;}
                        catch(PlatformNotSupportedException) { }
                        catch(IOException) { }
                        File.Copy(PrimaryPath,BackupPath,true);
                    }
                    else File.Copy(PrimaryPath,Path.Combine(DirectoryPath,"save.damaged.json"),true);
                }
                if(File.Exists(PrimaryPath))File.Delete(PrimaryPath);
                File.Move(TemporaryPath,PrimaryPath);
                return true;
            }
            catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException || exception is JsonException || exception is InvalidOperationException || exception is ArgumentException)
            {error="저장하지 못했습니다. 저장 공간을 확인하고 다시 시도해 주세요.";return false;}
        }
        public void ArchiveForNewGame()
        {
            Directory.CreateDirectory(DirectoryPath);
            var archive=Path.Combine(DirectoryPath,"Archive",DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff"));Directory.CreateDirectory(archive);
            foreach(var path in new[]{PrimaryPath,BackupPath,TemporaryPath})if(File.Exists(path))File.Copy(path,Path.Combine(archive,Path.GetFileName(path)));
        }
    }
}
