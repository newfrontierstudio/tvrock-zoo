namespace TsScheduler;
internal sealed class FlatHoverCard:Form
{
    readonly System.Windows.Forms.Timer _dismiss=new(){Interval=25000};
    Action<Graphics,Rectangle>? _draw;
    internal FlatHoverCard(){FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;TopMost=true;DoubleBuffered=true;_dismiss.Tick+=(_,_)=>HideCard();}
    protected override bool ShowWithoutActivation=>true;
    protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80|0x20;p.ClassStyle&=~0x20000;return p;}}
    internal void ShowCard(Control owner,Point location,Size size,Action<Graphics,Rectangle> draw){_draw=draw;var screen=Screen.FromPoint(location).WorkingArea;size.Width=Math.Min(size.Width,screen.Width);size.Height=Math.Min(size.Height,screen.Height);Bounds=new Rectangle(new Point(Math.Clamp(location.X,screen.Left,screen.Right-size.Width),Math.Clamp(location.Y,screen.Top,screen.Bottom-size.Height)),size);if(!Visible)Show(owner.FindForm());Invalidate();_dismiss.Stop();_dismiss.Start();}
    internal void HideCard(){_dismiss.Stop();Hide();}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using var fill=new SolidBrush(Color.FromArgb(255,254,247));using var border=new Pen(Color.FromArgb(157,163,170));e.Graphics.FillRectangle(fill,ClientRectangle);e.Graphics.DrawRectangle(border,0,0,Width-1,Height-1);_draw?.Invoke(e.Graphics,ClientRectangle);}
    protected override void Dispose(bool disposing){if(disposing)_dismiss.Dispose();base.Dispose(disposing);}
}
