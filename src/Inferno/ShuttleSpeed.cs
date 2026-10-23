namespace CMCutLab;
internal static class ShuttleSpeed
{
 internal static double FromDistance(double distance)
 {
  if(!double.IsFinite(distance)||Math.Abs(distance)<=8)return 0;
  double d=Math.Abs(distance), speed;
  if(d<=160) speed=.25+.75*Math.Pow((d-8)/152,2);
  else {double t=Math.Clamp((d-160)/320,0,1);speed=1+127*t*t*(3-2*t);}
  return Math.Sign(distance)*speed;
 }
}
