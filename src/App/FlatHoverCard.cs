namespace TsScheduler;
internal sealed class FlatHoverCard:Form
{
    readonly System.Windows.Forms.Timer _dismiss=new(){Interval=25000};
    readonly System.Windows.Forms.Timer _watch=new(){Interval=150};
    Control? _source;
    Action<Graphics,Rectangle>? _draw;
    internal FlatHoverCard(){FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;TopMost=false;DoubleBuffered=true;_dismiss.Tick+=(_,_)=>HideCard();_watch.Tick+=(_,_)=>{if(_source==null||!CanShowFor(_source))HideCard();};}
    internal static bool CanShowFor(Control owner)
    {
        if(owner.IsDisposed||!owner.Visible||!owner.IsHandleCreated)return false;
        var root=owner.TopLevelControl as Form??owner.FindForm();
        return root!=null&&!root.IsDisposed&&root.Visible&&root.WindowState!=FormWindowState.Minimized&&root.IsHandleCreated
            &&GetAncestor(GetForegroundWindow(),2)==GetAncestor(root.Handle,2)
            &&owner.ClientRectangle.Contains(owner.PointToClient(Cursor.Position));
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr window,uint flags);
    protected override bool ShowWithoutActivation=>true;
    protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80|0x20;p.ClassStyle&=~0x20000;return p;}}
    internal void ShowCard(Control owner,Point location,Size size,Action<Graphics,Rectangle> draw){if(!CanShowFor(owner)){HideCard();return;}_source=owner;_draw=draw;var screen=Screen.FromPoint(location).WorkingArea;size.Width=Math.Min(size.Width,screen.Width);size.Height=Math.Min(size.Height,screen.Height);Bounds=new Rectangle(new Point(Math.Clamp(location.X,screen.Left,screen.Right-size.Width),Math.Clamp(location.Y,screen.Top,screen.Bottom-size.Height)),size);if(!Visible)Show(owner.TopLevelControl as Form??owner.FindForm());Invalidate();_dismiss.Stop();_dismiss.Start();_watch.Start();}
    internal void HideCard(){_dismiss.Stop();_watch.Stop();_source=null;Hide();}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using var fill=new SolidBrush(Color.FromArgb(255,254,247));using var border=new Pen(Color.FromArgb(157,163,170));e.Graphics.FillRectangle(fill,ClientRectangle);e.Graphics.DrawRectangle(border,0,0,Width-1,Height-1);_draw?.Invoke(e.Graphics,ClientRectangle);}
    protected override void Dispose(bool disposing){if(disposing){_dismiss.Dispose();_watch.Dispose();}base.Dispose(disposing);}
}
