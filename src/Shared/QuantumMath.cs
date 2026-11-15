using System.Text.Json;

namespace TsRecommend;

// Shared quantum-cognition math for ZOOstudio / TVRock Zoo.
// Canonical home: NfsCore. Host-specific engines stay in each product.
internal readonly record struct QAxis(double X,double Y,double Z){internal static QAxis Interest=>new(0,0,1);internal static QAxis Allocation=>new(1,0,0);}

// rho=(I+r.sigma)/2. |r|<=1 guarantees trace one, Hermiticity and positivity.
// Born probability Tr(rho P_n)=(1+r.n)/2. Weak update is the square-root (Luders) instrument
// M_s=sqrt((1+s*eta)/2)P_n+sqrt((1-s*eta)/2)(I-P_n).
internal readonly record struct QDensity(double X,double Y,double Z)
{
 internal double Length=>Math.Sqrt(X*X+Y*Y+Z*Z);
 internal double Probability(QAxis n)=>Math.Clamp((1+X*n.X+Y*n.Y+Z*n.Z)/2,0,1);
 internal QDensity Observe(QAxis n,int outcome,double strength){
  double eta=Math.Clamp(strength,0,.95),v=X*n.X+Y*n.Y+Z*n.Z,den=1+outcome*eta*v,k=Math.Sqrt(1-eta*eta);
  return new((k*(X-v*n.X)+(v+outcome*eta)*n.X)/den,(k*(Y-v*n.Y)+(v+outcome*eta)*n.Y)/den,(k*(Z-v*n.Z)+(v+outcome*eta)*n.Z)/den);
 }
 // v2: free precession about the interest (Z) axis between observations.
 internal QDensity Precess(double phase){double c=Math.Cos(phase),s=Math.Sin(phase);return new(X*c-Y*s,X*s+Y*c,Z);}
 // v2: context at decision time is a unitary rotation about the Y axis, not a measurement.
 internal QDensity RotateY(double angle){double c=Math.Cos(angle),s=Math.Sin(angle);return new(X*c+Z*s,Y,-X*s+Z*c);}
 // v2: dephasing channel; coherence (X,Y) decays faster than population (Z).
 internal QDensity Dephase(double coherence){double k=Math.Clamp(coherence,0,1);return new(X*k,Y*k,Z);}
 internal static QDensity Prior=>new(Math.Sqrt(.75),0,.5); // pure prior P(interest)=.75
}

internal sealed record QuantumEvaluation(double Probability,double DephasedProbability,double QuantumFactor,double ClassicalFactor,double Factor,int OrderedEvents,int LegacyEvents,bool Busy,string Reason);

internal sealed record QuantumRankAudit(string Key,string Title,DateTimeOffset Start,double ContentScore,double ClassicalScore,double ProductionScore,QuantumEvaluation Evaluation){public bool Selected{get;set;}}
