using System.Drawing.Drawing2D;
using TsRecommend;
namespace TsScheduler;

internal static class StationBadgeRenderer
{
    internal static Color Background(string station)
    {
        string key=Data.StationKey(station);
        if(DisplayOptions.Current.StationColors.TryGetValue(key,out int custom))return Color.FromArgb(255,Color.FromArgb(custom));
        double hue=key switch{"NHK"=>245,"日本テレビ"=>200,"テレビ朝日"=>300,"TBS"=>270,"テレビ東京"=>180,"フジテレビ"=>105,"Eテレ"=>140,"TOKYOMX"=>230,_=>-1};
        if(hue<0)return Color.FromArgb(239,241,243);
        double angle=hue*Math.PI/180,chroma=key is "NHK" or "フジテレビ"?3:5,fy=112d/116;
        double fx=fy+chroma*Math.Cos(angle)/500,fz=fy-chroma*Math.Sin(angle)/200;
        static double Inv(double t)=>t>6d/29?t*t*t:3*Math.Pow(6d/29,2)*(t-4d/29);
        double x=.95047*Inv(fx),y=Inv(fy),z=1.08883*Inv(fz);
        static int Srgb(double c)=>(int)Math.Round(Math.Clamp(c<=.0031308?12.92*c:1.055*Math.Pow(c,1/2.4)-.055,0,1)*255);
        return IncreaseChroma(IncreaseChroma(Color.FromArgb(Srgb(3.2404542*x-1.5371385*y-.4985314*z),Srgb(-.969266*x+1.8760108*y+.041556*z),Srgb(.0556434*x-.2040259*y+1.0572252*z)),1.1),1.07);
    }
    internal static Color IncreaseChroma(Color color,double amount)
    {
        static double Linear(byte value){double v=value/255d;return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);}
        double r=Linear(color.R),g=Linear(color.G),b=Linear(color.B),l=Math.Cbrt(.4122214708*r+.5363325363*g+.0514459929*b),m=Math.Cbrt(.2119034982*r+.6806995451*g+.1073969566*b),s=Math.Cbrt(.0883024619*r+.2817188376*g+.6299787005*b);
        double light=.2104542553*l+.793617785*m-.0040720468*s,a=(1.9779984951*l-2.428592205*m+.4505937099*s)*amount,bb=(.0259040371*l+.7827717662*m-.808675766*s)*amount;
        double ll=Math.Pow(light+.3963377774*a+.2158037573*bb,3),mm=Math.Pow(light-.1055613458*a-.0638541728*bb,3),ss=Math.Pow(light-.0894841775*a-1.291485548*bb,3);
        static int Srgb(double c)=>(int)Math.Round(Math.Clamp(c<=.0031308?12.92*c:1.055*Math.Pow(c,1/2.4)-.055,0,1)*255);
        return Color.FromArgb(Srgb(4.0767416621*ll-3.3077115913*mm+.2309699292*ss),Srgb(-1.2684380046*ll+2.6097574011*mm-.3413193965*ss),Srgb(-.0041960863*ll-.7034186147*mm+1.707614701*ss));
    }
    internal static Color Foreground(Color background)
    {
        static double Linear(byte v){double c=v/255d;return c<=.04045?c/12.92:Math.Pow((c+.055)/1.055,2.4);}
        double light=.2126*Linear(background.R)+.7152*Linear(background.G)+.0722*Linear(background.B);
        return light>.179?Color.Black:Color.White;
    }
    internal static int Draw(Graphics graphics,string station,Font font,Rectangle bounds,bool disabled=false,bool centered=false)
    {
        string label=station=="MX1"?"MX":station;
        float scale=graphics.DpiX/96f;
        using var small=new Font(font.FontFamily,font.SizeInPoints*.85f,FontStyle.Regular,GraphicsUnit.Point);
        var flags=TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis;
        var measured=TextRenderer.MeasureText(graphics,label,small,Size.Empty,TextFormatFlags.NoPadding|TextFormatFlags.SingleLine);
        int width=Math.Min(bounds.Width,Math.Max((int)(43*scale),measured.Width)+(int)(8*scale));
        int height=Math.Min(bounds.Height,measured.Height+(int)(2*scale));
        if(width<2||height<2)return 0;
        var rect=new Rectangle(bounds.Left+(centered?(bounds.Width-width)/2:0),bounds.Top+(bounds.Height-height)/2,width,height);
        float diameter=Math.Min(8*scale,Math.Min(width,height));
        using var path=new GraphicsPath();
        path.AddArc(rect.Left,rect.Top,diameter,diameter,180,90);path.AddArc(rect.Right-diameter,rect.Top,diameter,diameter,270,90);
        path.AddArc(rect.Right-diameter,rect.Bottom-diameter,diameter,diameter,0,90);path.AddArc(rect.Left,rect.Bottom-diameter,diameter,diameter,90,90);path.CloseFigure();
        var previous=graphics.SmoothingMode;graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using(var fill=new SolidBrush(disabled?Color.FromArgb(239,241,243):Background(station)))graphics.FillPath(fill,path);
        graphics.SmoothingMode=previous;
        var background=Background(station);var textRect=rect;textRect.Offset(0,(int)Math.Round(scale));TextRenderer.DrawText(graphics,label,small,textRect,disabled?Color.FromArgb(111,118,125):DisplayOptions.Current.StationColors.ContainsKey(Data.StationKey(station))?Foreground(background):Color.FromArgb(82,96,109),flags);
        return width;
    }
}

internal sealed partial class MainForm
{
    void InitializeStationBadges()
    {
        foreach(var grid in new[]{_nativeReservations,_disabledReservations,_nativeGuide}){
            grid.CellPainting+=(_,e)=>{
                int column=grid==_nativeGuide?2:3;
                if(e.RowIndex<0||e.ColumnIndex!=column)return;
                e.Paint(e.ClipBounds,DataGridViewPaintParts.Background|DataGridViewPaintParts.SelectionBackground|DataGridViewPaintParts.Border);
                var state=e.Graphics!.Save();e.Graphics.SetClip(e.ClipBounds);
                try{StationBadgeRenderer.Draw(e.Graphics,Convert.ToString(e.FormattedValue)??"",e.CellStyle!.Font??grid.Font,Rectangle.Inflate(e.CellBounds,-6,-2),grid.Rows[e.RowIndex].Tag is Booking b&&!b.Enabled);}
                finally{e.Graphics.Restore(state);}e.Handled=true;
            };
        }
    }
}
