using TsRecommend;
namespace TsScheduler;
internal sealed partial class ScheduleGrid
{
    internal static Color ProgrammeFill(Show show,bool booked,bool recording)=>booked||recording?Color.FromArgb(255,232,235):CategoryFill(show);
    internal static readonly Color ReservationInk=Color.FromArgb(207,62,70);
    internal static string ReservationBadgeLabel(bool recording,GuideAssignment? assignment=null)=>(recording?"録画中":"予約済")+(assignment!=null?"｜"+assignment.BadgeNumber:"");
    internal static void DrawProgrammeContents(Graphics graphics,Show show,Rectangle bounds,Font font,bool booked,bool disabled,GuideAssignment? assignment=null,bool recording=false)
    {
        float scale=graphics.DpiX/96f;int inset=Math.Max(3,(int)Math.Round(5*scale));
        bool ended=booked&&!recording&&show.End<=DateTimeOffset.Now;
        if(booked&&!ended){using var stripe=new SolidBrush(ReservationInk);graphics.FillRectangle(stripe,bounds.Left+1,bounds.Top+1,Math.Min(Math.Max(1,(int)Math.Round(scale)),Math.Max(1,bounds.Width-2)),Math.Max(0,bounds.Height-2));}
        if(bounds.Height<font.Height)return;
        var text=Rectangle.Inflate(bounds,-inset,-Math.Max(1,(int)Math.Round(3*scale)));if(text.Width<1||text.Height<1)return;
        var ink=Color.FromArgb(23,43,65);string time=show.Start.ToLocalTime().ToString("HH:mm");
        if(!booked){TextRenderer.DrawText(graphics,time+" "+(disabled?"【予約無効】 ":"")+show.Title,font,text,ink,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis);return;}
        string label=ended?"終了":ReservationBadgeLabel(recording,assignment);
        var state=graphics.Save();try
        {
            graphics.SetClip(text,System.Drawing.Drawing2D.CombineMode.Intersect);
            int lineHeight=font.Height;int timeWidth=TextRenderer.MeasureText(graphics,time,font,Size.Empty,TextFormatFlags.NoPadding|TextFormatFlags.SingleLine).Width;
            TextRenderer.DrawText(graphics,time,font,new Rectangle(text.Left,text.Top,timeWidth+2,lineHeight),ink,TextFormatFlags.NoPadding|TextFormatFlags.SingleLine);
            using var small=new Font(font.FontFamily,Math.Max(7,font.SizeInPoints*.72f),FontStyle.Regular);
            int badgeHeight=Math.Min(lineHeight,Math.Max(12,(int)Math.Round(17*scale))),badgeWidth=TextRenderer.MeasureText(graphics,label,small,Size.Empty,TextFormatFlags.NoPadding|TextFormatFlags.SingleLine).Width+(int)Math.Round(8*scale);
            var badge=new Rectangle(text.Left+timeWidth+(int)Math.Round(5*scale),text.Top+(lineHeight-badgeHeight)/2,badgeWidth,badgeHeight);
            int titleTop=text.Top+lineHeight;
            if(badge.Right>text.Right){badge=new Rectangle(text.Left,text.Top+lineHeight,Math.Min(badgeWidth,text.Width),badgeHeight);titleTop=badge.Bottom+(int)Math.Round(2*scale);}
            if(badge.Width>4&&badge.Bottom<=text.Bottom)
            {
                var smoothing=graphics.SmoothingMode;graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using(var shape=RoundedControls.Shape(badge,3*scale)){using var fill=new SolidBrush(recording?ReservationInk:Color.White);using var border=new Pen(ReservationInk,scale);graphics.FillPath(fill,shape);graphics.DrawPath(border,shape);}graphics.SmoothingMode=smoothing;
                TextRenderer.DrawText(graphics,label,small,badge,recording?Color.White:ReservationInk,TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.PathEllipsis);
            }
            var title=new Rectangle(text.Left,titleTop,text.Width,Math.Max(0,text.Bottom-titleTop));if(title.Height>0)TextRenderer.DrawText(graphics,show.Title,font,title,ink,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis);
        }finally{graphics.Restore(state);}
    }
}
