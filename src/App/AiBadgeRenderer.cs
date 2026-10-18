namespace TsScheduler;
internal static class AiBadgeRenderer
{
    internal static int Draw(Graphics graphics,Font font,Rectangle bounds,string label="自動")
    {
        float scale=graphics.DpiX/96f;int width=(int)Math.Round(32*scale),height=Math.Min(bounds.Height-4,(int)Math.Round(16*scale));if(height<6||bounds.Width<width)return 0;
        var badge=new Rectangle(bounds.Left,bounds.Top+(bounds.Height-height)/2,width,height);var smoothing=graphics.SmoothingMode;graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using(var shape=RoundedControls.Shape(badge,4*scale)){using var fill=new SolidBrush(Color.White);using var border=new Pen(Color.FromArgb(198,201,219),scale);graphics.FillPath(fill,shape);graphics.DrawPath(border,shape);}graphics.SmoothingMode=smoothing;
        using var small=new Font("Segoe UI",Math.Max(7,font.SizeInPoints*.72f),FontStyle.Regular);TextRenderer.DrawText(graphics,label,small,badge,Color.FromArgb(83,91,143),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
        return width;
    }
}
