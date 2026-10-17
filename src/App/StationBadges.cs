using System.Drawing.Drawing2D;
using TsRecommend;
namespace TsScheduler;

internal static class StationBadgeRenderer
{
    internal static Color Background(string station)
    {
        string key=Data.StationKey(station);
        double hue=key switch{"NHK"=>245,"日本テレビ"=>85,"テレビ朝日"=>300,"TBS"=>270,"テレビ東京"=>180,"フジテレビ"=>105,"Eテレ"=>140,"TOKYOMX"=>230,_=>-1};
        if(hue<0)return Color.FromArgb(239,241,243);
        double angle=hue*Math.PI/180,chroma=key is "NHK" or "フジテレビ"?3:5,fy=112d/116;
        double fx=fy+chroma*Math.Cos(angle)/500,fz=fy-chroma*Math.Sin(angle)/200;
        static double Inv(double t)=>t>6d/29?t*t*t:3*Math.Pow(6d/29,2)*(t-4d/29);
        double x=.95047*Inv(fx),y=Inv(fy),z=1.08883*Inv(fz);
        static int Srgb(double c)=>(int)Math.Round(Math.Clamp(c<=.0031308?12.92*c:1.055*Math.Pow(c,1/2.4)-.055,0,1)*255);
        return Color.FromArgb(Srgb(3.2404542*x-1.5371385*y-.4985314*z),Srgb(-.969266*x+1.8760108*y+.041556*z),Srgb(.0556434*x-.2040259*y+1.0572252*z));
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
        TextRenderer.DrawText(graphics,label,small,rect,disabled?Color.FromArgb(111,118,125):Color.FromArgb(82,96,109),flags);
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
