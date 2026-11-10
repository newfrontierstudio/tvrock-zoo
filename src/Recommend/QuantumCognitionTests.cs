using System.Numerics;
using System.Text.Json;
using TsRecommend;
namespace TsRecommend;
internal static class QuantumCognitionTests
{
 static Complex[,] Mul(Complex[,]a,Complex[,]b){var c=new Complex[2,2];for(int i=0;i<2;i++)for(int j=0;j<2;j++)for(int k=0;k<2;k++)c[i,j]+=a[i,k]*b[k,j];return c;}
 static QDensity MatrixUpdate(QDensity r,QAxis n,int outcome,double eta){
  var rho=new Complex[,]{{(1+r.Z)/2,new(r.X/2,-r.Y/2)},{new(r.X/2,r.Y/2),(1-r.Z)/2}};
  var p=new Complex[,]{{(1+n.Z)/2,new(n.X/2,-n.Y/2)},{new(n.X/2,n.Y/2),(1-n.Z)/2}};
  double a=Math.Sqrt((1+outcome*eta)/2),b=Math.Sqrt((1-outcome*eta)/2);var m=new Complex[2,2];for(int i=0;i<2;i++)for(int j=0;j<2;j++)m[i,j]=(a-b)*p[i,j]+(i==j?b:0);
  var result=Mul(Mul(m,rho),m);double trace=(result[0,0]+result[1,1]).Real;return new(2*result[0,1].Real/trace,-2*result[0,1].Imaginary/trace,(result[0,0]-result[1,1]).Real/trace);
 }
 internal static int Run(){string dir=Path.Combine(Environment.CurrentDirectory,"work","quantum-tests");Directory.CreateDirectory(dir);var checks=new List<string>();void Check(bool condition,string name){if(!condition)throw new Exception(name);checks.Add(name);}
 try{
  Check(Math.Abs(QDensity.Prior.Probability(QAxis.Interest)-.75)<1e-12,"Born rule recovers specified prior");
  var rng=new Random(8741);QAxis Axis(){double z=rng.NextDouble()*2-1,phi=rng.NextDouble()*2*Math.PI,w=Math.Sqrt(1-z*z);return new(w*Math.Cos(phi),w*Math.Sin(phi),z);}
  for(int i=0;i<1000;i++){var axis=Axis();double radius=rng.NextDouble();var r=new QDensity(axis.X*radius,axis.Y*radius,axis.Z*radius);var n=Axis();double eta=rng.NextDouble()*.9;int outcome=i%2==0?1:-1;var bloch=r.Observe(n,outcome,eta);var matrix=MatrixUpdate(r,n,outcome,eta);Check(Math.Abs(bloch.X-matrix.X)+Math.Abs(bloch.Y-matrix.Y)+Math.Abs(bloch.Z-matrix.Z)<1e-11&&bloch.Length<=1+1e-12,"Matrix/Bloch equivalence and valid density "+i);}
  var state=new QDensity(0,0,0);var xz=state.Observe(QAxis.Allocation,1,.6).Observe(QAxis.Interest,1,.6);var zx=state.Observe(QAxis.Interest,1,.6).Observe(QAxis.Allocation,1,.6);Check(Math.Abs(xz.Probability(QAxis.Interest)-zx.Probability(QAxis.Interest))>.01,"Noncommuting context/interest updates produce order effects");
  var coherent=QDensity.Prior.Observe(QAxis.Allocation,1,.4);var dephased=new QDensity(0,0,.5).Observe(QAxis.Allocation,1,.4);Check(Math.Abs(coherent.Probability(QAxis.Interest)-dephased.Probability(QAxis.Interest))>.01,"Off-diagonal coherence has measurable effect; not a relabelled classical weight");
  var now=DateTimeOffset.Now;var show=new Show("next",1,1,"NHK","旅の記録 第20回","旅の記録",now.AddDays(1),now.AddDays(1).AddHours(1),[]);
  CognitionBooking B(int i,bool cancel=false)=>new(){Id=i.ToString(),Title="旅の記録 第"+i+"回",ChannelKey="NHK",Start=now.AddDays(-i),End=now.AddDays(-i).AddHours(1),UserSelected=!cancel,UserCancelled=cancel,State=cancel?"Cancelled":"Completed"};
  var history=new[]{B(1),B(2),B(3),B(4,true)};var classical=new CognitionLearning(history,now);double baseline=classical.Evaluate(show).Factor;var off=new QuantumCognition(history,now,new(){Enabled=false}).Evaluate(show,baseline);Check(off.Factor==baseline,"OFF exactly restores ordinary scoring");
  var single=new QuantumCognition([B(1,true)],now,new()).Evaluate(show,1);Check(single.Factor>.8,"Single cancellation remains a soft penalty");
  var auto=B(1) with{UserSelected=false,RecommendationKey="automatic"};var qAuto=new QuantumCognition([auto],now,new()).Evaluate(show,1);Check(qAuto.Factor==1&&qAuto.LegacyEvents==0&&qAuto.OrderedEvents==0,"Automatic suggestions cannot self-reinforce quantum state");
  var legacy=new QuantumCognition(history,now,new()).Evaluate(show,baseline);Check(legacy.LegacyEvents==4&&legacy.OrderedEvents==0,"Unknown action times use mixture, never fabricated order");
  var timed=history.Select((b,i)=>b with{UserActionAt=now.AddMinutes(-10+i),UserCancelledAt=b.UserCancelled?now.AddMinutes(-3):null}).ToArray();var chronological=new QuantumCognition(timed,now,new()).Evaluate(show,baseline);Check(chronological.OrderedEvents==4&&chronological.LegacyEvents==0,"Known action times drive sequential measurement updates");
  for(int i=0;i<5000;i++){state=state.Observe(Axis(),i%2==0?1:-1,.4);Check(state.Length<=1+1e-10&&double.IsFinite(state.Z)&&state.Probability(QAxis.Interest) is >=0 and <=1,"Long update sequence remains normalized "+i);}
  File.WriteAllLines(Path.Combine(dir,"result.txt"),checks.Where(s=>!s.StartsWith("Matrix/")&&!s.StartsWith("Long update")));File.WriteAllText(Path.Combine(dir,"summary.json"),JsonSerializer.Serialize(new{passed=true,matrixComparisons=1000,normalizedUpdates=5000,checks=checks.Count,precisionClaim="mathematical and behavior tests, not real-world accuracy proof"}));return 0;
 }catch(Exception error){File.WriteAllText(Path.Combine(dir,"error.txt"),error.ToString());return 1;}
 }
}
