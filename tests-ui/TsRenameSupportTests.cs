using System.Text;
using TsScheduler;
internal static class TsRenameSupportTests
{
 sealed class WindowStub:IWin32Window{public IntPtr Handle=>IntPtr.Zero;}
 internal static void Run(){string root=Path.Combine(Path.GetTempPath(),"zoo-tsrename-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string path=Path.Combine(root,"ChSet3.txt");
  var data=new Dictionary<int,string>{{1024,"ＮＨＫ総合・東京"},{1040,"日テレ"}};string text="局名\t局ID\r\n日テレ\t1040\r\n";
  foreach(var encoding in new Encoding[]{Encoding.GetEncoding(932),new UTF8Encoding(true),Encoding.Unicode}){File.WriteAllText(path,text,encoding);var info=TsRenameSupport.Inspect(path);if(info.Text!=text||info.Lines!=2||info.TabLines!=2)throw new Exception("TsRename diagnostic encoding detection failed");byte[] before=File.ReadAllBytes(path);string backup=TsRenameSupport.Backup(path,root);if(!File.ReadAllBytes(path).SequenceEqual(before)||!File.ReadAllBytes(backup).SequenceEqual(before))throw new Exception("TsRename backup modified the original");}
  string report=TsRenameSupport.Diagnose(Environment.ProcessPath!);if(!report.Contains("1.25")||!report.Contains("SID・TSID・ONID"))throw new Exception("Diagnostic overclaims TsRename compatibility");var tsv=TsRenameSupport.StationReference(data);if(!tsv.Contains("1024\tＮＨＫ総合・東京"))throw new Exception("Station reference lost the channel identity");
  var channels=new[]{new ChSet3Compatibility.Channel("ＮＨＫ総合・東京",1024,32736,32736,1),new ChSet3Compatibility.Channel("TBS",1048,32739,32739,1),new ChSet3Compatibility.Channel("BSテスト",161,16400,4,1),new ChSet3Compatibility.Channel("別TSの同じSID",161,16401,4,1)};
  string generated=Path.Combine(Environment.CurrentDirectory,"work","zoo121-generated-ChSet3.txt");ChSet3Compatibility.Save(generated,channels,root);if(ChSet3Compatibility.Parse(ChSet3Compatibility.ShiftJis.GetString(File.ReadAllBytes(generated))).Count!=4)throw new Exception("Generated ChSet3 identity roundtrip failed");
  foreach(string invalid in new[]{"局名\tSID\tTSID\tONID\t種別\tEPG\t検索\r\n","局名\t0x400\t32736\t32736\t1\t1\t1\r\n","局名\t1024\t32736\t32736\t1\t1\t1\n局名2\t1025\t32736\t32736\t1\t1\t1\n"}){bool rejected=false;try{ChSet3Compatibility.Parse(invalid);}catch(InvalidDataException){rejected=true;}if(!rejected)throw new Exception("Malformed ChSet3 accepted");}
  string ch2=Path.Combine(root,"test.ch2");File.WriteAllText(ch2,"; TVTest\r\nＮＨＫ総合・東京,0,14,1,1,1024,32736,32736,1\r\n",ChSet3Compatibility.ShiftJis);if(ChSet3Compatibility.ReadCh2(ch2).Single()!=channels[0])throw new Exception("CH2 SID/TSID/ONID order changed");
  byte[] existing=File.ReadAllBytes(generated);ChSet3Compatibility.Save(generated,channels,root);if(!File.ReadAllBytes(generated).SequenceEqual(existing))throw new Exception("ChSet3 atomic replacement changed the payload");
  var owner=new WindowStub();var now=DateTimeOffset.Now;var task=new AllocationTask("new","Test",now,now.AddHours(1),-1,[1,2],true);var choice=TunerAllocation.Choose(owner,task,[task with{Id="busy",Current=1}],i=>i.ToString(),false);if(choice?.Device!=2||choice.Changes.Length!=0||new DisplayOptions().ManualTunerSelection)throw new Exception("Automatic tuner selection is not free-only/default-on");
  Console.WriteLine("PASS: TsRename text encodings, exact backup, CH2 import, seven-column Shift_JIS/CRLF export, three-ID identity, invalid header/hex/LF rejection, atomic replacement; automatic free tuner selection");
 }
}
